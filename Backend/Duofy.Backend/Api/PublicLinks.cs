namespace Duofy.Api;

/// <summary>
/// Links, die Menschen weitergeben, zum Beispiel eine Einladung. Sie zeigen immer auf die öffentliche Adresse
/// des Servers aus der Einstellung Duofy:PublicUrl (Umgebungsvariable Duofy__PublicUrl), nie auf die Adresse,
/// über die ein Client gerade verbunden ist: Die App kann im Heimnetz über eine lokale Adresse sprechen, der
/// Link muss aber auch für die Person funktionieren, die ihn bekommt.
///
/// Fehlt die Einstellung oder ist sie keine http- oder https-Adresse, gibt es keinen Link.
/// </summary>
internal sealed class PublicLinks(IConfiguration configuration, ILogger<PublicLinks> logger)
{
    public const string SettingKey = "Duofy:PublicUrl";

    /// <summary>Der Pfad der Seite einer Einladung vor dem Token, wie ihn die Seite /einladung/{token} erwartet.</summary>
    public const string InvitationPath = "einladung/";

    /// <summary>Der Pfad der Registrierung vor dem Token einer Einladung zur Registrierung (Spec 70, R4).</summary>
    public const string RegistrationInvitationPath = "registrieren?einladung=";

    /// <summary>True, wenn eine gültige öffentliche Adresse eingestellt ist.</summary>
    public bool IsConfigured => Root() is not null;

    /// <summary>True, wenn die öffentliche Adresse eine https-Adresse ist; die gehostete Version verlangt das (Spec 79, R6).</summary>
    public bool IsHttps => Root()?.Scheme == Uri.UriSchemeHttps;

    /// <summary>Der Link <c>&lt;PublicUrl&gt;/einladung/&lt;Token&gt;</c>; null ohne gültige öffentliche Adresse.</summary>
    public string? Invitation(string token) =>
        Root() is { } root ? new Uri(root, InvitationPath + token).AbsoluteUri : null;

    /// <summary>
    /// Der Link <c>&lt;PublicUrl&gt;/registrieren?einladung=&lt;Token&gt;</c> einer Einladung zur Registrierung; null ohne
    /// gültige öffentliche Adresse.
    /// </summary>
    public string? RegistrationInvitation(string token) =>
        Root() is { } root ? new Uri(root, RegistrationInvitationPath + Uri.EscapeDataString(token)).AbsoluteUri : null;

    /// <summary>Der Link der Seite /bestaetigen, die eine neue Adresse bestätigt (Spec 53, R2).</summary>
    public string? ConfirmEmail(Guid userId, string token) =>
        Page("bestaetigen", ("id", userId.ToString()), ("token", token));

    /// <summary>Der Link der Seite /passwort-neu mit einem Token aus der Mail "Passwort vergessen" (R3).</summary>
    public string? PasswordReset(string email, string token) =>
        Page("passwort-neu", ("email", email), ("token", token));

    /// <summary>Der Link der Seite /passwort-neu mit einem Link des Administrators (R4).</summary>
    public string? PasswordResetLink(string token) =>
        Page("passwort-neu", ("link", token));

    /// <summary>Der Link der Seite /email-bestaetigen, die eine geänderte Adresse übernimmt (R5).</summary>
    public string? ConfirmEmailChange(Guid userId, string newEmail, string token) =>
        Page("email-bestaetigen", ("id", userId.ToString()), ("email", newEmail), ("token", token));

    // Eine Seite mit Parametern; jeder Wert ist für die Adresse verschlüsselt (Tokens enthalten + / =).
    private string? Page(string path, params (string Name, string Value)[] parameters) =>
        Root() is { } root
            ? new Uri(root, path + "?" + string.Join("&", parameters.Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value)}"))).AbsoluteUri
            : null;

    // Jedes Mal frisch gelesen, damit eine geänderte Einstellung ohne Neustart gilt und Tests sie setzen können.
    private Uri? Root()
    {
        var value = configuration[SettingKey]?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            logger.LogWarning("{Setting} ist keine http- oder https-Adresse: {Value}", SettingKey, value);
            return null;
        }
        // Ohne Schrägstrich am Ende würde der letzte Teil des Pfads beim Zusammensetzen ersetzt.
        return uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }
}
