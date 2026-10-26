using System.Text.RegularExpressions;

namespace Duofy.Shared.Services;

/// <summary>
/// Das Token aus einem eingefügten Einladungslink lesen. Den Link selbst baut die API mit der öffentlichen
/// Adresse des Servers (Duofy:PublicUrl) und schickt ihn mit der Einladung.
/// </summary>
public static partial class InvitationLink
{
    /// <summary>
    /// Das Token aus einem eingefügten Link, der auf <c>/einladung/&lt;Token&gt;</c> endet; null für alles andere.
    /// Der Server davor spielt keine Rolle, die App fragt immer ihren eigenen.
    /// </summary>
    public static string? TokenOf(string? link)
    {
        var match = LinkPattern().Match((link ?? "").Trim());
        return match.Success ? match.Groups["token"].Value : null;
    }

    [GeneratedRegex(@"(?:^|/)einladung/(?<token>[A-Za-z0-9_-]+)/?$")]
    private static partial Regex LinkPattern();
}
