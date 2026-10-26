using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Die bestätigte Adresse auf der gehosteten Version (Spec 53, R2): A2 bis A4.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EmailConfirmationTests(ApiFixture fixture) : IAsyncLifetime
{
    private MailServer server = null!;

    public Task InitializeAsync()
    {
        server = MailServer.Hosted(fixture);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await server.DisposeAsync();

    private HttpClient Client => server.Client;

    // A2
    [Fact]
    public async Task A_new_account_gets_one_confirmation_mail_and_signs_in_only_after_confirming()
    {
        var email = NewEmail();

        var register = await server.RegisterAsync(email);
        var mails = server.Mails.To(email);
        var before = await Client.LoginAsync(email);

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal("", await register.Content.ReadAsStringAsync());
        var mail = Assert.Single(mails);
        Assert.Equal("Bitte bestätige deine E-Mail-Adresse", mail.Subject);
        Assert.StartsWith("https://duofy.example.de/bestaetigen?id=", MailLink.UrlOf(mail).AbsoluteUri);
        Assert.Contains(MailLink.UrlOf(mail).AbsoluteUri, System.Net.WebUtility.HtmlDecode(mail.Html));
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(ErrorCodes.EmailNotConfirmed, await before.ReadCodeAsync());

        var confirm = await server.ConfirmAsync(mail);
        var after = await Client.LoginAsync(email);

        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task An_unconfirmed_account_with_the_wrong_password_looks_like_any_failed_sign_in()
    {
        var email = NewEmail();
        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();

        var login = await Client.LoginAsync(email, "falsches-passwort");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(ErrorCodes.LoginFailed, await login.ReadCodeAsync());
    }

    [Fact]
    public async Task A_used_or_changed_confirmation_token_is_invalid()
    {
        var email = NewEmail();
        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();
        var mail = server.Mails.To(email).Single();
        var link = MailLink.Of(mail);

        var changed = await Client.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest(Guid.Parse(link["id"]), link["token"] + "x"));
        var unknown = await Client.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest(Guid.NewGuid(), link["token"]));
        var first = await server.ConfirmAsync(mail);
        var again = await server.ConfirmAsync(mail);

        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await changed.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await unknown.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await again.ReadCodeAsync());
    }

    [Fact]
    public async Task A_confirmation_link_lives_24_hours_and_a_reset_link_one()
    {
        using var scope = server.Factory.Services.CreateScope();

        var confirmation = scope.ServiceProvider.GetRequiredService<IOptions<ConfirmationTokenProviderOptions>>().Value;
        var reset = scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
        var identity = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(24), confirmation.TokenLifespan);
        Assert.Equal(TimeSpan.FromHours(1), reset.TokenLifespan);
        Assert.Equal(ConfirmationTokenProvider.ProviderName, identity.Tokens.EmailConfirmationTokenProvider);
        Assert.Equal(ConfirmationTokenProvider.ProviderName, identity.Tokens.ChangeEmailTokenProvider);
        Assert.Equal(TokenOptions.DefaultProvider, identity.Tokens.PasswordResetTokenProvider);
    }

    [Fact]
    public async Task An_expired_confirmation_link_is_invalid()
    {
        await using var expiring = MailServer.Hosted(fixture, services =>
            services.Configure<ConfirmationTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.Zero));
        var email = NewEmail();
        (await expiring.RegisterAsync(email)).EnsureSuccessStatusCode();
        await Task.Delay(20);

        var confirm = await expiring.ConfirmAsync(expiring.Mails.To(email).Single());

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await confirm.ReadCodeAsync());
    }

    // A3
    [Fact]
    public async Task Resending_twice_within_five_minutes_sends_exactly_one_more_mail()
    {
        var email = NewEmail();
        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();

        var first = await Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", new ResendConfirmationRequest(email));
        var second = await Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", new ResendConfirmationRequest(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(2, server.Mails.To(email).Count);
        // Auch der neue Link bestätigt.
        Assert.Equal(HttpStatusCode.NoContent, (await server.ConfirmAsync(server.Mails.To(email)[1])).StatusCode);
    }

    [Fact]
    public async Task Resending_to_an_unknown_or_confirmed_address_answers_the_same_and_sends_nothing()
    {
        var confirmed = await server.ConfirmedAsync();
        var unknown = NewEmail("niemand");

        var forUnknown = await Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", new ResendConfirmationRequest(unknown));
        var forConfirmed = await Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", new ResendConfirmationRequest(confirmed));

        Assert.Equal(HttpStatusCode.Accepted, forUnknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, forConfirmed.StatusCode);
        Assert.Empty(server.Mails.To(unknown));
        Assert.Single(server.Mails.To(confirmed));
    }

    [Fact]
    public async Task After_five_minutes_the_link_can_be_sent_again()
    {
        var clock = new MovableTime(DateTimeOffset.UtcNow);
        await using var timed = MailServer.Hosted(fixture, services => services.AddSingleton<TimeProvider>(clock));
        var email = NewEmail();
        (await timed.RegisterAsync(email)).EnsureSuccessStatusCode();
        var request = new ResendConfirmationRequest(email);

        await timed.Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", request);
        clock.Advance(TimeSpan.FromMinutes(4));
        await timed.Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", request);
        clock.Advance(TimeSpan.FromMinutes(1));
        await timed.Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", request);

        Assert.Equal(3, timed.Mails.To(email).Count);
    }

    // A4
    [Fact]
    public async Task Users_from_before_the_migration_sign_in_on_a_hosted_server_without_confirming()
    {
        var connection = MailServer.FreshDatabase(fixture);
        var builder = new NpgsqlConnectionStringBuilder(connection);
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {builder.Database}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var email = NewEmail("alt");
        await using (var db = new DuofyDbContext(new DbContextOptionsBuilder<DuofyDbContext>()
            .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options, new OwnerContext(NullLogger<OwnerContext>.Instance)))
        {
            using var everything = db.Owners.Bypass("Tests: Migration an einer eigenen Datenbank");
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("AddInstanceAdminAndRegistrationInvitations");
            var hash = new PasswordHasher<AppUser>().HashPassword(new AppUser(), Password);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, " +
                "security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, " +
                "first_name, last_name, terms_accepted_at, quota_needs, quota_wants, quota_savings, is_instance_admin) " +
                "VALUES ({0}, {1}, {2}, {1}, {2}, false, {3}, {4}, {5}, false, false, true, 0, 'Alt', 'Konto', now(), 50, 30, 20, false)",
                Guid.NewGuid(), email, email.ToUpperInvariant(), hash, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString());
        }

        // Der Start der API spielt die neue Migration ein.
        await using var hosted = MailServer.Start(connection, MailServer.HostedSettings());
        var login = await hosted.Client.LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(await hosted.Factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.EmailConfirmed).SingleAsync()));
    }
}

/// <summary>Eine Uhr, die ein Test vorstellen kann.</summary>
internal sealed class MovableTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
