namespace Duofy.Shared.Services;

/// <summary>
/// Die Verbindung zwischen einer Seite und dem Rahmen: Die Seite sagt, wie ihr Titel in der oberen Leiste heißt;
/// der Rahmen zeigt ihn an.
///
/// Jede Seite gibt sich selbst als Besitzer an. So räumt eine alte Seite, die erst nach der
/// neuen weggeräumt wird, nicht versehentlich den Titel der neuen ab.
/// </summary>
public sealed class AppChrome
{
    private object? owner;

    /// <summary>Wird ausgelöst, wenn sich der Titel geändert hat.</summary>
    public event Action? Changed;

    public string? Title { get; private set; }

    /// <summary>
    /// Das Thema der Erklärspalte, wenn die Seite es selbst bestimmt, zum Beispiel der Monat je Reiter; sonst folgt es der
    /// Adresse (<see cref="HelpTopics.ForPath"/>).
    /// </summary>
    public string? HelpTopic { get; private set; }

    /// <summary>Setzt das Thema der Erklärspalte für diese Seite.</summary>
    public void SetHelpTopic(object page, string? topic)
    {
        owner = page;
        HelpTopic = topic;
        Changed?.Invoke();
    }

    public void Set(object page, string title)
    {
        // Eine neue Seite erbt kein Thema der Erklärspalte von der vorigen.
        if (!ReferenceEquals(owner, page))
        {
            HelpTopic = null;
        }
        owner = page;
        Title = title;
        Changed?.Invoke();
    }

    /// <summary>Räumt den Titel ab, aber nur, wenn er noch dieser Seite gehört.</summary>
    public void Release(object page)
    {
        if (!ReferenceEquals(owner, page)) return;

        owner = null;
        Title = null;
        HelpTopic = null;
        Changed?.Invoke();
    }
}
