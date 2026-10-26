using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Wie Mails hinausgehen (Spec 53, Bedrohungen): neben der Anfrage statt in ihr, nur verschlüsselt, wenn
/// Verschlüsselung eingestellt ist, und ohne frei getippte Texte, die wie Links aussehen.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MailDeliveryTests(ApiFixture fixture)
{
    /// <summary>Ein Versand, der hängt, bis der Test ihn loslässt.</summary>
    private sealed class BlockedMailSender : IMailSender
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Sent;

        public async Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken)
        {
            await Release.Task.WaitAsync(cancellationToken);
            Interlocked.Increment(ref Sent);
        }
    }

    /// <summary>Ein Versand, der immer scheitert.</summary>
    private sealed class FailingMailSender : IMailSender
    {
        public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Mailserver kaputt");
    }

    [Fact]
    public async Task Forgot_answers_before_the_mail_is_sent()
    {
        var blocked = new BlockedMailSender();
        await using var server = MailServer.Hosted(fixture, services => services.AddSingleton<IMailSender>(blocked));
        var email = NewEmail();
        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();

        var forgot = await server.Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Equal(0, blocked.Sent);
        blocked.Release.SetResult();
    }

    [Fact]
    public async Task A_failing_mail_server_is_logged_and_the_next_mail_still_goes_out()
    {
        await using var server = MailServer.Hosted(fixture, services => services.AddSingleton<IMailSender>(new FailingMailSender()));
        var email = NewEmail();

        var register = await server.RegisterAsync(email);
        var forgot = await server.Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
        await server.WaitForMailAsync();

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var failures = server.Logs.Messages(HostedSettings.LogCategory).Where(m => m.Contains("nicht verschickt")).ToList();
        Assert.Equal(2, failures.Count);
        Assert.All(failures, m => Assert.DoesNotContain(email, m));
    }

    /// <summary>Ein Versand, der mit einer Ausnahme scheitert, in deren Text die Adresse steht.</summary>
    private sealed class RejectingMailSender(Func<OutgoingMail, Exception> failure) : IMailSender
    {
        public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken) => throw failure(mail);
    }

    // A12: Der Mailserver nennt die abgelehnte Adresse in seinem Text; ins Log kommen nur Fehler- und Statuscode.
    [Fact]
    public async Task A_rejected_recipient_is_logged_with_the_codes_only_and_never_with_the_address_or_the_server_text()
    {
        await using var server = MailServer.Hosted(fixture, services => services.AddSingleton<IMailSender>(
            new RejectingMailSender(mail => new SmtpCommandException(
                SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, MailboxAddress.Parse(mail.To),
                $"5.1.1 <{mail.To}>: Recipient address rejected: User unknown in virtual mailbox table"))));
        var email = NewEmail();

        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();
        await server.WaitForMailAsync();

        var failure = Assert.Single(server.Logs.Messages(HostedSettings.LogCategory), m => m.Contains("nicht verschickt"));
        Assert.DoesNotContain(email, failure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recipient address rejected", failure);
        Assert.DoesNotContain("5.1.1", failure);
        Assert.Contains(nameof(SmtpErrorCode.RecipientNotAccepted), failure);
        Assert.Contains("550", failure);
    }

    // A12: Jede andere Ausnahme kommt nur mit ihrem Typ ins Log, nie mit ihrem Text.
    [Fact]
    public async Task Any_other_mail_failure_is_logged_with_its_type_only()
    {
        await using var server = MailServer.Hosted(fixture, services => services.AddSingleton<IMailSender>(
            new RejectingMailSender(mail => new InvalidOperationException($"Zustellung an {mail.To} gescheitert"))));
        var email = NewEmail();

        (await server.RegisterAsync(email)).EnsureSuccessStatusCode();
        await server.WaitForMailAsync();

        var failure = Assert.Single(server.Logs.Messages(HostedSettings.LogCategory), m => m.Contains("nicht verschickt"));
        Assert.DoesNotContain(email, failure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zustellung an", failure);
        Assert.Contains(nameof(InvalidOperationException), failure);
    }

    [Theory]
    [InlineData(true, 465, SecureSocketOptions.SslOnConnect)]
    [InlineData(true, 587, SecureSocketOptions.StartTls)]
    [InlineData(true, 25, SecureSocketOptions.StartTls)]
    [InlineData(false, 25, SecureSocketOptions.None)]
    public void With_encryption_set_the_connection_is_never_left_unencrypted(bool useTls, int port, SecureSocketOptions expected)
    {
        Assert.Equal(expected, SmtpMailSender.SocketOptions(useTls, port));
    }

    // Spec 79, Nachtrag 2026-10-26: Der Name des Absenders ist einstellbar (Duofy:Mail:FromName, sonst "Plagevo"); die
    // Adresse bleibt die eingestellte, auch wenn der Name selbst wie eine Adresse aussieht.
    [Theory]
    [InlineData("Plagevo Team", "Plagevo Team")]
    [InlineData(null, "Plagevo")]
    [InlineData("Plagevo <fremd@example.org>", "Plagevo <fremd@example.org>")]
    public async Task The_sender_shows_the_configured_name_and_keeps_the_plain_address(string? configured, string shown)
    {
        var settings = MailServer.HostedSettings();
        if (configured is not null)
        {
            settings[HostedSettings.FromNameKey] = configured;
        }
        await using var server = MailServer.Start(fixture, settings);
        var sender = new SmtpMailSender(server.Factory.Services.GetRequiredService<HostedSettings>());

        var message = sender.Message(MailTexts.PasswordReset("anna@example.de", "https://duofy.example.de/x"));

        var from = Assert.IsType<MailboxAddress>(Assert.Single(message.From));
        Assert.Equal(shown, from.Name);
        Assert.Equal(MailServer.From, from.Address);
    }

    [Fact]
    public void Mails_to_unconfirmed_addresses_carry_no_name_typed_at_registration()
    {
        var confirm = MailTexts.ConfirmEmail("anna@example.de", "https://duofy.example.de/bestaetigen?id=1&token=2");
        var reset = MailTexts.PasswordReset("anna@example.de", "https://duofy.example.de/passwort-neu?email=a&token=2");
        var change = MailTexts.ConfirmEmailChange("neu@example.de", "https://duofy.example.de/email-bestaetigen?id=1");

        Assert.All([confirm, reset, change], m => Assert.StartsWith("Hallo,\n", m.Text));
    }

    [Theory]
    [InlineData("Besuche https://evil.example/login", "Besuche https evil example login")]
    [InlineData("<b>Anna</b> & Ben", "b Anna b & Ben")]
    [InlineData("Anna   Muster", "Anna Muster")]
    [InlineData("evil．example：／／x", "evil example x")]
    [InlineData("Zoë O'Neil-Müller", "Zoë O'Neil-Müller")]
    public void Names_in_mails_lose_everything_that_makes_a_link(string typed, string shown)
    {
        Assert.Equal(shown, MailTexts.Neutral(typed));
    }

    [Fact]
    public void Names_in_mails_are_cut_to_60_characters()
    {
        Assert.Equal(60, MailTexts.Neutral(new string('a', 150)).Length);
    }

    [Fact]
    public void An_invitation_mail_shows_the_neutralised_names()
    {
        var mail = MailTexts.HouseholdInvitation("ben@example.de", "Anna www.evil.example", "Haushalt http://x.example", "https://duofy.example.de/einladung/tok");

        Assert.DoesNotContain("://x", mail.Subject);
        Assert.DoesNotContain("://x", mail.Text);
        Assert.Contains("https://duofy.example.de/einladung/tok", mail.Text);
    }
}
