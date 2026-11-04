using Duofy.ApiClient;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Meldet die API-Clients beim Dependency-Injection-Container an.
/// </summary>
public static class DuofyApiClientServiceCollectionExtensions
{
    /// <summary>Der Name des HttpClient, über den alle Aufrufe an die API gehen.</summary>
    public const string HttpClientName = "Duofy.Api";

    /// <summary>
    /// Registriert <see cref="IAuthApi"/>, <see cref="IUserApi"/>, <see cref="IBookingApi"/>,
    /// <see cref="ICategoryApi"/>, <see cref="IAccountApi"/>, <see cref="ICommitmentApi"/>, <see cref="IPlanApi"/>, <see cref="IHouseholdApi"/>, <see cref="ILegalApi"/>, <see cref="IInstanceApi"/>, <see cref="IAdminApi"/> und den
    /// Anmeldezustand (<see cref="AuthenticationStateProvider"/>), der aus dem
    /// <see cref="ITokenStore"/> abgeleitet wird.
    ///
    /// Der Host muss selbst noch einen <see cref="ITokenStore"/> registrieren und,
    /// für die Seiten, <c>AddCascadingAuthenticationState()</c> aufrufen.
    /// </summary>
    /// <param name="baseAddress">
    /// Die Adresse der API ohne /api/v1, zum Beispiel https://localhost:7080/. In der App ist das der
    /// gehostete Dienst; die gewählte Adresse steht im <see cref="IServerStore"/>.
    /// </param>
    public static IServiceCollection AddDuofyApiClient(this IServiceCollection services, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        services.AddHttpClient(HttpClientName);
        services.AddAuthorizationCore();

        // Ohne Serverwahl (Web) bleibt es bei dieser Adresse, und nichts kann sie ändern. Nur die App
        // ruft AddDuofyServerChoice auf und bekommt damit die Wahl des Servers.
        services.TryAddSingleton<IServerStore>(new ServerStore(baseAddress, new InMemoryServerPreference()));
        services.AddScoped<TokenSession>();
        services.AddScoped<ApiConnection>();
        services.AddScoped<TokenAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        services.AddScoped<IAuthApi, AuthApi>();
        services.AddScoped<IUserApi, UserApi>();
        services.AddScoped<IBookingApi, BookingApi>();
        services.AddScoped<ICategoryApi, CategoryApi>();
        services.AddScoped<IAccountApi, AccountApi>();
        services.AddScoped<ICommitmentApi, CommitmentApi>();
        services.AddScoped<ISavingsGoalApi, SavingsGoalApi>();
        services.AddScoped<IDebtApi, DebtApi>();
        services.AddScoped<IPlanApi, PlanApi>();
        services.AddScoped<IHouseholdApi, HouseholdApi>();
        services.AddScoped<ILegalApi, LegalApi>();
        services.AddScoped<IInstanceApi, InstanceApi>();
        services.AddScoped<IAdminApi, AdminApi>();
        services.AddScoped<IReminderApi, ReminderApi>();

        return services;
    }

    /// <summary>
    /// Schaltet die Wahl des Servers ein (nur die App): Der <paramref name="store"/> ersetzt den
    /// festen Speicher von <see cref="AddDuofyApiClient"/>, und der <see cref="IServerSelector"/>
    /// kann ihn ändern. Der Web-Host ruft das nicht auf, weil dort ein Speicher für alle
    /// Besucher gelten würde und ein Besucher den Server der anderen ändern könnte.
    /// </summary>
    /// <param name="store">Der Speicher der Wahl, in der App mit den Preferences des Telefons.</param>
    /// <param name="options">Ob http erlaubt ist (Debug) und die Zeitgrenze der Prüfung.</param>
    public static IServiceCollection AddDuofyServerChoice(
        this IServiceCollection services, IServerStore store, ServerSelectorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        services.Replace(ServiceDescriptor.Singleton(store));
        services.Replace(ServiceDescriptor.Singleton(options ?? new ServerSelectorOptions()));
        services.AddScoped<IServerSelector, ServerSelector>();
        return services;
    }
}
