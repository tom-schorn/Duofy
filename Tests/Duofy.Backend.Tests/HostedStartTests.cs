using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Einstellung Duofy:Hosted (Spec 53, R1): Nur die gehostete Version verschickt Mails, und sie startet nur mit
/// vollständigen Mail-Einstellungen. A1.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HostedStartTests(ApiFixture fixture)
{
    // A1
    [Theory]
    [InlineData("Duofy:Mail:Host")]
    [InlineData("Duofy:Mail:Port")]
    [InlineData("Duofy:Mail:From")]
    [InlineData("Duofy:PublicUrl")]
    public void A_hosted_server_without_a_mail_setting_stops_at_start_and_names_it(string missing)
    {
        var settings = MailServer.HostedSettings();
        settings[missing] = "";

        var server = (MailServer?)null;
        var start = Record.Exception(() => server = MailServer.Start(fixture, settings));

        Assert.NotNull(start);
        Assert.Null(server);
        Assert.Contains(missing, start.ToString());
    }

    [Fact]
    public async Task The_stop_at_start_is_logged_with_the_missing_setting()
    {
        var settings = MailServer.HostedSettings();
        settings["Duofy:Mail:Host"] = "";
        var logs = new CapturedLogs();
        await using var factory = new MailServer.MailApiFactory(fixture.ConnectionString, settings, logs, _ => { });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(logs.Messages(HostedSettings.LogCategory), m => m.Contains("Duofy:Mail:Host"));
    }

    // Spec 79, A4: Tokens dürfen nie im Klartext hinausgehen.
    [Theory]
    [InlineData("Duofy:PublicUrl", "http://app.plagevo.de")]
    [InlineData("Duofy:Mail:UseTls", "false")]
    public async Task A_hosted_server_without_https_or_mail_tls_stops_at_start_and_logs_the_setting(string setting, string value)
    {
        var settings = MailServer.HostedSettings();
        settings[setting] = value;
        var logs = new CapturedLogs();
        await using var factory = new MailServer.MailApiFactory(fixture.ConnectionString, settings, logs, _ => { });

        var start = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(start);
        Assert.Contains(setting, start.ToString());
        Assert.Contains(logs.Messages(HostedSettings.LogCategory), m => m.Contains(setting));
    }

    [Fact]
    public async Task A_self_hosted_server_may_use_http_and_mail_without_tls()
    {
        await using var server = MailServer.Start(fixture, new Dictionary<string, string>
        {
            ["Duofy:Hosted"] = "false",
            ["Duofy:PublicUrl"] = "http://duofy.local",
            ["Duofy:Mail:UseTls"] = "false",
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, (await server.Client.GetAsync("/api/v1/health")).StatusCode);
    }

    [Fact]
    public void A_port_that_is_no_port_counts_as_missing()
    {
        var settings = MailServer.HostedSettings();
        settings["Duofy:Mail:Port"] = "smtp";

        var start = Record.Exception(() => MailServer.Start(fixture, settings));

        Assert.NotNull(start);
        Assert.Contains("Duofy:Mail:Port", start.ToString());
    }

    // A1
    [Fact]
    public async Task A_self_hosted_server_starts_without_mail_settings_and_has_no_mail_endpoints()
    {
        await using var server = MailServer.SelfHosted(fixture);

        var forgot = await server.Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest("anna@example.de"));
        var resend = await server.Client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", new ResendConfirmationRequest("anna@example.de"));
        var confirm = await server.Client.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest(Guid.NewGuid(), "x"));
        var reset = await server.Client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest("anna@example.de", "x", "neues-passwort"));
        var change = await server.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new ConfirmEmailChangeRequest(Guid.NewGuid(), "neu@example.de", "x"));

        Assert.Equal(HttpStatusCode.NotFound, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resend.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
    }

    // A1: ohne Hosted geht nie eine Mail hinaus, auch nicht bei Registrierung, Einladungen oder E-Mail-Wechsel.
    [Fact]
    public async Task A_self_hosted_server_never_sends_mail()
    {
        await using var server = MailServer.SelfHosted(fixture);
        var email = NewEmail();
        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();
        var anna = await server.SignInAsync(email);
        await server.Client.FoundAsync(anna);
        var household = await server.Client.MineAsync(anna);

        var invite = await server.Client.SendAsync(anna, HttpMethod.Post, HouseholdDriver.Invitations(household.Id),
            new CreateHouseholdInvitationRequest(NewEmail("ben")));
        var change = await server.Client.SendAsync(anna, HttpMethod.Post, "/api/v1/users/me/email",
            new ChangeEmailRequest(NewEmail("neu"), Password));

        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Empty(server.Mails.All);
    }

    [Fact]
    public async Task The_instance_says_whether_it_is_hosted()
    {
        await using var hosted = MailServer.Hosted(fixture);
        await using var selfHosted = MailServer.SelfHosted(fixture);

        Assert.True((await hosted.Client.GetFromJsonAsync<InstanceResponse>("/api/v1/instance"))!.Hosted);
        Assert.False((await selfHosted.Client.GetFromJsonAsync<InstanceResponse>("/api/v1/instance"))!.Hosted);
    }
}
