namespace Duofy.Api;

/// <summary>
/// Ob dieser Server die gehostete Version ist (Spec 53, R1): Duofy:Hosted (Umgebungsvariable Duofy__Hosted), dazu die
/// Mail-Einstellungen unter Duofy:Mail (Duofy__Mail__Host und so weiter).
///
/// Nur die gehostete Version verschickt Mails. Sie braucht dafür Host, Port und Absender des Mailservers und die
/// öffentliche Adresse (Duofy:PublicUrl) für die Links; fehlt eins davon, startet sie nicht
/// (<see cref="StopIfIncomplete"/>). Er startet auch nicht mit einer http-Adresse oder mit Mails ohne TLS
/// (Spec 79, R6): Links mit Tokens dürfen nie im Klartext hinausgehen. Ein selbst betriebener Server braucht nichts davon.
/// </summary>
internal sealed class HostedSettings(IConfiguration configuration, PublicLinks links, ILoggerFactory loggers)
{
    public const string LogCategory = "Duofy.Api.Mail";

    public const string HostedKey = "Duofy:Hosted";
    public const string HostKey = "Duofy:Mail:Host";
    public const string PortKey = "Duofy:Mail:Port";
    public const string UserKey = "Duofy:Mail:User";
    public const string PasswordKey = "Duofy:Mail:Password";
    public const string FromKey = "Duofy:Mail:From";
    public const string FromNameKey = "Duofy:Mail:FromName";
    public const string UseTlsKey = "Duofy:Mail:UseTls";

    /// <summary>True auf der gehosteten Version; ohne Einstellung false.</summary>
    public bool Hosted => configuration.GetValue(HostedKey, false);

    /// <summary>Der Mailserver, zum Beispiel smtp.example.de.</summary>
    public string Host => Text(HostKey) ?? "";

    /// <summary>Der Port des Mailservers; 0, wenn er fehlt oder keine Zahl von 1 bis 65535 ist.</summary>
    public int Port => int.TryParse(Text(PortKey), out var port) && port is > 0 and <= 65535 ? port : 0;

    /// <summary>Der Benutzer am Mailserver; null heißt: ohne Anmeldung.</summary>
    public string? User => Text(UserKey);

    /// <summary>Das Passwort am Mailserver. Es kommt nie ins Log.</summary>
    public string? Password => Text(PasswordKey);

    /// <summary>Die Absenderadresse der Mails.</summary>
    public string From => Text(FromKey) ?? "";

    /// <summary>Der Name, der neben der Absenderadresse steht; ohne Einstellung "Plagevo".</summary>
    public string FromName => Text(FromNameKey) ?? "Plagevo";

    /// <summary>True (der Standard): Die Verbindung zum Mailserver ist verschlüsselt.</summary>
    public bool UseTls => configuration.GetValue(UseTlsKey, true);

    /// <summary>Die Einstellungen, die einem gehosteten Server fehlen; leer, wenn alles da ist oder er nicht gehostet ist.</summary>
    public IReadOnlyList<string> Missing()
    {
        if (!Hosted)
        {
            return [];
        }
        var missing = new List<string>();
        if (Host.Length == 0)
        {
            missing.Add(HostKey);
        }
        if (Port == 0)
        {
            missing.Add(PortKey);
        }
        if (From.Length == 0)
        {
            missing.Add(FromKey);
        }
        if (!links.IsConfigured)
        {
            missing.Add(PublicLinks.SettingKey);
        }
        return missing;
    }

    /// <summary>
    /// Die Einstellungen eines gehosteten Servers, die Tokens im Klartext hinausschicken würden (Spec 79, R6): eine
    /// öffentliche Adresse ohne https und Mails ohne TLS. Leer, wenn alles verschlüsselt ist oder er nicht gehostet ist.
    /// </summary>
    public IReadOnlyList<string> Insecure()
    {
        if (!Hosted)
        {
            return [];
        }
        var insecure = new List<string>();
        if (links.IsConfigured && !links.IsHttps)
        {
            insecure.Add(PublicLinks.SettingKey);
        }
        if (!UseTls)
        {
            insecure.Add(UseTlsKey);
        }
        return insecure;
    }

    /// <summary>
    /// Beim Start: Fehlt einem gehosteten Server eine Einstellung oder ist sie unsicher, steht sie im Log, und der
    /// Start bricht ab. Eine gehostete Version, die still keine Mails schickt, würde Menschen aussperren; eine, die
    /// Links im Klartext verschickt, gibt ihre Tokens preis.
    /// </summary>
    public void StopIfIncomplete()
    {
        var logger = loggers.CreateLogger(LogCategory);
        var missing = Missing();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing);
            logger.LogCritical("Gehosteter Server ohne Einstellung {Settings}; Start abgebrochen.", names);
            throw new InvalidOperationException($"Duofy:Hosted ist true, aber es fehlt: {names}");
        }
        var insecure = Insecure();
        if (insecure.Count > 0)
        {
            var names = string.Join(", ", insecure);
            logger.LogCritical("Gehosteter Server mit unverschlüsselter Einstellung {Settings} (verlangt: https und Mails mit TLS); Start abgebrochen.", names);
            throw new InvalidOperationException($"Duofy:Hosted ist true, aber unverschlüsselt: {names}");
        }
        logger.LogInformation(Hosted ? "Gehostete Version: Mails gehen über {Host}:{Port}." : "Selbst betriebener Server: keine Mails.", Host, Port);
    }

    private string? Text(string key)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
