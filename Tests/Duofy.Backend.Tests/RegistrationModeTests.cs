using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Der Registrierungsmodus aus der Umgebung (Spec 70, R3): A5.</summary>
[Collection(ApiCollection.Name)]
public sealed class RegistrationModeTests(ApiFixture fixture) : IAsyncLifetime
{
    private readonly List<WebApplicationFactory<Program>> factories = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var factory in factories)
        {
            await factory.DisposeAsync();
        }
    }

    /// <summary>Die gemeinsame Test-API mit diesem Modus; null heißt: nicht eingestellt.</summary>
    private HttpClient Start(string? mode)
    {
        var factory = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Duofy:RegistrationMode", mode ?? ""));
        factories.Add(factory);
        return factory.CreateClient();
    }

    // A5
    [Theory]
    [InlineData(RegistrationModes.Invite, HttpStatusCode.Forbidden, ErrorCodes.InvitationRequired)]
    [InlineData(RegistrationModes.Closed, HttpStatusCode.Forbidden, ErrorCodes.RegistrationClosed)]
    [InlineData(RegistrationModes.Open, HttpStatusCode.Created, null)]
    public async Task The_mode_decides_a_registration_without_invitation(string mode, HttpStatusCode status, string? code)
    {
        var client = Start(mode);
        var email = NewEmail("ben");

        var response = await client.RegisterAsync(Registration(email));

        Assert.Equal(status, response.StatusCode);
        if (code is not null)
        {
            Assert.Equal(code, await response.ReadCodeAsync());
            Assert.False(await fixture.Factory.WithDbAsync(db => Task.FromResult(db.Users.Any(u => u.Email == email))));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nonsense")]
    public async Task Without_a_valid_mode_registration_needs_an_invitation(string? mode)
    {
        var client = Start(mode);

        var response = await client.RegisterAsync(Registration(NewEmail("ben")));

        Assert.Equal(ErrorCodes.InvitationRequired, await response.ReadCodeAsync());
    }

    [Theory]
    [InlineData(RegistrationModes.Open)]
    [InlineData(RegistrationModes.Invite)]
    [InlineData(RegistrationModes.Closed)]
    public async Task Instance_announces_the_mode(string mode)
    {
        var client = Start(mode);

        var instance = await client.GetFromJsonAsync<InstanceResponse>("/api/v1/instance");

        Assert.Equal(mode, instance!.RegistrationMode);
    }

    [Fact]
    public async Task In_closed_mode_a_taken_address_is_not_revealed()
    {
        var (email, _) = await fixture.Factory.CreateClient().SignUpAsync();
        var client = Start(RegistrationModes.Closed);

        var response = await client.RegisterAsync(Registration(email));

        Assert.Equal(ErrorCodes.RegistrationClosed, await response.ReadCodeAsync());
    }
}
