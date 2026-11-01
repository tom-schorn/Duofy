using System.Reflection;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Spec 68, R6, R7: die Seite /haushalt/freigaben. Angemeldet ist Anna, Ben ist Mitglied ihres Haushalts.</summary>
public class GrantsPageTests : PageTestContext
{
    private static readonly Guid Ben = FakeHouseholdApi.BenId;

    private void Household(GrantLevels annaGivesBen) =>
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben() with { MyGrants = annaGivesBen }]);

    private IRenderedComponent<Grants> OpenPage()
    {
        var cut = Render<Grants>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='grants-card-{Ben}']")));
        return cut;
    }

    private static string Level(IRenderedComponent<Grants> cut, string area) =>
        cut.Find($"[data-testid='grants-{Ben}-{area}']").GetAttribute("value") ?? "";

    [Fact]
    public void The_page_is_reachable_under_household_and_needs_a_sign_in()
    {
        Assert.Equal("/haushalt/freigaben", typeof(Grants).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(typeof(Grants).GetCustomAttribute<AuthorizeAttribute>());
    }

    // A11
    [Fact]
    public void Choosing_mitlesen_sends_all_five_levels_and_changing_one_area_sends_only_that_one()
    {
        Household(GrantLevels.None);
        var cut = OpenPage();

        Assert.Equal(["Partner: alles", "Mitlesen", "Kind", "Nichts"],
            cut.FindAll($"[data-testid^='grants-{Ben}-preset-']").Select(b => b.TextContent.Trim()));
        Assert.Equal("true", cut.Find($"[data-testid='grants-{Ben}-preset-none']").GetAttribute("aria-pressed"));

        cut.Find($"[data-testid='grants-{Ben}-preset-read']").Click();

        cut.WaitForAssertion(() => Assert.All(GrantAreas.All, area => Assert.Equal(AccessLevelKeys.View, Level(cut, area))));
        Assert.Equal((Ben, new UpdateGrantsRequest(AccessLevelKeys.View, AccessLevelKeys.View, AccessLevelKeys.View, AccessLevelKeys.View, AccessLevelKeys.View)),
            Assert.Single(HouseholdApiFake.GrantsSet));
        Assert.Equal("true", cut.Find($"[data-testid='grants-{Ben}-preset-read']").GetAttribute("aria-pressed"));
        Assert.Empty(cut.FindAll($"[data-testid='grants-{Ben}-own-choice']"));

        cut.Find($"[data-testid='grants-{Ben}-{GrantAreas.Commitments}']").Change(AccessLevelKeys.None);

        cut.WaitForAssertion(() => Assert.Equal(2, HouseholdApiFake.GrantsSet.Count));
        Assert.Equal(new UpdateGrantsRequest(Commitments: AccessLevelKeys.None), HouseholdApiFake.GrantsSet[1].Request);
        cut.WaitForAssertion(() => Assert.Equal("eigene Auswahl", cut.Find($"[data-testid='grants-{Ben}-own-choice']").TextContent.Trim()));
        Assert.All(cut.FindAll($"[data-testid^='grants-{Ben}-preset-']"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    // A12
    [Fact]
    public void Booking_without_seeing_the_accounts_shows_a_hint_and_raises_nothing()
    {
        Household(GrantLevels.None with { Plan = AccessLevelKeys.View, Book = AccessLevelKeys.Create });

        var cut = OpenPage();

        var hints = cut.FindAll($"[data-testid='grants-{Ben}-hint']").Select(h => h.QuerySelector(".rz-alert-message")!.TextContent.Trim()).ToList();
        Assert.Equal(["Buchen bei dir geht nur, wenn Ben auch deine Konten ansehen darf."], hints);
        Assert.Equal(AccessLevelKeys.None, Level(cut, GrantAreas.Accounts));
        Assert.Empty(HouseholdApiFake.GrantsSet);
    }

    // A16: Buchungen ansehen geht auch ohne den Plan (wie im Server des Originals); der Hinweis sagt nur,
    // dass der Plan dann verborgen bleibt, und behauptet nicht, die Buchungen seien es.
    [Fact]
    public void Seeing_the_book_without_the_plan_shows_the_second_hint()
    {
        Household(GrantLevels.None with { Book = AccessLevelKeys.View });

        var cut = OpenPage();

        var hints = cut.FindAll($"[data-testid='grants-{Ben}-hint']").Select(h => h.QuerySelector(".rz-alert-message")!.TextContent.Trim()).ToList();
        Assert.Equal(["Ben sieht deine Buchungen, aber ohne Planung: Ansehen nicht deinen Plan."], hints);
        Assert.DoesNotContain(hints, hint => hint.Contains("Buch nicht"));
    }

    // R6: Während gespeichert wird, ist die Karte gesperrt; ein zweiter Klick schickt nichts.
    [Fact]
    public void The_card_locks_while_saving()
    {
        Household(GrantLevels.None);
        HouseholdApiFake.GrantsGate = new TaskCompletionSource();
        var cut = OpenPage();

        cut.Find($"[data-testid='grants-{Ben}-preset-partner']").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find($"[data-testid='grants-{Ben}-preset-read']").GetAttribute("aria-disabled")));
        Assert.Equal("true", cut.Find($"[data-testid='grants-{Ben}-{GrantAreas.Plan}']").GetAttribute("aria-disabled"));
        cut.Find($"[data-testid='grants-{Ben}-preset-read']").Click();
        Assert.Single(HouseholdApiFake.GrantsSet);

        HouseholdApiFake.GrantsGate.SetResult();

        cut.WaitForAssertion(() => Assert.Equal(AccessLevelKeys.Delete, Level(cut, GrantAreas.Book)));
        Assert.Null(cut.Find($"[data-testid='grants-{Ben}-preset-read']").GetAttribute("aria-disabled"));
    }

    // Das Auswahlfeld eines Bereichs. Der Browser zeigt nach einer Wahl die gewählte Stufe, bis Blazor das Feld
    // neu schreibt; bleibt die gespeicherte Stufe gleich, schreibt Blazor es nur, wenn es das Feld neu anlegt.
    private static NativeSelect AreaSelect(IRenderedComponent<Grants> cut, string area) =>
        cut.FindComponents<NativeSelect>().Single(s => s.Instance.Label == AreaLabels[area]).Instance;

    private static readonly IReadOnlyDictionary<string, string> AreaLabels = new Dictionary<string, string>
    {
        [GrantAreas.Plan] = "Planung",
        [GrantAreas.Book] = "Buchungen",
        [GrantAreas.Accounts] = "Konten",
        [GrantAreas.Commitments] = "Verträge",
        [GrantAreas.Savings] = "Sparen und Schulden",
    };

    // A20: Scheitert das Speichern, legt die Seite das Feld neu an, damit es wieder die gespeicherte Stufe zeigt.
    [Fact]
    public void After_a_failed_save_the_select_shows_the_stored_level_again()
    {
        Household(GrantLevels.None);
        HouseholdApiFake.Error = ErrorCodes.MemberNotFound;
        var cut = OpenPage();
        var before = AreaSelect(cut, GrantAreas.Plan);

        cut.Find($"[data-testid='grants-{Ben}-{GrantAreas.Plan}']").Change(AccessLevelKeys.View);

        cut.WaitForAssertion(() => Assert.NotSame(before, AreaSelect(cut, GrantAreas.Plan)));
        Assert.Equal(AccessLevelKeys.None, Level(cut, GrantAreas.Plan));
    }

    // A20: Eine Wahl, während die Karte gesperrt ist, schickt nichts und zeigt wieder die gespeicherte Stufe.
    [Fact]
    public void A_choice_while_the_card_is_locked_shows_the_stored_level_again()
    {
        Household(GrantLevels.None);
        HouseholdApiFake.GrantsGate = new TaskCompletionSource();
        var cut = OpenPage();
        cut.Find($"[data-testid='grants-{Ben}-{GrantAreas.Plan}']").Change(AccessLevelKeys.View);
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find($"[data-testid='grants-{Ben}-{GrantAreas.Book}']").GetAttribute("aria-disabled")));
        var before = AreaSelect(cut, GrantAreas.Book);

        cut.Find($"[data-testid='grants-{Ben}-{GrantAreas.Book}']").Change(AccessLevelKeys.Delete);

        cut.WaitForAssertion(() => Assert.NotSame(before, AreaSelect(cut, GrantAreas.Book)));
        Assert.Equal(AccessLevelKeys.None, Level(cut, GrantAreas.Book));
        Assert.Single(HouseholdApiFake.GrantsSet);
        HouseholdApiFake.GrantsGate.SetResult();
    }

    // R6: Die Vorlagen Kind und Partner.
    [Theory]
    [InlineData("kid", AccessLevelKeys.View, AccessLevelKeys.None)]
    [InlineData("partner", AccessLevelKeys.Delete, AccessLevelKeys.Delete)]
    public void The_presets_set_their_levels(string preset, string plan, string others)
    {
        Household(GrantLevels.None with { Commitments = AccessLevelKeys.Edit });
        var cut = OpenPage();

        cut.Find($"[data-testid='grants-{Ben}-preset-{preset}']").Click();

        Assert.Equal(new UpdateGrantsRequest(plan, others, others, others, others), Assert.Single(HouseholdApiFake.GrantsSet).Request);
    }

    [Fact]
    public void Alone_in_the_household_the_page_explains_that_there_is_nobody_to_grant()
    {
        var cut = Render<Grants>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='grants-empty']")));
    }

    [Fact]
    public void The_household_page_leads_to_the_grants()
    {
        Household(GrantLevels.None);

        var cut = Render<Household>();

        cut.WaitForAssertion(() => Assert.Equal("/haushalt/freigaben", cut.Find("[data-testid='household-grants']").GetAttribute("href")));
    }

    // Radzen-Umstellung R21: Die Seite ist in Radzen gebaut, die Voreinstellungen stehen ohne eigenes CSS nebeneinander.
    [Fact]
    public void The_page_is_built_with_radzen_with_one_card_per_person_and_a_row_of_preset_buttons()
    {
        Household(GrantLevels.None);
        var cut = OpenPage();

        Assert.Contains("rz-card", cut.Find($"[data-testid='grants-card-{Ben}']").ClassList);
        Assert.Equal($"grants-heading-{Ben}", cut.Find($"[data-testid='grants-card-{Ben}']").GetAttribute("aria-labelledby"));
        Assert.All(cut.FindAll($"[data-testid^='grants-{Ben}-preset-']"), b => Assert.Contains("rz-button", b.ClassList));
        Assert.Contains("rz-primary", cut.Find($"[data-testid='grants-{Ben}-preset-none']").ClassList);
        Assert.Equal("/haushalt", cut.Find("a[data-testid='grants-back']").GetAttribute("href"));
        Assert.Equal(GrantAreas.All.Count, cut.FindAll($"select[data-testid^='grants-{Ben}-']").Count);
    }

    [Fact]
    public void An_own_choice_shows_as_a_badge_and_a_hint_as_a_radzen_alert()
    {
        Household(GrantLevels.None with { Book = AccessLevelKeys.View });
        var cut = OpenPage();

        Assert.Contains("rz-badge", cut.Find($"[data-testid='grants-{Ben}-own-choice']").ClassList);
        Assert.Contains("rz-alert", cut.Find($"[data-testid='grants-{Ben}-hint']").ClassList);
    }
}
