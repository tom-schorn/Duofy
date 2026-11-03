namespace Duofy.Shared.Services;

/// <summary>
/// Wie ein Knopf aussieht, den die Freigabe nicht erlaubt (Spec 68, Nachtrag): ausgegraut und ohne Wirkung, aber
/// erreichbar. aria-disabled statt disabled, damit ein Screenreader ihn findet und den Grund vorliest, der mit
/// <see cref="ReasonId"/> auf der Seite steht ("Dafür hat dir Anna keine Freigabe gegeben.").
/// </summary>
public static class GrantLock
{
    /// <summary>Die Kennung des Elements mit dem Grund; eine Seite zeigt ihn einmal.</summary>
    public const string ReasonId = "grant-reason";

    // Ausdrücklich leer statt gar nicht: Bekommt ein Baustein keine weiteren Attribute mehr, behält Blazor die alten, und ein
    // Knopf bliebe nach dem Wechsel zurück auf "Ich" gesperrt. Null lässt das Attribut wegfallen.
    private static readonly IReadOnlyDictionary<string, object> None = new Dictionary<string, object>
    {
        ["aria-disabled"] = null!,
        ["aria-describedby"] = null!,
    };

    private static readonly IReadOnlyDictionary<string, object> Locked = new Dictionary<string, object>
    {
        ["aria-disabled"] = "true",
        ["aria-describedby"] = ReasonId,
    };

    /// <summary>Die zusätzlichen Attribute eines Knopfs: keine, wenn er gehen darf.</summary>
    public static IReadOnlyDictionary<string, object> Attributes(bool allowed) => allowed ? None : Locked;

    /// <summary>Die CSS-Klasse, die ihn ausgraut; leer, wenn er gehen darf.</summary>
    public static string Class(bool allowed) => allowed ? "" : "grant-locked";
}
