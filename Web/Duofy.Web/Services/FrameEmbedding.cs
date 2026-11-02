using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;

namespace Duofy.Web.Services;

/// <summary>
/// Die Seiten, die die Web-Variante in einem iframe zeigen dürfen, zum Beispiel ein eigenes Home Assistant:
/// Duofy:FrameAncestors (Umgebungsvariable Duofy__FrameAncestors), Adressen durch Leerzeichen getrennt.
/// Leer oder nicht gesetzt ändert nichts: Dann darf nur die eigene Adresse einbetten.
/// </summary>
public sealed partial class FrameAncestors
{
    public const string SettingKey = "Duofy:FrameAncestors";

    private const int HighestPort = 65535;

    private FrameAncestors(IReadOnlyList<string> addresses)
    {
        Addresses = addresses;
    }

    public IReadOnlyList<string> Addresses { get; }

    /// <summary>Wahr, sobald mindestens eine fremde Seite einbetten darf.</summary>
    public bool AllowsForeignSites => Addresses.Count > 0;

    /// <summary>Der Wert der Regel frame-ancestors: die eigene Adresse und genau die Liste.</summary>
    public string Policy => string.Join(' ', Addresses.Prepend("'self'"));

    /// <summary>
    /// Der Wert landet in einem Antwortkopf. Deshalb gilt eine Positivliste statt einzelner Verbote; alles andere
    /// bricht den Start ab, damit ein Tippfehler nie still mehr erlaubt als gemeint. Weißraum, auch Zeilenumbrüche,
    /// trennt nur und erreicht den Kopf nie.
    /// </summary>
    public static FrameAncestors Parse(string? value, bool isDevelopment)
    {
        var addresses = (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var address in addresses)
        {
            if (!IsAllowed(address, isDevelopment))
            {
                var schemes = isDevelopment ? "https://rechner[:port] oder http://rechner[:port]" : "https://rechner[:port]";
                throw new InvalidOperationException(
                    $"{SettingKey} enthält die ungültige Adresse '{address}'. Erlaubt ist nur {schemes}, mehrere "
                    + "durch Leerzeichen getrennt, ohne Pfad, Schrägstrich am Ende, * und Anführungszeichen. Siehe .env.example.");
            }
        }
        return new FrameAncestors(addresses);
    }

    private static bool IsAllowed(string address, bool isDevelopment)
    {
        var match = AddressPattern().Match(address);
        if (!match.Success)
        {
            return false;
        }
        if (match.Groups["scheme"].Value.Equals("http", StringComparison.OrdinalIgnoreCase) && !isDevelopment)
        {
            return false;
        }
        var port = match.Groups["port"];
        return !port.Success || int.Parse(port.Value) is > 0 and <= HighestPort;
    }

    // Rechnernamen und IPv4 wie im Standard für Content-Security-Policy; IPv6 und Umlaute kennt er dort nicht.
    [GeneratedRegex(@"\A(?<scheme>https?)://[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*(:(?<port>[0-9]{1,5}))?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();
}

/// <summary>Was sich ändert, sobald fremde Seiten einbetten dürfen. Ohne Liste greift hier nichts.</summary>
public static class FrameEmbedding
{
    private const string PolicyHeader = "Content-Security-Policy";

    /// <summary>
    /// Liest die Liste und stellt das Antiforgery-System um: Es setzt sonst X-Frame-Options: SAMEORIGIN, und der Browser
    /// schickte sein Cookie im iframe einer fremden Seite nicht mit.
    /// </summary>
    public static FrameAncestors AddFrameEmbedding(this WebApplicationBuilder builder)
    {
        var ancestors = FrameAncestors.Parse(builder.Configuration[FrameAncestors.SettingKey], builder.Environment.IsDevelopment());
        builder.Services.AddSingleton(ancestors);
        if (ancestors.AllowsForeignSites)
        {
            builder.Services.Configure<AntiforgeryOptions>(options =>
            {
                options.SuppressXFrameOptionsHeader = true;
                options.Cookie = new AlwaysSecureCookie
                {
                    Name = options.Cookie.Name,
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.None,
                };
            });
        }
        return ancestors;
    }

    /// <summary>
    /// Ohne X-Frame-Options schützt nur noch frame-ancestors. Blazor setzt die Regel aber nur auf seine Seiten, deshalb
    /// steht sie hier auf jeder Antwort, auch auf Fehlerseiten und statischen Dateien. Muss vor der Fehlerbehandlung stehen.
    /// </summary>
    public static IApplicationBuilder UseFrameEmbedding(this IApplicationBuilder app, FrameAncestors ancestors)
    {
        if (!ancestors.AllowsForeignSites)
        {
            return app;
        }
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[PolicyHeader] = $"frame-ancestors {ancestors.Policy}";
                return Task.CompletedTask;
            });
            return next(context);
        });
    }

    /// <summary>
    /// SecurePolicy = Always wäre der kurze Weg, aber damit wirft das Antiforgery-System bei jeder Anfrage, die hier als
    /// http ankommt, also hinter einem Proxy, der die Verschlüsselung übernimmt und nicht als vertraut eingetragen ist.
    /// So bleibt das Cookie immer Secure, und die Seite antwortet trotzdem (wie beim Sitzungs-Cookie).
    /// </summary>
    private sealed class AlwaysSecureCookie : CookieBuilder
    {
        public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
        {
            var options = base.Build(context, expiresFrom);
            options.Secure = true;
            return options;
        }
    }
}
