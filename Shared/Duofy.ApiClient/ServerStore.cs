using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Der Ort, an dem ein Host die gewählte Serveradresse über das Schließen hinaus aufbewahrt.
/// Die App legt sie in die Preferences des Telefons; die Tokens bleiben davon getrennt im
/// SecureStorage.
/// </summary>
public interface IServerPreference
{
    /// <summary>Die gespeicherte Adresse oder null, wenn der gehostete Dienst gewählt ist.</summary>
    string? Load();

    /// <summary>Speichert die Adresse; null löscht die Wahl.</summary>
    void Save(string? address);
}

/// <summary>Eine Ablage im Speicher: für Hosts ohne Serverwahl (Web) und für Tests.</summary>
public sealed class InMemoryServerPreference(string? initial = null) : IServerPreference
{
    private string? value = initial;

    public string? Load() => value;

    public void Save(string? address) => value = address;
}

/// <summary>
/// Die Adresse der API, an die alle Anfragen gehen. Sie ist zur Laufzeit änderbar: Jede
/// Anfrage fragt hier nach der aktuellen Adresse, auch der Tausch des Refresh-Tokens.
/// </summary>
public interface IServerStore
{
    /// <summary>Die Adresse des gehosteten Dienstes aus der Konfiguration (<c>Api:BaseAddress</c>).</summary>
    Uri HostedAddress { get; }

    /// <summary>Die Adresse, an die gerade alle Anfragen gehen; endet immer mit einem Schrägstrich.</summary>
    Uri CurrentAddress { get; }

    /// <summary>Ob der gehostete Dienst gewählt ist.</summary>
    bool IsHosted { get; }

    /// <summary>
    /// Die Anmeldewege, die der gewählte Server anbietet (siehe <see cref="InstanceResponse"/>).
    /// Solange nichts anderes bekannt ist: nur E-Mail und Passwort.
    /// </summary>
    IReadOnlyList<string> SignInMethods { get; set; }

    /// <summary>Wird gerufen, wenn sich die Wahl ändert.</summary>
    event Action? Changed;

    /// <summary>Nimmt diese Adresse und merkt sie sich. Prüft nichts; das macht <see cref="IServerSelector"/>.</summary>
    void Set(Uri address);

    /// <summary>Geht zurück zum gehosteten Dienst und vergisst die Wahl.</summary>
    void Reset();
}

/// <summary>Bringt Adressen in die Form, in der sie gespeichert und benutzt werden.</summary>
public static class ServerAddress
{
    /// <summary>
    /// Hängt den Schrägstrich am Ende an und lässt Parameter und Anker weg. Ohne Schrägstrich
    /// würde HttpClient den letzten Teil eines Pfades (etwa /duofy) beim Zusammensetzen abschneiden.
    /// </summary>
    public static Uri Normalize(Uri address)
    {
        var builder = new UriBuilder(address) { Query = "", Fragment = "" };
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }
        return builder.Uri;
    }
}

/// <summary>
/// Der Standard-Speicher: Der gehostete Dienst gilt, bis jemand einen eigenen Server nimmt;
/// diese Wahl steht in der <see cref="IServerPreference"/>.
/// </summary>
public sealed class ServerStore : IServerStore
{
    private static readonly string[] DefaultMethods = [SignInMethodNames.Password];

    private readonly IServerPreference preference;
    private readonly object gate = new();
    private Uri current;
    private IReadOnlyList<string> signInMethods = DefaultMethods;

    public ServerStore(Uri hostedAddress, IServerPreference preference)
    {
        ArgumentNullException.ThrowIfNull(hostedAddress);
        ArgumentNullException.ThrowIfNull(preference);
        HostedAddress = ServerAddress.Normalize(hostedAddress);
        this.preference = preference;

        // Eine unlesbare gespeicherte Adresse wäre ein Absturz beim Start; dann gilt der gehostete Dienst.
        current = Uri.TryCreate(preference.Load(), UriKind.Absolute, out var stored)
            && stored.Scheme is "http" or "https"
                ? ServerAddress.Normalize(stored)
                : HostedAddress;
    }

    public Uri HostedAddress { get; }

    public Uri CurrentAddress
    {
        get { lock (gate) { return current; } }
    }

    public bool IsHosted => CurrentAddress == HostedAddress;

    public IReadOnlyList<string> SignInMethods
    {
        get { lock (gate) { return signInMethods; } }
        set { lock (gate) { signInMethods = value; } }
    }

    public event Action? Changed;

    public void Set(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var normalized = ServerAddress.Normalize(address);
        lock (gate)
        {
            current = normalized;
            // Der gehostete Dienst wird nicht als Adresse gespeichert, damit eine spätere Änderung
            // in der Konfiguration bei allen ankommt, die ihn gewählt haben.
            preference.Save(normalized == HostedAddress ? null : normalized.AbsoluteUri);
        }
        Changed?.Invoke();
    }

    public void Reset()
    {
        lock (gate)
        {
            current = HostedAddress;
            preference.Save(null);
        }
        Changed?.Invoke();
    }
}
