using AngleSharp.Dom;
using Bunit;
using Duofy.ApiClient;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Unterbau für Seitentests: Die API-Clients und die Token-Ablage sind Fakes,
/// und es ist eine Person namens "anna" angemeldet.
/// </summary>
public abstract class PageTestContext : UiTestContext
{
    protected FakeTokenStore Tokens { get; } = new() { Tokens = new AuthTokens("access", "refresh") };
    protected FakeAuthApi Auth { get; }
    protected FakeUserApi Users { get; }
    protected FakeBookingApi BookingApiFake { get; } = new();
    protected FakeCategoryApi Categories { get; } = new();
    protected FakeAccountChoices Accounts { get; } = new();
    protected FakeAccountApi AccountApiFake { get; } = new();
    protected FakeCommitmentApi CommitmentApiFake { get; } = new();
    protected FakeSavingsGoalApi SavingsGoalApiFake { get; } = new();
    protected FakeDebtApi DebtApiFake { get; } = new();
    protected FakePlanApi PlanApiFake { get; } = new();
    protected FakeHouseholdApi HouseholdApiFake { get; } = new();
    protected FakeShareService ShareFake { get; } = new();
    protected FakeLegalApi LegalApiFake { get; } = new();
    protected FakeInstanceApi InstanceApiFake { get; } = new();
    protected FakeAdminApi AdminApiFake { get; } = new();

    protected PageTestContext()
    {
        Auth = new FakeAuthApi(Tokens);
        Users = new FakeUserApi(Tokens);
        Services.AddSingleton<ITokenStore>(Tokens);
        Services.AddSingleton<IAuthApi>(Auth);
        Services.AddSingleton<IUserApi>(Users);
        Services.AddSingleton<IBookingApi>(BookingApiFake);
        Services.AddSingleton<ICategoryApi>(Categories);
        Services.AddSingleton<IAccountChoices>(Accounts);
        Services.AddSingleton<IAccountApi>(AccountApiFake);
        Services.AddSingleton<ICommitmentApi>(CommitmentApiFake);
        Services.AddSingleton<ISavingsGoalApi>(SavingsGoalApiFake);
        Services.AddSingleton<IDebtApi>(DebtApiFake);
        Services.AddSingleton<IPlanApi>(PlanApiFake);
        Services.AddSingleton<IHouseholdApi>(HouseholdApiFake);
        Services.AddSingleton<IShareService>(ShareFake);
        Services.AddSingleton<ILegalApi>(LegalApiFake);
        Services.AddSingleton<IInstanceApi>(InstanceApiFake);
        Services.AddSingleton<IAdminApi>(AdminApiFake);
        // Läuft ein Test als App, zeigt das Layout die Serverzeile und braucht dafür einen Speicher.
        Services.AddSingleton<IServerStore>(new ServerStore(new Uri("https://api.duofy.invalid/"), new InMemoryServerPreference()));
        // Heute ist der 21. Oktober 2026.
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        AddAuthorization().SetAuthorized("anna");
    }

    /// <summary>
    /// Die Monatsseite öffnet mit der Übersicht (Spec 24); die Tests des Plans (Posten, Summen, Übertrag) wechseln
    /// zuerst zum Tab Plan und warten, bis er da ist.
    /// </summary>
    protected static void OpenPlanTab(IRenderedComponent<IComponent> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-plan']")));
        cut.Find("[data-testid='month-tab-plan']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-sums']")));
    }

    protected NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>Die Adresse ohne Rechnername, zum Beispiel "/anmelden".</summary>
    protected string CurrentPath => new Uri(Navigation.Uri).PathAndQuery;

    /// <summary>
    /// Findet ein Eingabefeld über sein data-testid. MudBlazor setzt das Attribut je nach
    /// Komponente auf das Feld selbst oder auf den Rahmen darum; wir suchen in beiden.
    /// </summary>
    protected static IElement Field(IRenderedComponent<IComponent> cut, string testId)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        return element.TagName is "INPUT" or "TEXTAREA" or "BUTTON" ? element : element.QuerySelector("input,textarea,button")!;
    }
}
