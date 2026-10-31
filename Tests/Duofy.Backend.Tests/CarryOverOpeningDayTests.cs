using System.Net;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Das erste Konto startet am Tag der Registrierung (Spec 13), der Übertrag in den November bucht auf den 31. Oktober,
/// und am Starttag eines Kontos lässt sich nichts buchen (Spec 59, R6). Tests, die mit der echten Uhr auf diesem Konto
/// übertragen, sind deshalb ab dem 31. Oktober rot; hier steht die Regel mit fester Uhr.
/// </summary>
[Collection(ApiCollection.Name)]
public class CarryOverOpeningDayTests(ApiFixture fixture)
{
    [Theory]
    [InlineData(30, HttpStatusCode.Created, null)]
    [InlineData(31, HttpStatusCode.BadRequest, ErrorCodes.DateBeforeOpening)]
    public async Task The_carry_over_into_november_needs_a_first_account_opened_before_october_31(
        int registeredOn, HttpStatusCode status, string? code)
    {
        var today = new MovableTime(new DateTimeOffset(2026, 10, registeredOn, 10, 0, 0, TimeSpan.Zero));
        await using var api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(today)));
        var client = api.CreateClient();
        var person = await client.PersonAsync("Anna");

        var answer = await client.SendAsync(person, HttpMethod.Post, "/api/v1/plans/2026/11/carry-over", new CreateCarryOverRequest(50.00m));

        Assert.Equal(status, answer.StatusCode);
        if (code is not null)
        {
            Assert.Equal(code, await answer.ReadCodeAsync());
        }
    }
}
