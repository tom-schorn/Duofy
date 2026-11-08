using Bunit;
using Duofy.Shared.Pages;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Seite Mehr (R14): jeder Eintrag führt zum selben Ziel wie vorher, Admin nur für Verwaltende. Seit Spec 82 R12 hat sie
/// keinen Haushalt-Eintrag mehr (der Haushalt hat sein Ziel unten), dafür oben das Profil mit der Auswahl der Person.
/// </summary>
public class MehrPageTests : PageTestContext
{
    [Fact]
    public void Every_entry_leads_to_the_same_target()
    {
        LegalApiFake.Documents["imprint"] = "Max Muster";
        Users.Me = Users.Me with { IsInstanceAdmin = true };

        var cut = Render<Mehr>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-admin']")));
        Assert.Equal("/konten", cut.Find("[data-testid='more-accounts']").GetAttribute("href"));
        Assert.Equal("/vertraege", cut.Find("[data-testid='more-contracts']").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='more-household']"));
        Assert.Equal("/konto", cut.Find("[data-testid='more-user-account']").GetAttribute("href"));
        Assert.Equal("/admin", cut.Find("[data-testid='more-admin']").GetAttribute("href"));
        Assert.Equal("/lizenzen", cut.Find("[data-testid='more-licenses']").GetAttribute("href"));
        Assert.Equal("/rechtliches/impressum", cut.Find("[data-testid='more-legal-imprint']").GetAttribute("href"));
        Assert.Contains("card", cut.Find("[data-testid='more-list']").ClassList);
    }

    // Review 07.11.: Mit einer äußeren Adresse führt der Eintrag dorthin; ohne Adresse zur Seite des Servers.
    [Fact]
    public void A_legal_entry_uses_the_external_address_of_its_text_when_there_is_one()
    {
        LegalApiFake.Documents["imprint"] = "Max Muster";
        LegalApiFake.Urls["privacy"] = "https://example.org/datenschutz";

        var cut = Render<Mehr>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-legal-privacy']")));
        Assert.Equal("https://example.org/datenschutz", cut.Find("[data-testid='more-legal-privacy']").GetAttribute("href"));
        Assert.Equal("/rechtliches/impressum", cut.Find("[data-testid='more-legal-imprint']").GetAttribute("href"));
    }

    [Fact]
    public void Sign_out_leads_to_the_sign_in()
    {
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-sign-out']")));

        cut.Find("[data-testid='more-sign-out']").Click();

        cut.WaitForAssertion(() => Assert.Equal("/anmelden", CurrentPath));
        Assert.Null(Tokens.Tokens);
    }

    // Spec 84, A9: Ohne Erinnerungen auf dem Server (404) gibt es den Abschnitt nicht.
    [Fact]
    public void Without_reminders_on_the_server_the_section_is_hidden()
    {
        ReminderApiFake.Available = false;
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-sign-out']")));

        Assert.Empty(cut.FindAll("[data-testid='more-reminders']"));
    }

    // Spec 84, R2: Der Abschnitt zeigt an/aus und die Uhrzeit; jede Wahl gilt sofort.
    [Fact]
    public void The_section_shows_the_stored_settings_and_saves_each_choice_at_once()
    {
        ReminderApiFake.Available = true;
        ReminderApiFake.Settings = new Duofy.Schemas.ReminderSettingsResponse(true, "07:30");
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-reminders']")));

        Assert.Equal("07:30", cut.Find("[data-testid='more-reminders-time']").GetAttribute("value"));
        Assert.Equal("true", cut.Find("[data-testid='more-reminders-enabled']").GetAttribute("aria-checked"));

        cut.Find("[data-testid='more-reminders-time']").Change("08:15");
        cut.WaitForAssertion(() => Assert.Equal(new Duofy.Schemas.UpdateReminderSettingsRequest(Time: "08:15"), Assert.Single(ReminderApiFake.Updates)));

        cut.Find("[data-testid='more-reminders-enabled']").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, ReminderApiFake.Updates.Count));
        Assert.Equal(new Duofy.Schemas.UpdateReminderSettingsRequest(Enabled: false), ReminderApiFake.Updates[1]);
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("[data-testid='more-reminders-enabled']").GetAttribute("aria-checked")));
    }

    [Fact]
    public void A_refused_time_is_not_shown_as_saved()
    {
        ReminderApiFake.Available = true;
        ReminderApiFake.UpdateFailure = Duofy.Schemas.ReminderErrorCodes.ReminderTimeInvalid;
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-reminders']")));

        cut.Find("[data-testid='more-reminders-time']").Change("25:99");

        cut.WaitForAssertion(() => Assert.Equal("09:00", cut.Find("[data-testid='more-reminders-time']").GetAttribute("value")));
        Assert.NotEmpty(cut.FindAll("[role='alert']"));
    }

    // Review von Issue 111: Nach einer abgelehnten Zeit steht im Browser-Feld wieder die gespeicherte, nicht nur im value-Attribut.
    [Fact]
    public void A_refused_time_is_put_back_into_the_field_of_the_browser()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/text-input.js");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        ReminderApiFake.Available = true;
        ReminderApiFake.UpdateFailure = Duofy.Schemas.ReminderErrorCodes.ReminderTimeInvalid;
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-reminders']")));

        cut.Find("[data-testid='more-reminders-time']").Change("25:99");

        cut.WaitForAssertion(() => Assert.Contains(module.Invocations, i => i.Identifier == "setValue" && Equals(i.Arguments[1], "09:00")));
    }

    // Review von Issue 111: Nur die jüngste abgelehnte Zeitänderung setzt das Feld zurück; eine ältere Antwort überschreibt keine neuere Eingabe.
    [Fact]
    public void An_older_refused_time_answer_does_not_reset_a_newer_input_but_the_newest_does()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/text-input.js");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var answers = new Dictionary<string, TaskCompletionSource>();
        ReminderApiFake.UpdateGate = request => request.Time is { } time ? (answers[time] = new TaskCompletionSource()).Task : Task.CompletedTask;
        ReminderApiFake.Available = true;
        ReminderApiFake.UpdateFailure = Duofy.Schemas.ReminderErrorCodes.ReminderTimeInvalid;
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-reminders']")));
        int Resets() => module.Invocations.Count(i => i.Identifier == "setValue" && Equals(i.Arguments[1], "09:00"));
        var before = Resets();

        cut.Find("[data-testid='more-reminders-time']").Change("25:00");
        cut.Find("[data-testid='more-reminders-time']").Change("26:00");
        cut.WaitForAssertion(() => Assert.Equal(2, answers.Count));

        answers["25:00"].SetResult();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='alert']")));
        Assert.Equal(before, Resets());

        answers["26:00"].SetResult();
        cut.WaitForAssertion(() => Assert.Equal(before + 1, Resets()));
    }

    // Review von Issue 111: Auch die abgelehnte Antwort eines Schalters setzt das Zeitfeld nicht zurück.
    [Fact]
    public void A_refused_toggle_does_not_reset_the_time_field()
    {
        var module = JSInterop.SetupModule("./_content/Duofy.Shared/text-input.js");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var answer = new TaskCompletionSource();
        ReminderApiFake.UpdateGate = request => request.Time is not null ? answer.Task : Task.CompletedTask;
        ReminderApiFake.Available = true;
        ReminderApiFake.UpdateFailure = Duofy.Schemas.ReminderErrorCodes.ReminderTimeInvalid;
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-reminders']")));
        int Resets() => module.Invocations.Count(i => i.Identifier == "setValue" && Equals(i.Arguments[1], "09:00"));
        var before = Resets();

        cut.Find("[data-testid='more-reminders-time']").Change("25:00");
        cut.Find("[data-testid='more-reminders-enabled']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='alert']")));
        Assert.Equal(before, Resets());
        answer.SetResult();
        cut.WaitForAssertion(() => Assert.Equal(before + 1, Resets()));
    }

    // N6: Die Beschriftung gehört zum Kästchen (ein Label, kein doppelter Text), und ein Klick auf den Text schaltet es um.
    [Fact]
    public void The_label_belongs_to_the_checkbox_and_its_text_is_shown_once()
    {
        ReminderApiFake.Available = true;
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-reminders']")));

        var label = cut.Find("[data-testid='more-reminders'] label");
        Assert.Contains(label.QuerySelector("[data-testid='more-reminders-enabled']"), label.Children);
        Assert.Equal(1, cut.Find("[data-testid='more-reminders']").TextContent.Split("Erinnerungen einschalten").Length - 1);
        Assert.Null(cut.Find("[data-testid='more-reminders-enabled']").GetAttribute("aria-label"));
    }

    // S4: Ist niemand mehr angemeldet, zeigt die Seite keine Fehlermeldung.
    [Fact]
    public void A_choice_without_a_sign_in_shows_no_message()
    {
        ReminderApiFake.Available = true;
        ReminderApiFake.UpdateFailure = Duofy.ApiClient.ClientErrorCodes.NotSignedIn;
        var cut = Render<Mehr>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-reminders']")));

        cut.Find("[data-testid='more-reminders-enabled']").Click();

        cut.WaitForAssertion(() => Assert.Single(ReminderApiFake.Updates));
        Assert.Empty(cut.FindAll("[data-testid='more-reminders'] [role='alert']"));
        Assert.Empty(cut.FindAll("[data-testid='more-reminders'] [role='status']"));
    }
}
