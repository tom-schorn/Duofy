using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 124: Der Buchungsdialog in Schritten mit klarer Auswahl. A1 Schritte und Zurück, A2 die gewählte Art, A3 Auswahlfelder mit
/// Suchfeld, A4 der Schritt Aufteilen. "Heute" ist der 21. Oktober 2026.
/// </summary>
public partial class BookingDialogStepsTests : PageTestContext
{
    private readonly IRenderedComponent<IComponent> dialogs;
    private readonly AccountChoice giro = new(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true);
    private readonly AccountChoice spar = new(Guid.NewGuid(), "Sparkonto", IsDefault: false, Active: true, Icon: AccountIcons.Piggy);

    public BookingDialogStepsTests()
    {
        dialogs = RenderDialogHost();
        Accounts.Accounts.Add(giro);
        Accounts.Accounts.Add(spar);
    }

    private void OpenDialog()
    {
        OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-line-position-0']")));
    }

    private string Progress() => dialogs.Find("[data-testid='step-dialog-progress']").TextContent.Trim();

    private void Next() => dialogs.Find("[data-testid='booking-next']").Click();

    private void Back() => dialogs.FindAll(".step-dialog-actions button").Single(b => b.TextContent.Trim() == "Zurück").Click();

    private static string CurrentStep(IRenderedComponent<IComponent> cut) =>
        cut.FindAll("[data-testid^='step-dialog-step-']").Single(s => !s.HasAttribute("hidden")).GetAttribute("data-testid")!;

    // A1
    [Fact]
    public void The_dialog_shows_step_one_of_four_with_back_and_next_in_the_foot()
    {
        OpenDialog();

        Assert.Equal("Schritt 1 von 4: Was und wann", Progress());
        Assert.Empty(dialogs.FindAll(".step-dialog-actions button").Where(b => b.TextContent.Trim() == "Zurück"));
        Assert.Equal("Weiter", dialogs.Find(".step-dialog-foot [data-testid='booking-next']").TextContent.Trim());
        Assert.Empty(dialogs.FindAll("[data-testid='booking-save']"));
        Next();
        Assert.Equal("Schritt 2 von 4: Konto", Progress());
        Next();
        Assert.Equal("Schritt 3 von 4: Kategorie und Posten", Progress());
        Next();
        Assert.Equal("Schritt 4 von 4: Prüfen und speichern", Progress());
        Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-save']"));
    }

    // A1: Zurück von Schritt 3 nach 1 behält alle Eingaben
    [Fact]
    public void Going_back_from_step_three_to_one_keeps_all_input()
    {
        var restaurant = FakeCategoryApi.Restaurant.Id.ToString();
        OpenDialog();
        Field(dialogs, "booking-amount").Change("42");
        dialogs.Find("[data-testid='booking-kind-income']").Click();
        dialogs.Find("[data-testid='booking-kind-expense']").Click();
        Next();
        BookingDialogSteps.Choose(dialogs, "booking-account", spar.Id.ToString());
        Next();
        BookingDialogSteps.Choose(dialogs, "booking-line-category-0", restaurant);
        Assert.Equal("Schritt 3 von 4: Kategorie und Posten", Progress());

        Back();
        Back();

        Assert.Equal("Schritt 1 von 4: Was und wann", Progress());
        Assert.Equal("42,00", Field(dialogs, "booking-amount").GetAttribute("value"));
        Assert.Equal("true", dialogs.Find("[data-testid='booking-kind-expense']").GetAttribute("aria-pressed"));
        Assert.Equal(spar.Id.ToString(), BookingDialogSteps.Chosen(dialogs, "booking-account"));
        Assert.Equal(restaurant, BookingDialogSteps.Chosen(dialogs, "booking-line-category-0"));
        Assert.Equal(PotKeys.Wants, dialogs.Find("[data-testid='booking-line-pot-0']").GetAttribute("value"));
    }

    // A2
    [Fact]
    public void Choosing_the_transfer_marks_only_that_kind_as_pressed()
    {
        OpenDialog();
        var kinds = dialogs.FindAll("[data-testid^='booking-kind-']");
        Assert.Equal(3, kinds.Count);
        Assert.All(kinds, k => Assert.Equal("button", k.TagName.ToLowerInvariant()));

        dialogs.Find("[data-testid='booking-kind-transfer']").Click();

        Assert.Equal("true", dialogs.Find("[data-testid='booking-kind-transfer']").GetAttribute("aria-pressed"));
        Assert.Equal("false", dialogs.Find("[data-testid='booking-kind-expense']").GetAttribute("aria-pressed"));
        Assert.Equal("false", dialogs.Find("[data-testid='booking-kind-income']").GetAttribute("aria-pressed"));
    }

    // A2: Die gewählte Art ist gefüllt, ihr Grund hat zu den anderen mindestens 3:1, in Hell und Dunkel (Stylesheet und Theme gelesen).
    [Theory]
    [InlineData(":root")]
    [InlineData(".dark")]
    public void The_selected_kind_has_a_contrast_of_at_least_three_to_the_others(string scope)
    {
        var theme = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "theme.css"));
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));
        var block = theme[(theme.IndexOf("\n" + scope + " {", StringComparison.Ordinal) + 1)..];
        block = block[..block.IndexOf('}', StringComparison.Ordinal)];
        string Token(string name) => Regex.Match(block, $@"--{name}:\s*(#[0-9a-fA-F]{{6}})").Groups[1].Value;

        var rule = Regex.Match(css, @"\.booking-kinds button\[aria-pressed=""true""\] \{([^}]*)\}").Groups[1].Value;
        var unselectedRule = Regex.Match(css, @"\.booking-kinds button \{([^}]*)\}").Groups[1].Value;
        Assert.Contains("background: var(--primary)", rule);
        Assert.Contains("background: var(--secondary)", unselectedRule);
        Assert.Contains("\\2713", css);

        Assert.True(Contrast(Token("primary"), Token("secondary")) >= 3, $"{scope}: {Token("primary")} gegen {Token("secondary")}");
        Assert.True(Contrast(Token("primary-foreground"), Token("primary")) >= 4.5, "Schrift auf dem gewählten Knopf");
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var c = int.Parse(hex.AsSpan(1 + offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }

    private static double Contrast(string a, string b)
    {
        var (high, low) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (high + 0.05) / (low + 0.05);
    }

    // A3
    [Fact]
    public void Account_category_and_position_are_combo_boxes_without_a_choose_button()
    {
        OpenDialog();

        foreach (var id in new[] { "booking-account", "booking-line-category-0", "booking-line-position-0" })
        {
            var input = dialogs.Find($"[data-testid='{id}-input']");
            Assert.Equal("combobox", input.GetAttribute("role"));
            Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
            Assert.Equal("listbox", dialogs.Find($"[data-testid='{id}'] ul").GetAttribute("role"));
        }
        Assert.DoesNotContain(dialogs.FindAll("button"), b => b.TextContent.Contains("bitte wählen", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(dialogs.FindAll("[data-testid='booking-account-none']"));
        Assert.Empty(dialogs.FindAll("fieldset[role='radiogroup']"));
    }

    [Fact]
    public void Typing_filters_the_list_and_choosing_an_option_fills_the_field()
    {
        OpenDialog();
        var input = dialogs.Find("[data-testid='booking-line-category-0-input']");

        input.Focus();
        Assert.Equal("true", dialogs.Find("[data-testid='booking-line-category-0-input']").GetAttribute("aria-expanded"));
        Assert.Equal(["Lebensmittel", "Körperpflege", "Essen gehen"], BookingDialogSteps.OptionLabels(dialogs, "booking-line-category-0"));

        dialogs.Find("[data-testid='booking-line-category-0-input']").Input("essen");

        Assert.Equal(["Essen gehen"], BookingDialogSteps.OptionLabels(dialogs, "booking-line-category-0"));
        BookingDialogSteps.Choose(dialogs, "booking-line-category-0", FakeCategoryApi.Restaurant.Id.ToString());
        var after = dialogs.Find("[data-testid='booking-line-category-0-input']");
        Assert.Equal("Essen gehen", after.GetAttribute("value"));
        Assert.Equal("false", after.GetAttribute("aria-expanded"));
        Assert.Equal("true", dialogs.Find($"[data-testid='booking-line-category-0-option-{FakeCategoryApi.Restaurant.Id}']").GetAttribute("aria-selected"));
    }

    [Fact]
    public void Nothing_found_says_so_and_the_arrow_keys_and_enter_choose()
    {
        OpenDialog();
        var input = dialogs.Find("[data-testid='booking-account-input']");

        input.Input("zzz");
        Assert.Contains("Kein Treffer", dialogs.Find("[data-testid='booking-account']").TextContent);

        dialogs.Find("[data-testid='booking-account-input']").Input("");
        dialogs.Find("[data-testid='booking-account-input']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        dialogs.Find("[data-testid='booking-account-input']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(spar.Id.ToString(), BookingDialogSteps.Chosen(dialogs, "booking-account"));
    }

    [Fact]
    public void Categories_are_grouped_by_pot()
    {
        OpenDialog();

        var headings = dialogs.FindAll("[data-testid='booking-line-category-0'] .combo-group").Select(g => g.TextContent.Trim()).ToList();

        Assert.Equal(["Grundbedarf", "Wünsche"], headings);
        var items = dialogs.Find("[data-testid='booking-line-category-0'] ul").Children.Select(c => c.TextContent.Trim()).ToList();
        Assert.Equal(["Grundbedarf", "Lebensmittel", "Körperpflege", "Wünsche", "Essen gehen"], items);
    }

    [Fact]
    public void Positions_show_name_due_day_and_planned_amount_grouped_by_due_day()
    {
        var rent = FakePlanApi.Position("Miete", 890m, day: 3);
        var gym = FakePlanApi.Position("Fitness", 29.9m, day: 15);
        var gez = FakePlanApi.Position("GEZ", 18.36m, day: 15, booked: 18.36m, done: true);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [gym, rent, gez] };

        OpenDialog();

        var list = dialogs.Find("[data-testid='booking-line-position-0'] ul");
        Assert.Equal(["Kein Posten", "Fällig am 3.", "Miete 3. · 890,00 €", "Fällig am 15.", "Fitness 15. · 29,90 €", "GEZ (abgehakt) 15. · 18,36 €"],
            list.Children.Select(c => Regex.Replace(c.TextContent.Trim(), @"\s+", " ")).ToList());
    }

    // A4
    [Fact]
    public void Splitting_adds_a_step_and_with_three_lines_they_are_edited_there()
    {
        OpenDialog();
        Assert.Equal("Schritt 1 von 4: Was und wann", Progress());
        Field(dialogs, "booking-amount").Change("90");
        Next();
        Next();

        dialogs.Find("[data-testid='booking-split']").Click();
        Assert.Equal("true", dialogs.Find("[data-testid='booking-split']").GetAttribute("aria-pressed"));
        dialogs.Find("[data-testid='booking-line-add']").Click();
        Assert.Equal("Schritt 3 von 5: Kategorie und Posten", Progress());
        Field(dialogs, "booking-line-amount-1").Change("20");
        Field(dialogs, "booking-line-amount-2").Change("10");
        Next();

        Assert.Equal("Schritt 4 von 5: Aufteilen", Progress());
        Assert.Equal("step-dialog-step-4", CurrentStep(dialogs));
        var split = dialogs.Find("[data-testid='step-dialog-step-4']");
        Assert.Equal(3, split.QuerySelectorAll(".booking-line").Length);
        Assert.Equal("60,00", Field(dialogs, "booking-line-amount-0").GetAttribute("value"));
        Assert.NotNull(split.QuerySelector("[data-testid='booking-line-category-2']"));
        Assert.NotNull(split.QuerySelector("[data-testid='booking-line-position-2']"));
        Next();
        Assert.Equal("Schritt 5 von 5: Prüfen und speichern", Progress());
    }

    [Fact]
    public void Without_splitting_there_is_no_split_step_and_switching_it_off_removes_the_extra_lines()
    {
        OpenDialog();
        Assert.Equal(4, dialogs.FindAll("[data-testid='step-dialog-step-1'], [data-testid='step-dialog-step-2'], [data-testid='step-dialog-step-3'], [data-testid='step-dialog-step-5']").Count);

        Next();
        Next();
        dialogs.Find("[data-testid='booking-split']").Click();
        dialogs.Find("[data-testid='booking-line-add']").Click();
        Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-line-amount-2']"));
        Assert.Contains("von 5", Progress());

        dialogs.Find("[data-testid='booking-split']").Click();

        Assert.Empty(dialogs.FindAll("[data-testid='booking-line-amount-1']"));
        Assert.Contains("von 4", Progress());
    }

    [Fact]
    public void A_split_booking_is_saved_with_all_its_lines()
    {
        OpenDialog();
        Field(dialogs, "booking-amount").Change("90");
        Next();
        Next();
        BookingDialogSteps.Choose(dialogs, "booking-line-category-0", FakeCategoryApi.Groceries.Id.ToString());
        dialogs.Find("[data-testid='booking-split']").Click();
        Field(dialogs, "booking-line-amount-1").Change("30");
        BookingDialogSteps.Choose(dialogs, "booking-line-category-1", FakeCategoryApi.Restaurant.Id.ToString());

        BookingDialogSteps.ToLast(dialogs);
        Assert.Contains("Zeile 2", dialogs.Find("[data-testid='booking-summary']").TextContent);
        dialogs.Find("[data-testid='booking-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        Assert.Equal(
            [new SaveBookingLine(60m, PotKeys.Needs, FakeCategoryApi.Groceries.Id), new SaveBookingLine(30m, PotKeys.Wants, FakeCategoryApi.Restaurant.Id)],
            BookingApiFake.Created[0].Lines);
    }
}
