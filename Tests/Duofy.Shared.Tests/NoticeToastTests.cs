using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Spec 82: Rückmeldungen wie "Der Posten ist abgehakt." erscheinen als kurze Meldung, die sich selbst schließt und nichts verschiebt.</summary>
public class NoticeToastTests : UiTestContext
{
    public NoticeToastTests()
    {
        JSInterop.SetupModule("./_content/Duofy.Shared/focus-notice.js").Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void A_success_is_a_short_status_message_that_overlays_the_page()
    {
        var cut = Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Der Posten ist abgehakt.", NoticeSeverity.Success)));

        var toast = cut.Find("[data-testid='notice']");
        Assert.Contains("notice-toast", toast.ClassList);
        Assert.Equal("status", toast.GetAttribute("role"));
        var css = File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));
        Assert.Contains("position: fixed", css[css.IndexOf(".notice-toast {", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void An_error_stays_above_the_fields()
    {
        var cut = Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Das ging nicht.", NoticeSeverity.Error)));

        Assert.DoesNotContain("notice-toast", cut.Find("[data-testid='notice']").ClassList);
    }

    [Fact]
    public void A_success_closes_itself_after_a_few_seconds()
    {
        var cut = Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Gespeichert.", NoticeSeverity.Success)));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='notice']")), Notice.ToastDuration + TimeSpan.FromSeconds(3));
    }
}
