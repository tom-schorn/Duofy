using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.AdminDriver;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Der erste Administrator über den Einrichtungscode (Spec 70, R1 und R2): A1 bis A4 und A16.</summary>
[Collection(ApiCollection.Name)]
public sealed class SetupCodeTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string Code = "s3cret-einrichtung";

    private readonly List<FreshInstance> instances = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var instance in instances)
        {
            await instance.DisposeAsync();
        }
    }

    private FreshInstance Start(string? setupCode, string mode)
    {
        var instance = FreshInstance.Start(fixture, setupCode, mode);
        instances.Add(instance);
        return instance;
    }

    // A1
    [Fact]
    public async Task The_right_code_makes_the_first_person_administrator_even_when_registration_is_closed()
    {
        var server = Start(Code, RegistrationModes.Closed);
        Assert.True((await server.InstanceAsync()).SetupPending);

        var response = await server.RegisterAsync("Anna", setupCode: Code);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var instance = await server.InstanceAsync();
        Assert.False(instance.SetupPending);
        Assert.Equal(RegistrationModes.Closed, instance.RegistrationMode);
        var anna = Assert.Single(await server.Factory.WithDbAsync(db => Task.FromResult(db.Users.ToList())));
        Assert.True(anna.IsInstanceAdmin);
    }

    // A2
    [Fact]
    public async Task A_wrong_code_answers_setup_code_invalid_and_creates_nobody()
    {
        var server = Start(Code, RegistrationModes.Closed);

        var response = await server.RegisterAsync("Ben", setupCode: "wrong");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.SetupCodeInvalid, await response.ReadCodeAsync());
        Assert.Equal(0, await server.UserCountAsync());
    }

    [Fact]
    public async Task Without_a_configured_code_any_code_answers_the_same_and_creates_nobody()
    {
        var server = Start(setupCode: null, RegistrationModes.Open);

        var response = await server.RegisterAsync("Ben", setupCode: "");
        var guessed = await server.RegisterAsync("Ben", setupCode: "irgendwas");

        // Ein leeres Feld ist kein Code: Die Registrierung geht im offenen Modus normal durch.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Die Antwort verrät nicht, ob überhaupt ein Code eingestellt ist.
        Assert.Equal(HttpStatusCode.BadRequest, guessed.StatusCode);
        Assert.Equal(ErrorCodes.SetupCodeInvalid, await guessed.ReadCodeAsync());
        Assert.Equal(1, await server.UserCountAsync());
    }

    // A3
    [Fact]
    public async Task Once_there_is_an_administrator_the_code_is_an_ordinary_registration_under_the_mode()
    {
        var server = Start(Code, RegistrationModes.Open);
        await server.PersonAsync("Anna", setupCode: Code);

        var ben = await server.PersonAsync("Ben", setupCode: Code);
        var wrong = await server.RegisterAsync("Clara", setupCode: "wrong");

        Assert.False(await server.IsAdminAsync(ben.Id));
        // Der Code hat keine Wirkung mehr, auch ein falscher nicht.
        Assert.Equal(HttpStatusCode.Created, wrong.StatusCode);
    }

    [Fact]
    public async Task Once_there_is_an_administrator_a_registration_with_the_code_follows_the_closed_mode()
    {
        var server = Start(Code, RegistrationModes.Closed);
        await server.PersonAsync("Anna", setupCode: Code);

        var response = await server.RegisterAsync("Ben", setupCode: Code);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.RegistrationClosed, await response.ReadCodeAsync());
    }

    [Fact]
    public async Task Two_registrations_with_the_code_at_the_same_time_make_only_one_administrator()
    {
        var server = Start(Code, RegistrationModes.Open);

        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => server.RegisterAsync($"Person{i}", setupCode: Code)));

        Assert.All(answers, a => Assert.Equal(HttpStatusCode.Created, a.StatusCode));
        var admins = await server.Factory.WithDbAsync(db => Task.FromResult(db.Users.Count(u => u.IsInstanceAdmin)));
        Assert.Equal(1, admins);
    }

    // A4
    [Fact]
    public async Task Without_a_code_and_without_an_administrator_the_start_logs_the_warning_once()
    {
        var server = Start(setupCode: null, RegistrationModes.Invite);

        var instance = await server.InstanceAsync();
        await server.InstanceAsync();

        Assert.False(instance.SetupPending);
        Assert.Equal(RegistrationModes.Invite, instance.RegistrationMode);
        var warning = Assert.Single(server.Logs.Messages(FreshInstance.LogCategory),
            m => m.Contains("Kein Einrichtungscode gesetzt", StringComparison.Ordinal));
        Assert.Equal("Kein Einrichtungscode gesetzt: Es kann kein Admin entstehen.", warning);
    }

    [Fact]
    public async Task With_a_code_the_start_logs_no_warning()
    {
        var server = Start(Code, RegistrationModes.Invite);

        await server.InstanceAsync();

        Assert.DoesNotContain(server.Logs.Messages(FreshInstance.LogCategory),
            m => m.Contains("Kein Einrichtungscode gesetzt", StringComparison.Ordinal));
    }

    // A17: Ein Code unter 16 Zeichen gilt als nicht gesetzt; der Start sagt es in einer Zeile, ohne den Code.
    [Fact]
    public async Task A_code_shorter_than_16_characters_counts_as_missing_and_the_start_says_so_once()
    {
        const string tooShort = "einrichtung-202";
        var server = Start(tooShort, RegistrationModes.Open);
        var tom = await server.PersonAsync("Tom");

        var instance = await server.InstanceAsync();
        var register = await server.RegisterAsync("Anna", setupCode: tooShort);
        var claim = await server.Client.SendAsync(tom, HttpMethod.Post, ClaimAdmin, new ClaimAdminRequest(tooShort));

        Assert.False(instance.SetupPending);
        Assert.Equal(ErrorCodes.SetupCodeInvalid, await register.ReadCodeAsync());
        Assert.Equal(ErrorCodes.SetupCodeInvalid, await claim.ReadCodeAsync());
        Assert.Equal(1, await server.UserCountAsync());
        Assert.False(await server.IsAdminAsync(tom.Id));
        var messages = server.Logs.Messages(FreshInstance.LogCategory);
        var warning = Assert.Single(messages);
        Assert.Contains("kürzer als 16 Zeichen", warning, StringComparison.Ordinal);
        Assert.DoesNotContain(messages, m => m.Contains(tooShort, StringComparison.Ordinal));
    }

    // A17: Genau 16 Zeichen reichen.
    [Fact]
    public async Task A_code_of_exactly_16_characters_makes_the_first_administrator()
    {
        const string exactly16 = "einrichtung-2026";
        var server = Start(exactly16, RegistrationModes.Closed);

        var instance = await server.InstanceAsync();
        var anna = await server.PersonAsync("Anna", setupCode: exactly16);

        Assert.True(instance.SetupPending);
        Assert.True(await server.IsAdminAsync(anna.Id));
        Assert.Empty(server.Logs.Messages(FreshInstance.LogCategory));
    }

    // A16
    [Fact]
    public async Task An_existing_account_becomes_administrator_with_the_code_and_a_second_claim_is_refused()
    {
        var server = Start(Code, RegistrationModes.Open);
        var tom = await server.PersonAsync("Tom");
        var ben = await server.PersonAsync("Ben");

        var claimed = await server.Client.SendAsync(tom, HttpMethod.Post, ClaimAdmin, new ClaimAdminRequest(Code));
        var second = await server.Client.SendAsync(ben, HttpMethod.Post, ClaimAdmin, new ClaimAdminRequest(Code));

        Assert.Equal(HttpStatusCode.OK, claimed.StatusCode);
        Assert.True((await claimed.Content.ReadFromJsonAsync<MeResponse>())!.IsInstanceAdmin);
        Assert.True(await server.IsAdminAsync(tom.Id));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(ErrorCodes.SetupCodeInvalid, await second.ReadCodeAsync());
        Assert.False(await server.IsAdminAsync(ben.Id));
        Assert.False((await server.InstanceAsync()).SetupPending);
    }

    [Fact]
    public async Task A_claim_with_a_wrong_code_or_without_a_configured_code_is_refused()
    {
        var server = Start(Code, RegistrationModes.Open);
        var tom = await server.PersonAsync("Tom");
        var withoutCode = Start(setupCode: null, RegistrationModes.Open);
        var ben = await withoutCode.PersonAsync("Ben");

        var wrong = await server.Client.SendAsync(tom, HttpMethod.Post, ClaimAdmin, new ClaimAdminRequest("wrong"));
        var empty = await withoutCode.Client.SendAsync(ben, HttpMethod.Post, ClaimAdmin, new ClaimAdminRequest(""));

        Assert.Equal(ErrorCodes.SetupCodeInvalid, await wrong.ReadCodeAsync());
        Assert.Equal(ErrorCodes.SetupCodeInvalid, await empty.ReadCodeAsync());
        Assert.False(await server.IsAdminAsync(tom.Id));
        Assert.False(await withoutCode.IsAdminAsync(ben.Id));
    }

    [Fact]
    public async Task Me_says_who_is_administrator()
    {
        var server = Start(Code, RegistrationModes.Open);
        var anna = await server.PersonAsync("Anna", setupCode: Code);
        var ben = await server.PersonAsync("Ben");

        var annaMe = await (await server.Client.SendAsync(anna, HttpMethod.Get, "/api/v1/users/me")).Content.ReadFromJsonAsync<MeResponse>();
        var benMe = await (await server.Client.SendAsync(ben, HttpMethod.Get, "/api/v1/users/me")).Content.ReadFromJsonAsync<MeResponse>();

        Assert.True(annaMe!.IsInstanceAdmin);
        Assert.False(benMe!.IsInstanceAdmin);
    }
}
