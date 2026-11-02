using System.Security.Cryptography;
using Duofy.Web.Services;

namespace Duofy.Web.Session;

/// <summary>
/// Das einzige, was der Browser von der Anmeldung zu sehen bekommt: ein HttpOnly-Cookie
/// mit einer zufälligen Kennung der Sitzung. Die Tokens selbst bleiben auf dem
/// Web-Server, so kann kein Skript im Browser sie lesen ("Backend for Frontend").
///
/// Jeder Besucher bekommt das Cookie schon beim ersten Aufruf, auch ohne Anmeldung.
/// Der Grund: Die Anmeldeseite läuft interaktiv über die dauerhafte Verbindung zum
/// Server, und über diese Verbindung lässt sich kein Cookie mehr setzen.
/// </summary>
public static class SessionCookie
{
    /// <summary>
    /// Der Name außerhalb der Entwicklung. Das Präfix "__Host-" verlangt vom Browser,
    /// dass das Cookie nur über HTTPS kommt und nur zu genau diesem Rechnernamen gehört;
    /// eine Subdomain kann es so nicht unterschieben.
    /// </summary>
    public const string Name = "__Host-Duofy.Session";

    /// <summary>
    /// Der Name in der Entwicklung, wo die Web-Variante auch über einfaches HTTP läuft
    /// und das Präfix "__Host-" deshalb nicht möglich ist.
    /// </summary>
    public const string DevelopmentName = "Duofy.Session";

    /// <summary>Unter diesem Claim steht die Kennung der Sitzung im ClaimsPrincipal der Anfrage.</summary>
    public const string ClaimType = "duofy:session";

    private const string ItemKey = "Duofy.SessionId";

    // Hierüber baut Blazor die dauerhafte Verbindung der interaktiven Seiten auf.
    private const string InteractiveConnectionPath = "/_blazor";

    // 32 Zufallsbytes ergeben in Base64Url 43 Zeichen.
    private const int IdLength = 43;

    public static string NewId() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Liefert die Kennung der Sitzung dieser Anfrage oder null. Werte, die nicht
    /// wie eine von uns erzeugte Kennung aussehen, werden ignoriert.
    /// </summary>
    public static string? Read(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var fresh) && fresh is string id)
        {
            return id;
        }
        var value = context.Request.Cookies[NameFor(context)];
        return IsWellFormed(value) ? value : null;
    }

    private static bool IsDevelopment(HttpContext context) =>
        context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();

    private static string NameFor(HttpContext context) => IsDevelopment(context) ? DevelopmentName : Name;

    /// <summary>
    /// Setzt das Cookie, wenn der Browser noch keines mitgeschickt hat. Die Verbindung
    /// der interaktiven Seiten bekommt keines: Kommt sie ohne Cookie, hat der Browser
    /// das Cookie der Seite nicht angenommen, und eine neue Kennung nur für diese
    /// Verbindung fände er nie wieder. Ohne Kennung bittet die Anmeldung um Cookies.
    /// </summary>
    public static IApplicationBuilder UseWebSessionCookie(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (Read(context) is null && !context.Request.Path.StartsWithSegments(InteractiveConnectionPath))
        {
            var id = NewId();
            // Diese Anfrage soll die neue Kennung schon kennen, nicht erst die nächste.
            context.Items[ItemKey] = id;
            // Im iframe einer fremden Seite schickt der Browser ein Lax-Cookie nicht mit. None verlangt immer Secure.
            var embeddable = context.RequestServices.GetRequiredService<FrameAncestors>().AllowsForeignSites;
            context.Response.Cookies.Append(NameFor(context), id, new CookieOptions
            {
                HttpOnly = true,
                SameSite = embeddable ? SameSiteMode.None : SameSiteMode.Lax,
                // Außerhalb der Entwicklung immer nur über HTTPS, auch wenn ein Proxy
                // davor die Verschlüsselung übernimmt und hier HTTP ankommt.
                Secure = embeddable || !IsDevelopment(context) || context.Request.IsHttps,
                IsEssential = true,
                Path = "/",
                // So lange wie ein Refresh-Token gilt.
                MaxAge = TimeSpan.FromDays(30),
            });
        }
        await next(context);
    });

    private static bool IsWellFormed(string? value) =>
        value is { Length: IdLength } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
