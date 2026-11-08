namespace Duofy.Shared.Services;

/// <summary>
/// Die Verbindung zwischen der Registrierung und der Zeile mit den Rechtstexten (Spec 109, Entscheidung 3): In der App darf
/// ein Link die Registrierung nicht verlassen. Die Seite meldet hier an, dass sie einen eigenen Text als Fenster über dem
/// Formular zeigen kann; die Zeile im Rahmen ruft sie dann auf, statt zur Seite des Servers zu gehen.
/// </summary>
public sealed class LegalOverlay
{
    private Func<string, Task>? opener;

    /// <summary>Ändert sich, wenn eine Seite sich anmeldet oder abmeldet; die Zeile zeichnet sich dann neu.</summary>
    public event Action? Changed;

    /// <summary>Ob gerade eine Seite den Text als Fenster zeigt.</summary>
    public bool IsAvailable => opener is not null;

    public void Register(Func<string, Task> open)
    {
        opener = open;
        Changed?.Invoke();
    }

    /// <summary>Meldet die Seite ab, aber nur, wenn noch sie es ist, die angemeldet ist.</summary>
    public void Unregister(Func<string, Task> open)
    {
        if (opener == open)
        {
            opener = null;
            Changed?.Invoke();
        }
    }

    /// <summary>Zeigt den Text zu einem Namen der API als Fenster.</summary>
    public Task OpenAsync(string name) => opener?.Invoke(name) ?? Task.CompletedTask;
}
