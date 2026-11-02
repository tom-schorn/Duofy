using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Die Bewegungen am Konto /konten/{id} (Spec 51, R5), dazu der Buchungsdialog, wie ihn der Monat öffnet.</summary>
public class AccountBookingsPageTests : PageTestContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly AccountChoice giro = new(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true);
    private readonly AccountChoice cash = new(Guid.NewGuid(), "Bargeld", IsDefault: false, Active: true);

    // Radzen zeichnet Dialoge in RadzenComponents (im echten Layout stehen sie in jedem Layout).
    private readonly IRenderedComponent<IComponent> dialogs;

    public AccountBookingsPageTests()
    {
        dialogs = RenderRadzenHost();
        Accounts.Accounts.AddRange([giro, cash]);
    }

    private IRenderedComponent<AccountBookings> OpenPage(Guid? account = null)
    {
        var cut = Render<AccountBookings>(p => p.Add(x => x.AccountId, account ?? giro.Id));
        cut.WaitForAssertion(() => Assert.NotEmpty(BookingApiFake.ListCalls));
        return cut;
    }

    [Fact]
    public void The_page_is_reachable_at_the_account_and_needs_a_sign_in()
    {
        Assert.Equal("/konten/{AccountId:guid}", typeof(AccountBookings).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(typeof(AccountBookings).GetCustomAttribute<AuthorizeAttribute>());
    }

    // Spec 51, A5
    [Fact]
    public void The_old_address_leads_to_the_current_month()
    {
        Assert.NotNull(typeof(BookingsRedirect).GetCustomAttribute<AuthorizeAttribute>());

        Render<BookingsRedirect>();

        Assert.EndsWith("/plan/2026/10", Navigation.Uri);
    }

    // Spec 51, A7: Hier wird nicht neu gebucht.
    [Fact]
    public void The_page_has_no_way_to_add_a_booking_and_names_the_account()
    {
        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("Girokonto", cut.Find("[data-testid='account-bookings-title']").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='bookings-add']"));
        Assert.Empty(cut.FindAll(".app-fab"));
    }

    // R8
    [Fact]
    public void The_page_opens_the_current_month_for_all_accounts()
    {
        var cut = OpenPage();

        Assert.Equal((2026, 10, (Guid?)giro.Id), BookingApiFake.ListCalls[0]);
        Assert.Contains("Oktober 2026", cut.Markup);
    }

    // A14
    [Fact]
    public void The_left_arrow_shows_the_previous_month_with_its_own_sums()
    {
        BookingApiFake.Bookings.Add(FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 10, 5), 11m, FakeCategoryApi.Groceries, "Oktoberkauf"));
        BookingApiFake.Bookings.Add(FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 9, 3), 30m, FakeCategoryApi.Groceries, "Altkauf"));
        BookingApiFake.Bookings.Add(FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 9, 20), 70m, FakeCategoryApi.Restaurant, "Neukauf"));
        BookingApiFake.Bookings.Add(FakeBookingApi.Income(giro.Id, new DateOnly(2026, 9, 1), 2000m));
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.Contains("Oktoberkauf", cut.Markup));

        Click(cut, "bookings-previous-month");

        cut.WaitForAssertion(() => Assert.Contains("September 2026", cut.Markup));
        Assert.Equal((2026, 9, (Guid?)giro.Id), BookingApiFake.ListCalls[^1]);
        Assert.DoesNotContain("Oktoberkauf", cut.Markup);
        // Neueste zuerst: der 20. steht vor dem 3.
        Assert.True(cut.Markup.IndexOf("Neukauf", StringComparison.Ordinal) < cut.Markup.IndexOf("Altkauf", StringComparison.Ordinal));
        Assert.Contains("2.000,00", Text(cut, "bookings-sum-income"));
        Assert.Contains("100,00", Text(cut, "bookings-sum-expenses"));
    }

    [Fact]
    public void The_right_arrow_goes_forward_across_the_year_end()
    {
        var cut = OpenPage();

        Click(cut, "bookings-next-month");
        Click(cut, "bookings-next-month");
        Click(cut, "bookings-next-month");

        cut.WaitForAssertion(() => Assert.Contains("Januar 2027", cut.Markup));
        Assert.Equal((2027, 1, (Guid?)giro.Id), BookingApiFake.ListCalls[^1]);
    }

    // A15
    [Fact]
    public void The_page_shows_only_this_accounts_bookings_and_counts_transfers_in_the_sums()
    {
        BookingApiFake.Bookings.Add(FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 10, 5), 10m, FakeCategoryApi.Groceries, "AufGiro"));
        BookingApiFake.Bookings.Add(FakeBookingApi.Expense(cash.Id, new DateOnly(2026, 10, 6), 5m, FakeCategoryApi.Groceries, "AufBargeld"));
        BookingApiFake.Bookings.Add(FakeBookingApi.Transfer(giro.Id, cash.Id, new DateOnly(2026, 10, 7), 100m));

        var cut = OpenPage(cash.Id);

        cut.WaitForAssertion(() => Assert.Contains("AufBargeld", cut.Markup));
        Assert.DoesNotContain("AufGiro", cut.Markup);
        Assert.Equal((2026, 10, (Guid?)cash.Id), BookingApiFake.ListCalls[^1]);
        Assert.Contains("AufBargeld", cut.Markup);
        // Für das Bargeld ist die Umbuchung Geld, das hereinkommt.
        Assert.Contains("100,00", Text(cut, "bookings-sum-income"));
        Assert.Contains("5,00", Text(cut, "bookings-sum-expenses"));
    }

    [Fact]
    public void A_split_booking_is_shown_once_with_its_total_and_the_note_aufgeteilt()
    {
        var split = new BookingResponse(
            Guid.NewGuid(), BookingKinds.Expense, giro.Id, null, new DateOnly(2026, 10, 3), 80m, null,
            [
                new BookingLineResponse(Guid.NewGuid(), 60m, PotKeys.Needs, FakeCategoryApi.Groceries.Id),
                new BookingLineResponse(Guid.NewGuid(), 20m, PotKeys.Needs, FakeCategoryApi.PersonalCare.Id),
            ]);
        BookingApiFake.Bookings.Add(split);

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("aufgeteilt", cut.Markup));
        Assert.Equal(1, cut.FindAll($"[data-testid='booking-{split.Id}']").Count);
        Assert.Contains("80,00", cut.Find($"[data-testid='booking-{split.Id}']").TextContent);
    }

    [Fact]
    public void A_transfer_is_shown_with_both_account_names()
    {
        BookingApiFake.Bookings.Add(FakeBookingApi.Transfer(giro.Id, cash.Id, new DateOnly(2026, 10, 7), 100m));

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("Girokonto → Bargeld", cut.Markup));
    }

    // Spec 72, R9: Die Liste nennt Quell- und Zieltopf als Text, nicht nur durch Farbe.
    [Fact]
    public void A_transfer_shows_its_source_and_target_pot_as_text()
    {
        var transfer = FakeBookingApi.Transfer(giro.Id, cash.Id, new DateOnly(2026, 10, 7), 100m);
        BookingApiFake.Bookings.Add(transfer);

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("Sparen → Wünsche", cut.Find($"[data-testid='booking-transfer-pots-{transfer.Id}']").TextContent));
    }

    // Spec 72, R9: Eine ältere Umbuchung ohne Töpfe bleibt lesbar und wird angezeigt.
    [Fact]
    public void An_older_transfer_without_pots_is_still_listed()
    {
        var older = FakeBookingApi.Transfer(giro.Id, cash.Id, new DateOnly(2026, 10, 7), 100m) with { Lines = [], TargetPot = null };
        BookingApiFake.Bookings.Add(older);

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("Girokonto → Bargeld", cut.Markup));
        Assert.Empty(cut.FindAll($"[data-testid='booking-transfer-pots-{older.Id}']"));
    }

    [Fact]
    public void Accounts_that_cannot_be_loaded_show_the_error_and_not_the_hint_to_create_one()
    {
        Accounts.Accounts.Clear();
        Accounts.Error = ApiClient.ClientErrorCodes.ServerUnreachable;

        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("Der Server ist gerade nicht erreichbar.", cut.Markup));
        Assert.DoesNotContain("Lege zuerst ein Konto an", cut.Markup);
    }

    [Fact]
    public void An_empty_month_says_so()
    {
        var cut = OpenPage();

        cut.WaitForAssertion(() => Assert.Contains("In diesem Monat gibt es noch keine Buchungen.", cut.Markup));
    }

    // A13
    [Fact]
    public void Delete_asks_for_confirmation_and_deletes_only_after_confirming()
    {
        var booking = FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 10, 5), 11m, FakeCategoryApi.Groceries, "Weg damit");
        BookingApiFake.Bookings.Add(booking);
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.Contains("Weg damit", cut.Markup));

        Click(cut, $"booking-delete-{booking.Id}");

        dialogs.WaitForAssertion(() => Assert.Contains("Buchung löschen?", dialogs.Markup));
        Assert.Empty(BookingApiFake.Deleted);

        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Löschen").Click();

        cut.WaitForAssertion(() => Assert.Equal([booking.Id], BookingApiFake.Deleted));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Weg damit", cut.Markup));
    }

    [Fact]
    public void Cancelling_the_confirmation_keeps_the_booking()
    {
        var booking = FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 10, 5), 11m, FakeCategoryApi.Groceries, "Bleibt");
        BookingApiFake.Bookings.Add(booking);
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.Contains("Bleibt", cut.Markup));
        Click(cut, $"booking-delete-{booking.Id}");
        dialogs.WaitForAssertion(() => Assert.Contains("Buchung löschen?", dialogs.Markup));

        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Buchung löschen?", dialogs.Markup));
        Assert.Empty(BookingApiFake.Deleted);
        Assert.Contains("Bleibt", cut.Markup);
    }

    // A16
    [Fact]
    public void The_plus_button_opens_the_dialog_with_an_expense_dated_today_on_the_default_account()
    {
        OpenMonthAndNewBooking();

        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));
        Assert.Equal("true", dialogs.Find("[data-testid='booking-kind-expense']").GetAttribute("aria-pressed"));
        Assert.Equal("false", dialogs.Find("[data-testid='booking-kind-income']").GetAttribute("aria-pressed"));
        Assert.Equal(Today.ToString("yyyy-MM-dd"), Input(dialogs, "booking-date").GetAttribute("value"));
        Assert.Equal(giro.Id.ToString(), SelectedValue(dialogs, "booking-account"));
    }

    // R9: ohne Standardkonto bleibt das Feld leer
    [Fact]
    public void Without_a_default_account_the_account_field_is_empty()
    {
        Accounts.Accounts.Clear();
        Accounts.Accounts.Add(cash);

        OpenMonthAndNewBooking();

        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));
        Assert.Equal("", SelectedValue(dialogs, "booking-account"));
    }

    [Fact]
    public void Without_any_account_the_month_asks_for_one_first()
    {
        Accounts.Accounts.Clear();

        var cut = OpenMonthAndNewBooking();

        cut.WaitForAssertion(() => Assert.Contains("Lege zuerst ein Konto an", cut.Markup));
        Assert.Empty(dialogs.FindAll("[data-testid='booking-next']"));
    }

    [Fact]
    public void Closed_accounts_are_not_offered_in_the_dialog()
    {
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Altes Konto", IsDefault: false, Active: false));

        OpenMonthAndNewBooking();

        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));
        Assert.DoesNotContain("Altes Konto", dialogs.Find("[data-testid='booking-account']").InnerHtml);
    }

    // A5
    [Fact]
    public void Choosing_the_category_restaurant_preselects_the_pot_wants_which_can_be_changed()
    {
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));

        SelectIn(dialogs, "booking-line-category-0", FakeCategoryApi.Restaurant.Id.ToString());

        dialogs.WaitForAssertion(() => Assert.Equal(PotKeys.Wants, SelectedValue(dialogs, "booking-line-pot-0")));
        SelectIn(dialogs, "booking-line-pot-0", PotKeys.Needs);
        dialogs.WaitForAssertion(() => Assert.Equal(PotKeys.Needs, SelectedValue(dialogs, "booking-line-pot-0")));
    }

    [Fact]
    public void Expense_dialog_offers_no_income_categories_and_income_only_those()
    {
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));
        var expenseOptions = dialogs.Find("[data-testid='booking-line-category-0']").InnerHtml;
        Assert.Contains("Lebensmittel", expenseOptions);
        Assert.DoesNotContain("Gehalt", expenseOptions);

        dialogs.Find("[data-testid='booking-kind-income']").Click();

        dialogs.WaitForAssertion(() =>
        {
            var incomeOptions = dialogs.Find("[data-testid='booking-line-category-0']").InnerHtml;
            Assert.Contains("Gehalt", incomeOptions);
            Assert.DoesNotContain("Lebensmittel", incomeOptions);
        });
    }

    // A21
    [Fact]
    public void Splitting_adds_a_line_and_the_first_line_shows_the_rest()
    {
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));
        Input(dialogs, "booking-amount").Change("80");
        dialogs.WaitForAssertion(() => Assert.Equal("80,00", Input(dialogs, "booking-line-amount-0").GetAttribute("value")));

        dialogs.Find("[data-testid='booking-split']").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(dialogs.Find("[data-testid='booking-line-amount-1']")));
        Input(dialogs, "booking-line-amount-1").Change("20");

        dialogs.WaitForAssertion(() => Assert.Equal("60,00", Input(dialogs, "booking-line-amount-0").GetAttribute("value")));
    }

    [Fact]
    public void Saving_sends_the_booking_and_closes_the_dialog()
    {
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));
        Input(dialogs, "booking-amount").Change("42");
        SelectIn(dialogs, "booking-line-category-0", FakeCategoryApi.Groceries.Id.ToString());

        dialogs.Find("[data-testid='booking-next']").Click();

        dialogs.Find("[data-testid='booking-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        var request = BookingApiFake.Created[0];
        Assert.Equal(BookingKinds.Expense, request.Kind);
        Assert.Equal(giro.Id, request.AccountId);
        Assert.Equal(Today, request.Date);
        Assert.Equal(42m, request.Amount);
        Assert.Equal([new SaveBookingLine(42m, PotKeys.Needs, FakeCategoryApi.Groceries.Id)], request.Lines);
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Neue Buchung", dialogs.Markup));
    }

    [Fact]
    public void An_error_of_the_api_is_shown_as_german_text_and_the_dialog_stays_open()
    {
        BookingApiFake.CreateError = ErrorCodes.LinesSumMismatch;
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));
        Input(dialogs, "booking-amount").Change("42");

        dialogs.Find("[data-testid='booking-next']").Click();

        dialogs.Find("[data-testid='booking-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Die Zeilen ergeben zusammen nicht den Betrag der Buchung.", dialogs.Markup));
        Assert.Contains("Neue Buchung", dialogs.Markup);
    }

    [Fact]
    public void Editing_opens_the_dialog_with_the_saved_values_and_saves_with_put()
    {
        var booking = FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 10, 5), 11m, FakeCategoryApi.Groceries, "Alt");
        BookingApiFake.Bookings.Add(booking);
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.Contains("Alt", cut.Markup));

        Click(cut, $"booking-edit-{booking.Id}");

        dialogs.WaitForAssertion(() => Assert.Contains("Buchung ändern", dialogs.Markup));
        Assert.Equal("11,00", Input(dialogs, "booking-amount").GetAttribute("value"));
        Input(dialogs, "booking-amount").Change("12");
        dialogs.Find("[data-testid='booking-next']").Click();
        dialogs.Find("[data-testid='booking-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(BookingApiFake.Updated));
        Assert.Equal(booking.Id, BookingApiFake.Updated[0].Id);
        Assert.Equal(12m, BookingApiFake.Updated[0].Request.Amount);
    }

    [Fact]
    public void A_transfer_asks_for_two_accounts_and_hides_the_lines()
    {
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));

        dialogs.Find("[data-testid='booking-kind-transfer']").Click();

        dialogs.WaitForAssertion(() => Assert.NotNull(dialogs.Find("[data-testid='booking-counter-account']")));
        Assert.Empty(dialogs.FindAll("[data-testid='booking-line-category-0']"));
        // Spec 72, R9: Quelle und Ziel sind Pflichtfelder ohne Vorauswahl.
        var source = dialogs.Find("[data-testid='booking-transfer-source-pot']");
        var target = dialogs.Find("[data-testid='booking-transfer-target-pot']");
        Assert.Equal(("", ""), (source.GetAttribute("value"), target.GetAttribute("value")));
        Assert.Equal("true", source.GetAttribute("aria-required"));
        Assert.Equal("true", target.GetAttribute("aria-required"));
        Assert.Contains("Von Topf (Pflicht)", dialogs.Markup);
        Assert.Contains("Zu Topf (Pflicht)", dialogs.Markup);
        Assert.Contains("(bitte wählen)", source.TextContent);

        SelectIn(dialogs, "booking-counter-account", cash.Id.ToString());
        SelectIn(dialogs, "booking-transfer-source-pot", PotKeys.Savings);
        SelectIn(dialogs, "booking-transfer-target-pot", PotKeys.Wants);
        Input(dialogs, "booking-amount").Change("100");
        dialogs.Find("[data-testid='booking-next']").Click();
        dialogs.Find("[data-testid='booking-save']").Click();

        cut.WaitForAssertion(() => Assert.Single(BookingApiFake.Created));
        var request = BookingApiFake.Created[0];
        Assert.Equal(BookingKinds.Transfer, request.Kind);
        Assert.Equal(cash.Id, request.CounterAccountId);
        Assert.Equal(PotKeys.Savings, Assert.Single(request.Lines!).Pot);
        Assert.Equal(PotKeys.Wants, request.TargetPot);
    }

    [Fact]
    public void An_own_category_can_be_created_in_the_dialog_and_is_chosen_at_once()
    {
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));

        dialogs.Find("[data-testid='booking-new-category']").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(dialogs.Find("[data-testid='booking-new-category-name']")));
        Input(dialogs, "booking-new-category-name").Change("Friseur Kinder");
        SelectIn(dialogs, "booking-new-category-group", "children");
        SelectIn(dialogs, "booking-new-category-pot", PotKeys.Needs);
        dialogs.Find("[data-testid='booking-new-category-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Single(Categories.Created));
        Assert.Equal(new SaveCategoryRequest("Friseur Kinder", "children", PotKeys.Needs), Categories.Created[0]);
        var created = Categories.All.Last();
        dialogs.WaitForAssertion(() => Assert.Equal(created.Id.ToString(), SelectedValue(dialogs, "booking-line-category-0")));
    }

    // Radzen-Umstellung R16
    [Fact]
    public void The_page_and_the_dialog_are_built_with_radzen_and_the_amount_is_german_with_a_numeric_keypad()
    {
        BookingApiFake.Bookings.Add(FakeBookingApi.Expense(giro.Id, new DateOnly(2026, 10, 5), 11m, FakeCategoryApi.Groceries, "Oktoberkauf"));
        var booking = BookingApiFake.Bookings[0];
        var cut = OpenPage();
        cut.WaitForAssertion(() => Assert.Contains("Oktoberkauf", cut.Markup));

        Assert.Contains("rz-card", cut.Find($"[data-testid='booking-{booking.Id}']").ClassList);

        OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.Contains("Neue Buchung", dialogs.Markup));

        var amount = Input(dialogs, "booking-amount");
        Assert.Equal("booking-amount-input-label", amount.GetAttribute("aria-labelledby"));
        Assert.Equal("decimal", amount.GetAttribute("inputmode"));
        amount.Change("1234,5");
        dialogs.WaitForAssertion(() => Assert.Equal("1234,50", Input(dialogs, "booking-amount").GetAttribute("value")));
        Assert.Equal("date", Input(dialogs, "booking-date").GetAttribute("type"));
    }

    // --- Hilfen -------------------------------------------------------------

    private static void Click(IRenderedComponent<AccountBookings> cut, string testId) =>
        cut.Find($"[data-testid='{testId}']").Click();

    private static string Text(IRenderedComponent<AccountBookings> cut, string testId) =>
        cut.Find($"[data-testid='{testId}']").TextContent;

    private static IElement Input(IRenderedComponent<IComponent> cut, string testId)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        return element.TagName is "INPUT" or "TEXTAREA" ? element : element.QuerySelector("input,textarea")!;
    }

    // Ein Auswahlfeld (select) ändert sich mit Change, die Kontoauswahl (Gruppe von Optionsfeldern, Issue 16) mit dem Optionsfeld.
    private static void SelectIn(IRenderedComponent<IComponent> cut, string testId, string value)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        if (element.TagName == "FIELDSET")
        {
            element.QuerySelector($"input[type='radio'][value='{value}']")!.Change(true);
        }
        else
        {
            element.Change(value);
        }
    }

    // Blazor schreibt die Auswahl als value-Attribut an das select-Element; bei Optionsfeldern ist es das angekreuzte.
    private static string? SelectedValue(IRenderedComponent<IComponent> cut, string testId)
    {
        var element = cut.Find($"[data-testid='{testId}']");
        return element.TagName == "FIELDSET"
            ? element.QuerySelectorAll("input[type='radio']").Single(i => i.HasAttribute("checked")).GetAttribute("value")
            : element.GetAttribute("value");
    }
}
