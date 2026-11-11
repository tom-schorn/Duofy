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
        Assert.Equal(["Keine Kategorie", "Lebensmittel", "Körperpflege", "Essen gehen"], BookingDialogSteps.OptionLabels(dialogs, "booking-line-category-0"));

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
        Assert.Equal(["Keine Kategorie", "Grundbedarf", "Lebensmittel", "Körperpflege", "Wünsche", "Essen gehen"], items);
    }

    [Fact]
    public void Positions_show_name_and_planned_amount_under_their_due_day()
    {
        var rent = FakePlanApi.Position("Miete", 890m, day: 3);
        var gym = FakePlanApi.Position("Fitness", 29.9m, day: 15);
        var gez = FakePlanApi.Position("GEZ", 18.36m, day: 15, booked: 18.36m, done: true);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [gym, rent, gez] };

        OpenDialog();

        var list = dialogs.Find("[data-testid='booking-line-position-0'] ul");
        Assert.Equal(["Kein Posten", "Fällig am 3.", "Miete 890,00 €", "Fällig am 15.", "Fitness 29,90 €", "GEZ (abgehakt) 18,36 €"],
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

    // S1: Escape in einer offenen Auswahl schließt nur die Liste, nicht den Dialog
    [Fact]
    public void Escape_in_an_open_combo_box_closes_only_the_list_not_the_dialog()
    {
        OpenDialog();
        Field(dialogs, "booking-amount").Change("42");
        Next();
        var input = dialogs.Find("[data-testid='booking-account-input']");
        input.Focus();
        Assert.Equal("true", dialogs.Find("[data-testid='booking-account-input']").GetAttribute("aria-expanded"));

        dialogs.Find("[data-testid='booking-account-input']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("false", dialogs.Find("[data-testid='booking-account-input']").GetAttribute("aria-expanded"));
        Assert.NotEmpty(dialogs.FindAll("[data-testid='step-dialog']"));
        Assert.Empty(dialogs.FindAll("[data-testid='confirm-dialog'], [role='alertdialog']"));
    }

    // S2: die Kategorie lässt sich wieder löschen
    [Fact]
    public void The_category_can_be_cleared_again_with_no_category()
    {
        OpenDialog();
        Field(dialogs, "booking-amount").Change("42");
        Next();
        Next();
        BookingDialogSteps.Choose(dialogs, "booking-line-category-0", FakeCategoryApi.Restaurant.Id.ToString());
        Assert.Equal(FakeCategoryApi.Restaurant.Id.ToString(), BookingDialogSteps.Chosen(dialogs, "booking-line-category-0"));

        BookingDialogSteps.Choose(dialogs, "booking-line-category-0", "");

        Assert.Equal("", BookingDialogSteps.Chosen(dialogs, "booking-line-category-0"));
        BookingDialogSteps.ToLast(dialogs);
        dialogs.Find("[data-testid='booking-save']").Click();
        dialogs.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        Assert.Null(BookingApiFake.Created[0].Lines[0].CategoryId);
    }

    // N2: die Pfeiltasten laufen über die gezeichnete Reihenfolge, auch wenn die Optionen nicht nach Gruppen geordnet kommen
    [Fact]
    public void The_list_is_ordered_by_group_so_the_arrow_keys_follow_what_is_drawn()
    {
        var chosen = "";
        var cut = Render<Duofy.Shared.Layout.ComboBox>(p => p
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => chosen = v))
            .Add(c => c.Options, new[]
            {
                new Duofy.Shared.Layout.ComboOption("a1", "A eins", "A"), new Duofy.Shared.Layout.ComboOption("b1", "B eins", "B"),
                new Duofy.Shared.Layout.ComboOption("a2", "A zwei", "A"),
            })
            .Add(c => c.TestId, "order"));
        cut.Find("[data-testid='order-input']").Focus();

        Assert.Equal(["A eins", "A zwei", "B eins"], BookingDialogSteps.OptionLabels(cut, "order"));
        cut.Find("[data-testid='order-input']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("[data-testid='order-input']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("[data-testid='order-input']").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("a2", chosen);
    }

    // N4: keine Überschrift "Zeile 1" direkt über "Zeile 1 bekommt den Rest"
    [Fact]
    public void The_first_split_line_has_no_heading_above_its_label()
    {
        OpenDialog();
        Field(dialogs, "booking-amount").Change("90");
        Next();
        Next();
        dialogs.Find("[data-testid='booking-split']").Click();
        Next();

        Assert.Empty(dialogs.FindAll("[data-testid='booking-line-0'] .booking-line-title"));
        Assert.Contains("Zeile 1 bekommt den Rest", dialogs.Find("[data-testid='booking-line-0']").TextContent);
        Assert.Equal("Zeile 2", dialogs.Find("[data-testid='booking-line-1'] .booking-line-title").TextContent.Trim());
    }

    // N5
    [Fact]
    public void The_account_field_invites_to_choose_or_search()
    {
        OpenDialog();
        Accounts.Accounts.Clear();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: false, Active: true));
        dialogs.Render();

        Assert.Equal("Konto wählen oder suchen", dialogs.Find("[data-testid='booking-account-input']").GetAttribute("placeholder"));
    }

    // A6: der frühe Speichern-Knopf
    [Fact]
    public void Early_save_is_not_shown_in_step_one_nor_before_amount_and_account_are_set()
    {
        OpenDialog();
        Field(dialogs, "booking-amount").Change("42");
        Assert.Empty(dialogs.FindAll("[data-testid='booking-save-early']"));

        Next();
        Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-save-early']"));
        Assert.Equal("Speichern", dialogs.Find("[data-testid='booking-save-early']").TextContent.Trim());

        Next();
        Next();
        Assert.Empty(dialogs.FindAll("[data-testid='booking-save-early']"));
        Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-save']"));
    }

    [Fact]
    public void Early_save_waits_for_the_amount()
    {
        OpenDialog();
        Next();

        Assert.Empty(dialogs.FindAll("[data-testid='booking-save-early']"));

        Back();
        Field(dialogs, "booking-amount").Change("42");
        Next();
        Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-save-early']"));
    }

    [Fact]
    public void Early_save_waits_for_the_account()
    {
        Accounts.Accounts.Clear();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: false, Active: true));
        OpenDialog();
        Field(dialogs, "booking-amount").Change("42");
        Next();
        Assert.Empty(dialogs.FindAll("[data-testid='booking-save-early']"));

        BookingDialogSteps.Choose(dialogs, "booking-account", Accounts.Accounts[0].Id.ToString());

        Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-save-early']"));
    }

    [Fact]
    public void Early_save_saves_like_the_last_step()
    {
        OpenDialog();
        Field(dialogs, "booking-amount").Change("42");
        Next();
        BookingDialogSteps.Choose(dialogs, "booking-account", spar.Id.ToString());

        dialogs.Find("[data-testid='booking-save-early']").Click();

        dialogs.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        Assert.Equal(spar.Id, BookingApiFake.Created[0].AccountId);
        Assert.Equal(42m, BookingApiFake.Created[0].Lines.Sum(l => l.Amount));
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='step-dialog']")));
    }

    [Fact]
    public void A_failed_early_save_shows_the_error_in_the_last_step()
    {
        BookingApiFake.CreateError = "validation";
        OpenDialog();
        Field(dialogs, "booking-amount").Change("42");
        Next();

        dialogs.Find("[data-testid='booking-save-early']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-error']")));
        Assert.Equal("step-dialog-step-5", CurrentStep(dialogs));
    }
}
