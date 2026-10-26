namespace Duofy.Shared.Services;

/// <summary>
/// Die Verbindung zwischen einer Seite und dem App-Layout: Die Seite sagt, wie ihr Titel in der
/// oberen Leiste heißt und was der Plus-Knopf tut; das Layout zeigt beides an. Im Browser
/// zeichnet niemand den Dienst, er hat dort keine sichtbare Wirkung.
///
/// Jede Seite gibt sich selbst als Besitzer an. So räumt eine alte Seite, die erst nach der
/// neuen weggeräumt wird, nicht versehentlich den Titel der neuen ab.
/// </summary>
public sealed class AppChrome
{
    private object? owner;

    /// <summary>Wird ausgelöst, wenn sich Titel oder Plus-Knopf geändert haben.</summary>
    public event Action? Changed;

    public string? Title { get; private set; }

    /// <summary>Die Aktion des Plus-Knopfs; null heißt: kein Knopf.</summary>
    public Func<Task>? PlusAction { get; private set; }

    public bool PlusDisabled { get; private set; }

    public string? PlusLabel { get; private set; }

    /// <summary>
    /// Gesetzt, ist der Plus-Knopf ausgegraut, weil die Freigabe zum Anlegen fehlt (Spec 68); der Wert ist die
    /// Kennung des Elements auf der Seite, das den Grund nennt.
    /// </summary>
    public string? PlusLockedBy { get; private set; }

    public void Set(object page, string title, Func<Task>? plusAction = null, string? plusLabel = null, bool plusDisabled = false)
    {
        owner = page;
        Title = title;
        PlusAction = plusAction;
        PlusLabel = plusLabel;
        PlusDisabled = plusDisabled;
        PlusLockedBy = null;
        Changed?.Invoke();
    }

    /// <summary>Graut den Plus-Knopf der Seite aus und nennt das Element mit dem Grund; null gibt ihn wieder frei.</summary>
    public void LockPlus(object page, string? reasonId)
    {
        if (!ReferenceEquals(owner, page) || PlusLockedBy == reasonId) return;

        PlusLockedBy = reasonId;
        Changed?.Invoke();
    }

    /// <summary>Räumt Titel und Plus-Knopf ab, aber nur, wenn sie noch dieser Seite gehören.</summary>
    public void Release(object page)
    {
        if (!ReferenceEquals(owner, page)) return;

        owner = null;
        Title = null;
        PlusAction = null;
        PlusLabel = null;
        PlusDisabled = false;
        PlusLockedBy = null;
        Changed?.Invoke();
    }
}
