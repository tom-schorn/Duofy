using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Seite "Benachrichtigungen" (Spec 118, R1 bis R3, R10): vier Schalter, zwei Uhrzeiten, der Abend, die Vorwarnung; jede
/// Wahl gilt sofort und wird mit "Gespeichert" bestätigt; Zeiten kommen als "HH:mm" an den Server, ohne Wahl bleibt die gespeicherte.
/// Die Tests des Zeitfelds stammen aus Issue 111 und stehen jetzt hier.
/// </summary>
public class NotificationsPageTests : PageTestContext
{
    private IRenderedComponent<Notifications> Open()
    {
        ReminderApiFake.Available = true;
        var cut = Render<Notifications>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='notifications']")));
        return cut;
    }

    private static string Checked(IRenderedComponent<Notifications> cut, string id) =>
        cut.Find($"[data-testid='{id}']").GetAttribute("aria-checked")!;

    // A1
    [Fact]
    public void A_new_person_sees_all_four_switches_on_with_the_default_times_and_warning_days()
    {
        var cut = Open();

        foreach (var id in new[] { "notify-due-today", "notify-overdue", "notify-advance", "notify-household", "notify-evening-enabled" })
        {
            Assert.Equal("true", Checked(cut, id));
        }
        Assert.Equal("09:00", cut.Find("[data-testid='notify-morning-time']").GetAttribute("value"));
        Assert.Equal("19:00", cut.Find("[data-testid='notify-evening-time']").GetAttribute("value"));
        Assert.Equal("2", cut.Find("[data-testid='notify-warning-days']").GetAttribute("value"));
        Assert.Equal(7, cut.FindAll("[data-testid='notify-warning-days'] option").Count);
    }

    [Fact]
    public void Without_reminders_on_the_server_the_page_shows_no_settings()
    {
        ReminderApiFake.Available = false;

        var cut = Render<Notifications>();

        Assert.Empty(cut.FindAll("[data-testid='notifications']"));
    }

    [Fact]
    public void Each_switch_the_evening_and_the_warning_days_are_saved_at_once_and_confirmed()
    {
        var cut = Open();

        cut.Find("[data-testid='notify-household']").Click();
        cut.WaitForAssertion(() => Assert.Equal(new UpdateReminderSettingsRequest(Household: false), Assert.Single(ReminderApiFake.Updates)));
        cut.WaitForAssertion(() => Assert.Equal("false", Checked(cut, "notify-household")));
        Assert.Contains("Gespeichert", cut.Find("[data-testid='notifications']").TextContent);

        cut.Find("[data-testid='notify-evening-enabled']").Click();
        cut.WaitForAssertion(() => Assert.Equal(new UpdateReminderSettingsRequest(EveningEnabled: false), ReminderApiFake.Updates[1]));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='notify-evening-time']")));

        cut.Find("[data-testid='notify-warning-days']").Change("5");
        cut.WaitForAssertion(() => Assert.Equal(new UpdateReminderSettingsRequest(WarningDays: 5), ReminderApiFake.Updates[2]));
    }

    // A3: Die gewählte Zeit geht als "HH:mm" und mit "Gespeichert" bestätigt.
    [Fact]
    public void A_chosen_time_is_sent_as_hours_and_minutes_and_confirmed()
    {
        var cut = Open();

        cut.Find("[data-testid='notify-morning-time']").Change("04:40");

        cut.WaitForAssertion(() => Assert.Equal(new UpdateReminderSettingsRequest(MorningTime: "04:40"), Assert.Single(ReminderApiFake.Updates)));
        cut.WaitForAssertion(() => Assert.Contains("Gespeichert", cut.Find("[data-testid='notifications']").TextContent));
        Assert.Equal("04:40", cut.Find("[data-testid='notify-morning-time']").GetAttribute("value"));
    }

    // Issue 117, A3: Das Zeitfenster liefert Sekunden mit; es geht trotzdem "HH:mm" an den Server.
    [Theory]
    [InlineData("04:40:00", "04:40")]
    [InlineData("04:40:59", "04:40")]
    [InlineData("4:40", "04:40")]
    public void A_time_with_seconds_is_sent_without_them(string chosen, string sent)
    {
        var cut = Open();

        cut.Find("[data-testid='notify-evening-time']").Change(chosen);

        cut.WaitForAssertion(() => Assert.Equal(new UpdateReminderSettingsRequest(EveningTime: sent), Assert.Single(ReminderApiFake.Updates)));
    }

    // R3: "Löschen" im Zeitfenster lässt die gespeicherte Uhrzeit stehen und zeigt einen Hinweis statt eines Fehlers.
    [Fact]
    public void Clearing_the_time_keeps_the_stored_one_and_shows_a_hint_not_an_error()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/text-input.js");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var cut = Open();

        cut.Find("[data-testid='notify-morning-time']").Change("");

        cut.WaitForAssertion(() => Assert.Contains("Die gespeicherte Uhrzeit bleibt", cut.Find("[data-testid='notifications']").TextContent));
        Assert.Empty(ReminderApiFake.Updates);
        Assert.DoesNotContain("Diese Uhrzeit gibt es nicht", cut.Find("[data-testid='notifications']").TextContent);
        Assert.Equal("09:00", cut.Find("[data-testid='notify-morning-time']").GetAttribute("value"));
        cut.WaitForAssertion(() => Assert.Contains(module.Invocations, i => i.Identifier == "setValue" && Equals(i.Arguments[1], "09:00")));
    }

    [Fact]
    public void A_refused_time_is_not_shown_as_saved_and_the_field_shows_the_stored_one_again()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/text-input.js");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        ReminderApiFake.UpdateFailure = ReminderErrorCodes.ReminderTimeInvalid;
        var cut = Open();

        cut.Find("[data-testid='notify-morning-time']").Change("25:99");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='alert']")));
        Assert.DoesNotContain("Gespeichert", cut.Find("[data-testid='notifications']").TextContent);
        Assert.Equal("09:00", cut.Find("[data-testid='notify-morning-time']").GetAttribute("value"));
    }

    // Review von Issue 111: Nur die jüngste abgelehnte Zeitänderung setzt das Feld zurück; eine ältere Antwort überschreibt keine neuere Eingabe.
    [Fact]
    public void An_older_refused_time_answer_does_not_reset_a_newer_input_but_the_newest_does()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/text-input.js");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var answers = new Dictionary<string, TaskCompletionSource>();
        ReminderApiFake.UpdateGate = request => request.MorningTime is { } time ? (answers[time] = new TaskCompletionSource()).Task : Task.CompletedTask;
        ReminderApiFake.UpdateFailure = ReminderErrorCodes.ReminderTimeInvalid;
        var cut = Open();
        int Resets() => module.Invocations.Count(i => i.Identifier == "setValue" && Equals(i.Arguments[1], "09:00"));
        var before = Resets();

        cut.Find("[data-testid='notify-morning-time']").Change("23:00");
        cut.Find("[data-testid='notify-morning-time']").Change("23:30");
        cut.WaitForAssertion(() => Assert.Equal(2, answers.Count));

        answers["23:00"].SetResult();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='alert']")));
        Assert.Equal(before, Resets());

        answers["23:30"].SetResult();
        cut.WaitForAssertion(() => Assert.Equal(before + 1, Resets()));
    }

    // Review von Issue 111: Auch die abgelehnte Antwort eines Schalters setzt das Zeitfeld nicht zurück.
    [Fact]
    public void A_refused_toggle_does_not_reset_the_time_field()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/text-input.js");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var answer = new TaskCompletionSource();
        ReminderApiFake.UpdateGate = request => request.MorningTime is not null ? answer.Task : Task.CompletedTask;
        ReminderApiFake.UpdateFailure = ReminderErrorCodes.ReminderTimeInvalid;
        var cut = Open();
        int Resets() => module.Invocations.Count(i => i.Identifier == "setValue" && Equals(i.Arguments[1], "09:00"));
        var before = Resets();

        cut.Find("[data-testid='notify-morning-time']").Change("23:00");
        cut.Find("[data-testid='notify-overdue']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='alert']")));
        Assert.Equal(before, Resets());
        answer.SetResult();
        cut.WaitForAssertion(() => Assert.Equal(before + 1, Resets()));
    }

    // N6: Die Beschriftung gehört zum Kästchen (ein Label, kein doppelter Text), und ein Klick auf den Text schaltet es um.
    [Fact]
    public void The_label_belongs_to_the_checkbox_and_its_text_is_shown_once()
    {
        var cut = Open();

        var label = cut.Find("[data-testid='notify-due-today-row']");
        Assert.Contains(label.QuerySelector("[data-testid='notify-due-today']"), label.Children.SelectMany(c => new[] { c }.Concat(c.Children)));
        Assert.Equal(1, label.TextContent.Split("Heute fällig").Length - 1);
        Assert.Null(cut.Find("[data-testid='notify-due-today']").GetAttribute("aria-label"));
    }

    // S4: Ist niemand mehr angemeldet, zeigt die Seite keine Fehlermeldung.
    [Fact]
    public void A_choice_without_a_sign_in_shows_no_message()
    {
        ReminderApiFake.UpdateFailure = Duofy.ApiClient.ClientErrorCodes.NotSignedIn;
        var cut = Open();

        cut.Find("[data-testid='notify-overdue']").Click();

        cut.WaitForAssertion(() => Assert.Single(ReminderApiFake.Updates));
        Assert.Empty(cut.FindAll("[data-testid='notifications'] [role='alert']"));
        Assert.Empty(cut.FindAll("[data-testid='notifications'] [role='status']"));
    }
}
