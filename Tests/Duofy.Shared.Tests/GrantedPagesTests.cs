using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 68, R8: Plan, Buchungen, Konten und Verträge zeigen die Zahlen des gewählten Mitglieds; Knöpfe, die die
/// Stufe nicht erlaubt, sind ausgegraut und nennen den Grund. Angemeldet ist Ben, Anna gibt ihm frei, was der
/// Test vorgibt. "Heute" ist der 21. Oktober 2026.
/// </summary>
public class GrantedPagesTests : PageTestContext
{
    private const string Reason = "Dafür hat dir Anna keine Freigabe gegeben.";
    private const string NotShared = "Anna teilt Buchungen nicht mit dir.";

    private static readonly Guid Anna = FakeHouseholdApi.AnnaId;

    private readonly IRenderedComponent<IComponent> dialogs;

    public GrantedPagesTests()
    {
        dialogs = RenderDialogHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Annas Giro", IsDefault: true, Active: true));
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    /// <summary>Anna gibt Ben diese Stufen; Ben wählt sie im Umschalter.</summary>
    private async Task ChooseAnnaAsync(GrantLevels levels)
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = levels },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
        await Member.LoadAsync();
        Member.Choose(Anna);
    }

    private static void AssertLocked<T>(IRenderedComponent<T> cut, string testId) where T : Microsoft.AspNetCore.Components.IComponent
    {
        var element = cut.Find($"[data-testid='{testId}']");
        Assert.Equal("true", element.GetAttribute("aria-disabled"));
        Assert.Equal(GrantLock.ReasonId, element.GetAttribute("aria-describedby"));
    }

    // BbButton schreibt aria-disabled="false" aus; frei heißt also: nicht gesperrt und ohne Verweis auf einen Grund.
    private static void AssertFree<T>(IRenderedComponent<T> cut, string testId) where T : Microsoft.AspNetCore.Components.IComponent
    {
        var element = cut.Find($"[data-testid='{testId}']");
        Assert.NotEqual("true", element.GetAttribute("aria-disabled"));
        Assert.Null(element.GetAttribute("aria-describedby"));
    }

    // Fund S3: Ohne Sicht auf die Buchungen kommt jeder gebuchte Betrag als 0 an. Lücke, Liste und offene Beträge wären
    // falsch; Auswertung, Buchungen und Anstehend sagen stattdessen, dass die Person das nicht teilt.
    [Fact]
    public async Task With_plan_view_only_review_bookings_and_unpaid_say_not_shared_instead_of_wrong_amounts()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });
        var rent = FakePlanApi.Position("Miete", 890m) with { DueDay = 1 };
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [rent],
            Overview = new PlanOverviewResponse(0m, 890m, -890m, [new PlanPotResponse(PotKeys.Needs, 890m, 0m)], []),
        };

        var cut = Render<Plan>();
        OpenReviewTab(cut);

        Assert.Contains(NotShared, cut.Find("[data-testid='month-review-not-shared']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='month-gap']"));
        Assert.Empty(cut.FindAll("[data-testid='month-due']"));
        Assert.DoesNotContain("weniger als geplant", cut.Markup);
        var unpaid = cut.Find("[data-testid='month-block-unpaid']").TextContent;
        Assert.Contains(NotShared, unpaid);
        Assert.DoesNotContain("890,00", unpaid);

        OpenBookingsTab(cut);

        Assert.Contains(NotShared, cut.Find("[data-testid='bookings-not-shared']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='month-bookings-empty']"));
    }

    // Gegenprobe zu S3: Mit Buchungen Ansehen bleiben Auswertung und Anstehend vollständig.
    [Fact]
    public async Task With_bookings_view_the_review_shows_the_gap_and_the_open_amounts()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.View, Book = AccessLevelKeys.View });
        var rent = FakePlanApi.Position("Miete", 890m) with { DueDay = 1 };
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [rent],
            Overview = new PlanOverviewResponse(0m, 890m, -890m, [new PlanPotResponse(PotKeys.Needs, 890m, 0m)], []),
        };

        var cut = Render<Plan>();
        OpenReviewTab(cut);

        Assert.Empty(cut.FindAll("[data-testid='month-review-not-shared']"));
        Assert.Contains("890,00 €", cut.Find("[data-testid='month-review-plan-vs-actual']").TextContent);
        Assert.Contains("Miete", cut.Find("[data-testid='month-due']").TextContent);
        Assert.Contains("890,00 €", cut.Find("[data-testid='month-block-unpaid']").TextContent);
    }

    // A13
    [Fact]
    public async Task With_plan_view_annas_month_shows_with_add_and_tick_greyed_out_and_the_reason()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });
        var rent = FakePlanApi.Position("Miete", 890m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [rent] };

        var cut = Render<Plan>();
        OpenPlanTab(cut);

        Assert.All(PlanApiFake.Owners, owner => Assert.Equal(Anna, owner));
        Assert.Equal(Anna, Assert.Single(Categories.Owners));
        AssertLocked(cut, "plan-add");
        AssertLocked(cut, $"plan-tick-{rent.Id}");
        AssertLocked(cut, $"plan-edit-{rent.Id}");
        Assert.Equal(Reason, cut.Find($"#{GrantLock.ReasonId}").TextContent.Trim());

        cut.Find("[data-testid='plan-add']").Click();
        cut.Find($"[data-testid='plan-edit-{rent.Id}']").Click();

        Assert.Empty(dialogs.FindAll(DialogSelector));
        Assert.Empty(PlanApiFake.Ticks);
    }

    // A13
    [Fact]
    public async Task A_page_whose_area_anna_did_not_share_says_so_and_asks_nothing()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });

        var commitments = Render<Commitments>();
        var bookings = Render<AccountBookings>(c => c.Add(x => x.AccountId, Guid.NewGuid()));
        var accounts = Render<Accounts>();

        Assert.Contains("Anna teilt Verträge nicht mit dir.", commitments.Markup);
        Assert.Contains("Anna teilt Buchungen nicht mit dir.", bookings.Markup);
        Assert.Contains("Anna teilt Konten nicht mit dir.", accounts.Markup);
        Assert.Empty(CommitmentApiFake.ListCalls);
        Assert.Empty(BookingApiFake.ListCalls);
        Assert.Equal(0, AccountApiFake.ListCalls);
        Assert.Empty(commitments.FindAll("[data-testid='commitments-add']"));
    }

    // R8: Mit Ändern geht Bearbeiten; Abhaken braucht die Buchungen, Löschen (im Dialog des Postens) die Stufe Löschen.
    [Fact]
    public async Task With_plan_edit_editing_works_while_ticking_and_deleting_stay_greyed_out()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.Edit });
        var gift = FakePlanApi.Position("Geschenk", 30m) with { CommitmentId = null };
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [gift] };

        var cut = Render<Plan>();
        OpenPlanTab(cut);

        AssertFree(cut, "plan-add");
        AssertFree(cut, $"plan-edit-{gift.Id}");
        AssertLocked(cut, $"plan-tick-{gift.Id}");
        cut.Find($"[data-testid='plan-edit-{gift.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='plan-delete']")));
        AssertLocked(dialogs, "plan-delete");
        // Ohne Konten freizugeben, fragt die Seite Annas Konten gar nicht erst ab.
        Assert.Empty(Accounts.Owners);
    }

    // R8: Mit Buchungen Anlegen und Konten Ansehen bucht Ben für Anna; Ändern und Löschen bleiben aus.
    [Fact]
    public async Task With_book_create_ben_books_for_anna_but_cannot_change_or_delete()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Book = AccessLevelKeys.Create, Accounts = AccessLevelKeys.View, Plan = AccessLevelKeys.View });
        var booking = new BookingResponse(Guid.NewGuid(), BookingKinds.Expense, Accounts.Accounts[0].Id, null, new DateOnly(2026, 10, 5),
            12m, null, [new BookingLineResponse(Guid.NewGuid(), 12m, PotKeys.Needs, FakeCategoryApi.Groceries.Id)]);
        BookingApiFake.Bookings.Add(booking);

        var cut = Render<AccountBookings>(c => c.Add(x => x.AccountId, Accounts.Accounts[0].Id));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='booking-{booking.Id}']")));

        Assert.Equal(Anna, Assert.Single(BookingApiFake.ListOwners));
        Assert.Equal(Anna, Assert.Single(Accounts.Owners));
        AssertLocked(cut, $"booking-edit-{booking.Id}");
        AssertLocked(cut, $"booking-delete-{booking.Id}");

        OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-next']")));
        Assert.Empty(dialogs.FindAll("[data-testid='booking-new-category']"));
        Field(dialogs, "booking-amount").Change("42");
        BookingDialogSteps.Choose(dialogs, "booking-line-category-0", FakeCategoryApi.Groceries.Id.ToString());
        BookingDialogSteps.ToLast(dialogs);
        dialogs.Find("[data-testid='booking-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(BookingApiFake.Created).OwnerId));
    }

    // Spec 140, R2: Im Buchungsdialog für Anna legt Ben eine Kategorie nur mit Kategorien Anlegen an, und sie gehört Anna.
    [Fact]
    public async Task With_categories_create_the_booking_dialog_creates_a_category_for_anna()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Book = AccessLevelKeys.Create, Accounts = AccessLevelKeys.View, Plan = AccessLevelKeys.View, Categories = AccessLevelKeys.Create });

        OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-new-category']")));
        dialogs.Find("[data-testid='booking-new-category']").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(dialogs.Find("[data-testid='booking-new-category-name']")));
        dialogs.Find("[data-testid='booking-new-category-name']").Change("Haushaltsausgleich");
        dialogs.Find("[data-testid='booking-new-category-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Single(Categories.Created));
        Assert.Equal("Haushaltsausgleich", Categories.Created[0].Name);
        Assert.Equal(Anna, Assert.Single(Categories.CreatedOwners));
    }

    // Spec 140, R2: Mit nur Kategorien Ansehen gibt es den Knopf im Dialog nicht; angelegt wird nichts.
    [Fact]
    public async Task With_categories_view_only_the_booking_dialog_has_no_new_category_button()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Book = AccessLevelKeys.Create, Accounts = AccessLevelKeys.View, Plan = AccessLevelKeys.View, Categories = AccessLevelKeys.View });

        OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-next']")));

        Assert.Empty(dialogs.FindAll("[data-testid='booking-new-category']"));
        Assert.Empty(Categories.Created);
        Assert.Empty(Categories.CreatedOwners);
    }

    // R8: Mit Konten Ansehen sieht Ben Annas Konten, kann aber nichts anlegen, ändern oder auflösen.
    [Fact]
    public async Task With_accounts_view_annas_accounts_show_with_every_change_greyed_out()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Accounts = AccessLevelKeys.View });
        var giro = new AccountResponse(Guid.NewGuid(), "Annas Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1), true, null, true, true, 100m);
        AccountApiFake.Accounts.Add(giro);

        var cut = Render<Accounts>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='account-{giro.Id}']")));

        Assert.Equal(Anna, Assert.Single(AccountApiFake.ListOwners));
        AssertLocked(cut, "accounts-add");
        AssertLocked(cut, $"account-edit-{giro.Id}");
        AssertLocked(cut, $"account-close-{giro.Id}");
        AssertLocked(cut, $"account-default-{giro.Id}");
        Assert.Equal(Reason, cut.Find($"#{GrantLock.ReasonId}").TextContent.Trim());
    }

    // R8: Mit Verträge Anlegen legt Ben einen Vertrag für Anna an; Beenden bleibt aus.
    [Fact]
    public async Task With_commitments_create_the_contracts_page_lists_annas_and_ending_stays_greyed_out()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Commitments = AccessLevelKeys.Create });
        var rent = FakeCommitmentApi.Commitment("Miete");
        CommitmentApiFake.Commitments.Add(rent);

        var cut = Render<Commitments>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='commitment-{rent.Id}']")));

        Assert.Equal(Anna, Assert.Single(CommitmentApiFake.ListOwners));
        AssertFree(cut, "commitments-add");
        AssertLocked(cut, $"commitment-edit-{rent.Id}");
        AssertLocked(cut, $"commitment-end-{rent.Id}");
    }

    // Ein Knopf im offenen Dialog, gefunden an seinem Text (die Knöpfe des Schritt-Dialogs haben keine Test-Kennung).
    private void ClickInDialog(string text) => dialogs.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    // A15: Ein offener Dialog speichert für die Person, für die er geöffnet wurde, auch wenn der Umschalter
    // inzwischen woanders steht.
    [Fact]
    public async Task A_position_dialog_opened_for_anna_saves_for_anna_after_switching_to_me()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.Create });
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.Find("[data-testid='plan-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='position-label']")));

        await cut.InvokeAsync(() => Member.Choose(null));
        Field(dialogs, "position-label").Change("Geschenk");
        Field(dialogs, "position-amount").Change("50,00");
        Field(dialogs, "position-day").Change("20");
        dialogs.Find("[data-testid='position-category']").Change(FakeCategoryApi.Restaurant.Id.ToString());
        dialogs.Find("[data-testid='position-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(PlanApiFake.WriteOwners)));
    }

    // A15
    [Fact]
    public async Task A_tick_dialog_opened_for_anna_ticks_for_anna_after_switching_to_me()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.View, Book = AccessLevelKeys.Edit, Accounts = AccessLevelKeys.View });
        var groceries = FakePlanApi.Position("Lebensmittel", 600m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = [groceries] };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.Find($"[data-testid='plan-tick-{groceries.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='tick-amount']")));

        await cut.InvokeAsync(() => Member.Choose(null));
        dialogs.Find("[data-testid='tick-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(PlanApiFake.WriteOwners)));
    }

    // A15
    [Fact]
    public async Task A_carry_over_dialog_opened_for_anna_books_for_anna_after_switching_to_me()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.Edit, Book = AccessLevelKeys.Edit, Accounts = AccessLevelKeys.View });
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            CarryOver = new PlanCarryOverResponse(230m, false, null, []),
        };
        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-carry-over-open']")));
        cut.Find("[data-testid='plan-carry-over-open']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='carry-over-amount']")));

        await cut.InvokeAsync(() => Member.Choose(null));
        dialogs.Find("[data-testid='carry-over-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(PlanApiFake.WriteOwners)));
    }

    // A15
    [Fact]
    public async Task A_booking_dialog_opened_for_anna_saves_for_anna_after_switching_to_me()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Book = AccessLevelKeys.Create, Accounts = AccessLevelKeys.View, Plan = AccessLevelKeys.View });
        var cut = OpenMonthAndNewBooking();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='booking-next']")));

        await cut.InvokeAsync(() => Member.Choose(null));
        Field(dialogs, "booking-amount").Change("42");
        BookingDialogSteps.Choose(dialogs, "booking-line-category-0", FakeCategoryApi.Groceries.Id.ToString());
        BookingDialogSteps.ToLast(dialogs);
        dialogs.Find("[data-testid='booking-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(BookingApiFake.Created).OwnerId));
        // Auch die Posten zur Wahl kamen aus Annas Plan, nicht aus dem eigenen.
        Assert.Equal(Anna, PlanApiFake.Owners[1]);
    }

    // A15
    [Fact]
    public async Task A_contract_dialog_opened_for_anna_saves_for_anna_after_switching_to_me()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Commitments = AccessLevelKeys.Create });
        var cut = Render<Commitments>();
        cut.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(CommitmentApiFake.ListOwners)));
        cut.Find("[data-testid='commitments-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-name']")));

        await cut.InvokeAsync(() => Member.Choose(null));
        Field(dialogs, "commitment-name").Change("Miete");
        Field(dialogs, "commitment-amount").Change("890");
        dialogs.Find("[data-testid='commitment-category']").Change(FakeCategoryApi.Groceries.Id.ToString());
        // Der Vertragsdialog hat drei Schritte (Spec 17): zweimal Weiter, dann Speichern.
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.StartsWith("Schritt 2", dialogs.Find("[data-testid='step-dialog-progress']").TextContent));
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Weiter").Click();
        dialogs.WaitForAssertion(() => Assert.StartsWith("Schritt 3", dialogs.Find("[data-testid='step-dialog-progress']").TextContent));
        dialogs.Find($"[data-testid='commitment-payment-method-{PaymentMethods.StandingOrder}']").Change(true);
        dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Speichern").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(CommitmentApiFake.Created).OwnerId));
    }

    // A15
    [Fact]
    public async Task An_account_dialog_opened_for_anna_saves_for_anna_after_switching_to_me()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Accounts = AccessLevelKeys.Create });
        var cut = Render<Accounts>();
        cut.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(AccountApiFake.ListOwners)));
        cut.Find("[data-testid='accounts-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='account-save']")));

        await cut.InvokeAsync(() => Member.Choose(null));
        Field(dialogs, "account-name").Change("Giro");
        Field(dialogs, "account-opening-balance").Change("1250");
        Field(dialogs, "account-opening-date").Change("2026-10-01");
        dialogs.Find("[data-testid='account-save']").Click();

        dialogs.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(AccountApiFake.Created).OwnerId));
    }

    // A18: Übernehmen, das einen Posten entfernt, braucht Löschen; ohne die Stufe ist es ausgegraut, ebenso
    // "Alle übernehmen", solange ein solcher Unterschied in der Liste steht. Einen neuen Vertrag übernimmt Ben.
    [Fact]
    public async Task Without_plan_delete_removing_changes_and_apply_all_are_greyed_out()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.Edit, Commitments = AccessLevelKeys.View });
        var rent = FakePlanApi.Position("Miete", 890m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [rent],
            Changes =
            [
                new PlanChangeResponse(PlanChangeKinds.NoLongerDue, rent.Id, rent.CommitmentId!.Value, "Miete", [], Protected: false),
                new PlanChangeResponse(PlanChangeKinds.New, null, Guid.NewGuid(), "Fitness", [], Protected: false),
            ],
        };

        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='plan-change-apply']").Count));

        var applies = cut.FindAll("[data-testid='plan-change-apply']");
        Assert.Equal("true", applies[0].GetAttribute("aria-disabled"));
        Assert.Equal(GrantLock.ReasonId, applies[0].GetAttribute("aria-describedby"));
        Assert.NotEqual("true", applies[1].GetAttribute("aria-disabled"));
        AssertLocked(cut, "plan-changes-apply-all");
        Assert.Equal(Reason, cut.Find($"#{GrantLock.ReasonId}").TextContent.Trim());

        cut.FindAll("[data-testid='plan-change-apply']")[0].Click();
        cut.Find("[data-testid='plan-changes-apply-all']").Click();
        Assert.Empty(PlanApiFake.AppliedChanges);

        cut.FindAll("[data-testid='plan-change-apply']")[1].Click();
        cut.WaitForAssertion(() => Assert.Single(PlanApiFake.AppliedChanges));
    }

    // A18: Mit Löschen sind beide frei.
    [Fact]
    public async Task With_plan_delete_removing_changes_and_apply_all_are_free()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.Delete, Commitments = AccessLevelKeys.View });
        var rent = FakePlanApi.Position("Miete", 890m);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [rent],
            Changes = [new PlanChangeResponse(PlanChangeKinds.NoLongerDue, rent.Id, rent.CommitmentId!.Value, "Miete", [], Protected: false)],
        };

        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-change-apply']")));

        AssertFree(cut, "plan-change-apply");
        AssertFree(cut, "plan-changes-apply-all");
    }

    // A17: Die letzte Zahlung eines bestehenden Vertrags zu setzen oder zu leeren, beendet ihn oder holt ihn
    // zurück und braucht Löschen. Ohne die Stufe ist das Feld nur lesbar und nennt den Grund; der Rest bleibt änderbar.
    [Fact]
    public async Task Without_commitments_delete_the_last_payment_of_annas_contract_is_read_only()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Commitments = AccessLevelKeys.Edit });
        var rent = FakeCommitmentApi.Commitment("Miete");
        CommitmentApiFake.Commitments.Add(rent);
        var cut = Render<Commitments>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='commitment-edit-{rent.Id}']")));
        cut.Find($"[data-testid='commitment-edit-{rent.Id}']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-name']")));

        var lastDue = Field(dialogs, "commitment-last-due");
        Assert.NotNull(lastDue.GetAttribute("readonly"));
        Assert.Contains("commitment-last-due-reason", lastDue.GetAttribute("aria-describedby") ?? "");
        Assert.Contains(Reason, dialogs.Find("[data-testid='commitment-last-due-reason']").TextContent);
        Assert.Null(Field(dialogs, "commitment-name").GetAttribute("readonly"));
    }

    // A17: Beim Anlegen (Stufe Anlegen) und mit Löschen ist die letzte Zahlung frei.
    [Fact]
    public async Task When_creating_or_with_commitments_delete_the_last_payment_is_free()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Commitments = AccessLevelKeys.Create });
        var cut = Render<Commitments>();
        cut.WaitForAssertion(() => Assert.Equal(Anna, Assert.Single(CommitmentApiFake.ListOwners)));
        cut.Find("[data-testid='commitments-add']").Click();
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-testid='commitment-name']")));

        Assert.Null(Field(dialogs, "commitment-last-due").GetAttribute("readonly"));
        Assert.Empty(dialogs.FindAll("[data-testid='commitment-last-due-reason']"));
    }

    // R8: Zurück auf "Ich" holt die Seite die eigenen Zahlen und gibt alle Knöpfe frei.
    [Fact]
    public async Task Switching_back_to_me_reloads_my_own_month_with_every_button_free()
    {
        await ChooseAnnaAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });
        var cut = Render<Plan>();
        OpenPlanTab(cut);

        await cut.InvokeAsync(() => Member.Choose(null));

        cut.WaitForAssertion(() => Assert.Null(PlanApiFake.Owners[^1]));
        cut.WaitForAssertion(() => AssertFree(cut, "plan-add"));
        Assert.Empty(cut.FindAll($"#{GrantLock.ReasonId}"));
    }
}
