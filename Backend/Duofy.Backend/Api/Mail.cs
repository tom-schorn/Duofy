using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Duofy.Api;

/// <summary>Eine Mail an eine Person: Text und eine einfache HTML-Fassung mit demselben Inhalt.</summary>
internal sealed record OutgoingMail(string To, string Subject, string Text, string Html);

/// <summary>
/// Verschickt eine Mail (Spec 53). Im Betrieb über SMTP (<see cref="SmtpMailSender"/>), in den Tests ein Ersatz, der
/// sich die Mails nur merkt. Wer eine Mail schicken will, geht über <see cref="Mailer"/>, nie direkt hierher: Nur der
/// prüft, ob dieser Server überhaupt Mails schickt.
/// </summary>
internal interface IMailSender
{
    Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken);
}

/// <summary>Der Versand über SMTP mit MailKit, mit den Einstellungen aus <see cref="HostedSettings"/>.</summary>
internal sealed class SmtpMailSender(HostedSettings settings) : IMailSender
{
    /// <summary>
    /// Wie die Verbindung verschlüsselt wird: Mit Verschlüsselung über Port 465 von Anfang an, sonst mit STARTTLS, das
    /// der Mailserver anbieten muss; bietet er es nicht an, scheitert der Versand, statt unverschlüsselt weiterzugehen.
    /// Ohne Verschlüsselung (Duofy:Mail:UseTls=false) gar nicht.
    /// </summary>
    public static SecureSocketOptions SocketOptions(bool useTls, int port) =>
        !useTls ? SecureSocketOptions.None
        : port == 465 ? SecureSocketOptions.SslOnConnect
        : SecureSocketOptions.StartTls;

    public async Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Duofy", settings.From));
        message.To.Add(MailboxAddress.Parse(mail.To));
        message.Subject = mail.Subject;
        message.Body = new BodyBuilder { TextBody = mail.Text, HtmlBody = mail.Html }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, SocketOptions(settings.UseTls, settings.Port), cancellationToken);
        if (settings.User is { } user)
        {
            await client.AuthenticateAsync(user, settings.Password ?? "", cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}

/// <summary>
/// Der einzige Weg zu einer Mail. Auf einem selbst betriebenen Server geht nie eine hinaus (R1). Die Mail kommt nur in
/// die Warteschlange (<see cref="MailQueue"/>); verschickt wird sie daneben (<see cref="MailDispatcher"/>). So
/// antwortet eine Anfrage gleich schnell, ob eine Mail hinausgeht oder nicht, und verrät auch über die Zeit nicht,
/// wer ein Konto hat.
/// </summary>
internal sealed class Mailer(HostedSettings settings, MailQueue queue, ILoggerFactory loggers)
{
    private readonly ILogger logger = loggers.CreateLogger(HostedSettings.LogCategory);

    /// <summary>True, wenn dieser Server Mails schickt: nur auf der gehosteten Version.</summary>
    public bool IsOn => settings.Hosted;

    /// <summary>Stellt die Mail in die Warteschlange; false, wenn der Server keine Mails schickt oder die Schlange voll ist.</summary>
    public bool Queue(OutgoingMail mail, string purpose)
    {
        if (!IsOn)
        {
            logger.LogError("Mail ({Purpose}) auf einem selbst betriebenen Server verlangt; nicht verschickt.", purpose);
            return false;
        }
        if (!queue.TryAdd(mail, purpose))
        {
            logger.LogError("Mail ({Purpose}) nicht verschickt: Warteschlange voll.", purpose);
            return false;
        }
        return true;
    }
}

/// <summary>
/// Die Mails, die noch hinaus müssen, im Speicher. Ein Neustart verliert, was noch drin ist; die Person fordert den
/// Link dann noch einmal an.
/// </summary>
internal sealed class MailQueue
{
    // So viele Mails warten höchstens; mehr deutet auf einen hängenden Mailserver oder eine Flut von Anfragen.
    private const int Capacity = 1_000;

    private readonly Channel<(OutgoingMail Mail, string Purpose)> channel =
        Channel.CreateBounded<(OutgoingMail, string)>(new BoundedChannelOptions(Capacity) { SingleReader = true });

    private int pending;

    /// <summary>Wie viele Mails noch nicht fertig sind, auch die gerade im Versand.</summary>
    public int Pending => Volatile.Read(ref pending);

    public bool TryAdd(OutgoingMail mail, string purpose)
    {
        Interlocked.Increment(ref pending);
        if (channel.Writer.TryWrite((mail, purpose)))
        {
            return true;
        }
        Interlocked.Decrement(ref pending);
        return false;
    }

    public IAsyncEnumerable<(OutgoingMail Mail, string Purpose)> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Meldet eine Mail als fertig, verschickt oder gescheitert.</summary>
    public void Done() => Interlocked.Decrement(ref pending);
}

/// <summary>
/// Verschickt die Mails aus der Warteschlange, eine nach der anderen. Ein Fehler beim Versand landet mit dem Zweck im
/// Log, nie mit der Adresse, und hält die nächste Mail nicht auf.
/// </summary>
internal sealed class MailDispatcher(MailQueue queue, IMailSender sender, ILoggerFactory loggers) : BackgroundService
{
    private readonly ILogger logger = loggers.CreateLogger(HostedSettings.LogCategory);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (mail, purpose) in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(mail, stoppingToken);
                logger.LogInformation("Mail ({Purpose}) verschickt.", purpose);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Mail ({Purpose}) beim Herunterfahren nicht mehr verschickt.", purpose);
                return;
            }
            // Der Mailserver lehnt einen Befehl ab, etwa die Adresse. Sein Text nennt oft die Adresse ("5.1.1 <...>"),
            // deshalb kommen nur die beiden Codes ins Log, nie die Ausnahme selbst (A12).
            catch (SmtpCommandException ex)
            {
                logger.LogError("Mail ({Purpose}) nicht verschickt: Mailserver lehnt ab ({ErrorCode}, Status {StatusCode}).",
                    purpose, ex.ErrorCode, (int)ex.StatusCode);
            }
            // Jeder andere Fehler der Verbindung oder des Servers: nur der Typ, denn auch dieser Text kann die Adresse
            // enthalten. Der Versand der übrigen Mails soll weitergehen.
            catch (Exception ex)
            {
                logger.LogError("Mail ({Purpose}) nicht verschickt: {ErrorType}.", purpose, ex.GetType().Name);
            }
            finally
            {
                queue.Done();
            }
        }
    }
}


/// <summary>
/// Die Bremse für Mails (Spec 53): Je Zweck und Adresse höchstens eine Mail alle fünf Minuten, und je Administrator
/// höchstens 20 Einladungen zur Registrierung am Tag (Tag in Berlin). Eine gebremste Anfrage bekommt dieselbe
/// Antwort, als wäre die Mail hinausgegangen. Der Zähler lebt im Speicher; ein Neustart setzt ihn zurück.
/// </summary>
internal sealed class MailThrottle(TimeProvider time)
{
    /// <summary>So lange wartet die nächste Mail desselben Zwecks an dieselbe Adresse.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>So viele Einladungen zur Registrierung darf ein Administrator am Tag per Mail schicken.</summary>
    public const int RegistrationInvitationsPerDay = 20;

    // Ab so vielen Einträgen räumt die Bremse alte weg, damit der Speicher nicht wächst.
    private const int CleanupThreshold = 10_000;

    private readonly ConcurrentDictionary<(string Purpose, string Address), DateTimeOffset> lastMail = new();
    private readonly ConcurrentDictionary<(Guid Admin, DateOnly Day), int> invitationsToday = new();

    /// <summary>True, wenn eine Mail dieses Zwecks an diese Adresse jetzt hinaus darf; merkt sie sich dann gleich.</summary>
    public bool TryTake(string purpose, string address)
    {
        var now = time.GetUtcNow();
        var key = (purpose, address.Trim().ToLowerInvariant());
        if (lastMail.Count > CleanupThreshold)
        {
            foreach (var (old, at) in lastMail)
            {
                if (now - at >= Interval)
                {
                    lastMail.TryRemove(old, out _);
                }
            }
        }

        while (true)
        {
            if (!lastMail.TryGetValue(key, out var last))
            {
                if (lastMail.TryAdd(key, now))
                {
                    return true;
                }
                continue;
            }
            if (now - last < Interval)
            {
                return false;
            }
            if (lastMail.TryUpdate(key, now, last))
            {
                return true;
            }
        }
    }

    /// <summary>True, wenn der Administrator heute noch eine Einladung zur Registrierung per Mail schicken darf; zählt sie dann.</summary>
    public bool TryTakeRegistrationInvitation(Guid adminId)
    {
        var today = Berlin.Today(time);
        var key = (adminId, today);
        var count = invitationsToday.AddOrUpdate(key, 1, (_, n) => n + 1);
        foreach (var old in invitationsToday.Keys.Where(k => k.Day < today))
        {
            invitationsToday.TryRemove(old, out _);
        }
        return count <= RegistrationInvitationsPerDay;
    }
}

/// <summary>
/// Die Texte der Mails, auf Deutsch, mit dem Link als Text und als Knopf in HTML.
///
/// Mails an eine Adresse, die (noch) niemand bestätigt hat, nennen keinen Namen: Wer sich mit fremder Adresse
/// registriert, soll Duofy keinen eigenen Text an sie schicken lassen. Namen in Einladungen gehen durch
/// <see cref="Neutral"/>.
/// </summary>
internal static partial class MailTexts
{
    /// <summary>So lang darf ein Name in einer Mail höchstens sein.</summary>
    public const int NameMaxLength = 60;

    /// <summary>
    /// Ein Name, den jemand selbst getippt hat, ohne alles, was daraus einen Link oder HTML machen könnte: Nach der
    /// Unicode-Normalisierung (NFKC, damit ähnlich aussehende Zeichen zu den gewöhnlichen werden) bleiben nur
    /// Buchstaben, Ziffern, Leerzeichen, Bindestrich, Apostroph und &amp;; alles andere, auch der Punkt, wird zum
    /// Leerzeichen. Dazu höchstens 60 Zeichen.
    /// </summary>
    public static string Neutral(string text)
    {
        var plain = NotAllowed().Replace(text.Normalize(System.Text.NormalizationForm.FormKC), " ");
        plain = Spaces().Replace(plain, " ").Trim();
        return plain.Length <= NameMaxLength ? plain : plain[..NameMaxLength].TrimEnd();
    }

    [GeneratedRegex(@"[^\p{L}\p{M}\p{N} \-'&]")]
    private static partial Regex NotAllowed();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    public static OutgoingMail ConfirmEmail(string to, string link) => Build(to,
        "Bitte bestätige deine E-Mail-Adresse",
        "Hallo,",
        "bitte bestätige deine E-Mail-Adresse für Duofy. Der Link gilt 24 Stunden.",
        "Adresse bestätigen", link,
        "Wenn du dich nicht bei Duofy registriert hast, kannst du diese Mail ignorieren.");

    public static OutgoingMail PasswordReset(string to, string link) => Build(to,
        "Neues Passwort für Duofy",
        "Hallo,",
        "mit diesem Link setzt du ein neues Passwort für Duofy. Er gilt eine Stunde. Danach bist du auf allen Geräten abgemeldet.",
        "Neues Passwort setzen", link,
        "Wenn du kein neues Passwort angefordert hast, kannst du diese Mail ignorieren; dein Passwort bleibt, wie es ist.");

    public static OutgoingMail ConfirmEmailChange(string to, string link) => Build(to,
        "Bitte bestätige deine neue E-Mail-Adresse",
        "Hallo,",
        "du möchtest diese Adresse für dein Duofy-Konto verwenden. Erst mit diesem Link gilt sie. Er gilt 24 Stunden.",
        "Neue Adresse bestätigen", link,
        "Wenn du das nicht warst, kannst du diese Mail ignorieren; dein Konto behält seine bisherige Adresse.");

    public static OutgoingMail EmailChanged(string to, string newEmail) => Build(to,
        "Deine E-Mail-Adresse bei Duofy wurde geändert",
        "Hallo,",
        $"dein Duofy-Konto verwendet ab jetzt die Adresse {newEmail}. An diese Adresse kommen keine Mails von Duofy mehr.",
        null, null,
        "Wenn du das nicht warst, wende dich bitte sofort an die Betreiber von Duofy.");

    public static OutgoingMail HouseholdInvitation(string to, string inviter, string household, string link) => Build(to,
        $"{Neutral(inviter)} lädt dich in den Haushalt {Neutral(household)} ein",
        "Hallo,",
        $"{Neutral(inviter)} lädt dich ein, in Duofy gemeinsam im Haushalt {Neutral(household)} zu planen. Der Link gilt 14 Tage.",
        "Einladung ansehen", link,
        "Wenn du nicht mitmachen möchtest, kannst du diese Mail ignorieren.");

    public static OutgoingMail RegistrationInvitation(string to, string inviter, string link) => Build(to,
        "Einladung zu Duofy",
        "Hallo,",
        $"{Neutral(inviter)} lädt dich ein, dich bei Duofy zu registrieren. Der Link gilt sieben Tage und lässt sich einmal benutzen.",
        "Jetzt registrieren", link,
        "Wenn du kein Konto möchtest, kannst du diese Mail ignorieren.");

    private static OutgoingMail Build(string to, string subject, string greeting, string body, string? button, string? link, string footer)
    {
        var text = link is null
            ? $"{greeting}\n\n{body}\n\n{footer}\n"
            : $"{greeting}\n\n{body}\n\n{link}\n\n{footer}\n";
        Func<string?, string?> e = WebUtility.HtmlEncode;
        var action = link is null
            ? ""
            : $"<p><a href=\"{e(link)}\" style=\"display:inline-block;padding:10px 16px;background:#2f6f4f;color:#fff;text-decoration:none;border-radius:6px\">{e(button)}</a></p>"
              + $"<p style=\"font-size:13px;color:#555\">Oder diesen Link öffnen: {e(link)}</p>";
        var html = $"<!doctype html><html lang=\"de\"><body style=\"font-family:sans-serif;line-height:1.5\">"
            + $"<p>{e(greeting)}</p><p>{e(body)}</p>{action}<p style=\"font-size:13px;color:#555\">{e(footer)}</p></body></html>";
        return new OutgoingMail(to, subject, text, html);
    }
}
