using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Registrierung (R2): A2, A3, A4.</summary>
[Collection(ApiCollection.Name)]
public class RegisterTests
{
    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public RegisterTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    // A2
    [Fact]
    public async Task Registered_person_can_sign_in_and_sees_their_names()
    {
        var email = NewEmail();

        var register = await client.RegisterAsync(Registration(email));

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.LoginAsync(email);
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        var me = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", tokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = (await me.Content.ReadFromJsonAsync<MeResponse>())!;
        Assert.Equal(("Anna", "Beispiel", email), (body.FirstName, body.LastName, body.Email));
    }

    [Fact]
    public async Task Registration_stores_the_consent_time_and_trims_the_names()
    {
        var email = NewEmail();
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        await client.RegisterAsync(new RegisterRequest("  Anna ", " Beispiel  ", email, Password, true));

        var user = await fixture.Factory.WithDbAsync(db => db.Users.SingleAsync(u => u.Email == email));
        Assert.Equal(("Anna", "Beispiel"), (user.FirstName, user.LastName));
        Assert.InRange(user.TermsAcceptedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.False(user.EmailConfirmed);
    }

    public static TheoryData<string, RegisterRequest> InvalidRegistrations()
    {
        string Email() => NewEmail("invalid");
        return new TheoryData<string, RegisterRequest>
        {
            { ErrorCodes.TermsNotAccepted, new RegisterRequest("Anna", "Beispiel", Email(), Password, false) },
            { ErrorCodes.FirstNameRequired, new RegisterRequest("", "Beispiel", Email(), Password, true) },
            { ErrorCodes.FirstNameRequired, new RegisterRequest("   ", "Beispiel", Email(), Password, true) },
            { ErrorCodes.LastNameRequired, new RegisterRequest("Anna", "", Email(), Password, true) },
            { ErrorCodes.PasswordTooShort, new RegisterRequest("Anna", "Beispiel", Email(), "neun-zeic", true) },
            { ErrorCodes.EmailInvalid, new RegisterRequest("Anna", "Beispiel", "anna.example.org", Password, true) },
            { ErrorCodes.EmailInvalid, new RegisterRequest("Anna", "Beispiel", "", Password, true) },
            { ErrorCodes.FirstNameTooLong, new RegisterRequest(new string('a', 101), "Beispiel", Email(), Password, true) },
            { ErrorCodes.LastNameTooLong, new RegisterRequest("Anna", new string('b', 101), Email(), Password, true) },
        };
    }

    // A3
    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task Invalid_registration_answers_400_with_its_code_and_creates_no_user(string code, RegisterRequest request)
    {
        var response = await client.RegisterAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ReadCodeAsync());
        var exists = await fixture.Factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == request.Email));
        Assert.False(exists);
    }

    // A4
    [Fact]
    public async Task Same_email_in_other_case_is_taken()
    {
        var email = NewEmail();
        (await client.RegisterAsync(Registration(email))).EnsureSuccessStatusCode();

        var second = await client.RegisterAsync(Registration(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await second.ReadCodeAsync());
    }

    // Zwei gleichzeitige Registrierungen: Beide finden die Adresse noch frei, die
    // zweite scheitert erst am eindeutigen Index der Datenbank.
    [Fact]
    public async Task Same_email_registered_at_the_same_moment_is_taken()
    {
        var email = NewEmail();
        using var racingApi = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IUserValidator<AppUser>, RivalRegistersFirst>()));

        var response = await racingApi.CreateClient().RegisterAsync(Registration(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await response.ReadCodeAsync());
        var names = await fixture.Factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.FirstName).ToListAsync());
        Assert.Equal(["Rivalin"], names);
    }

    // Läuft nach der Prüfung von Identity, die die Adresse frei gefunden hat, und legt
    // genau dann in einer eigenen Verbindung ein Konto mit derselben Adresse an.
    private sealed class RivalRegistersFirst(IServiceScopeFactory scopes) : IUserValidator<AppUser>
    {
        public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DuofyDbContext>();
            db.Users.Add(new AppUser
            {
                UserName = user.UserName,
                NormalizedUserName = manager.NormalizeName(user.UserName),
                Email = user.Email,
                NormalizedEmail = manager.NormalizeEmail(user.Email),
                FirstName = "Rivalin",
                LastName = "Beispiel",
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            await db.SaveChangesAsync();
            return IdentityResult.Success;
        }
    }

    [Fact]
    public async Task Health_answers_200()
    {
        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
