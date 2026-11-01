using Bunit;
using Duofy.Shared.Layout;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Duofy.Shared.Tests;

/// <summary>
/// Mangel S6: Nach einer Aktion darf der Fokus nicht auf BODY fallen. Eine Meldung auf der Seite holt ihn zurück, wenn er verloren
/// ging (die Zeile oder der Knopf ist weg), und ein Fehler im Dialog nimmt ihn gleich selbst, damit man ihn hört und lesen kann.
/// </summary>
public class FocusAfterActionTests : UiTestContext
{
    private const string Module = "./_content/Duofy.Shared/focus-notice.js";

    [Fact]
    public void A_new_notice_asks_the_browser_to_focus_it_when_the_focus_was_lost()
    {
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;

        var cut = Render<Notice>(p => p.Add(n => n.Message, new NoticeMessage("Der Posten wurde gelöscht.", NoticeSeverity.Success)));

        var box = cut.Find("[data-testid='notice']");
        Assert.Equal("-1", box.GetAttribute("tabindex"));
        var call = Assert.Single(module.Invocations, i => i.Identifier == "focusIfLost");
        Assert.Equal(box.GetAttribute("blazor:elementreference"), ((ElementReference)call.Arguments[0]!).Id);
    }

    [Fact]
    public void The_same_notice_is_not_focused_twice_and_a_new_one_is()
    {
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;
        var first = new NoticeMessage("Eins", NoticeSeverity.Success);
        var cut = Render<Notice>(p => p.Add(n => n.Message, first));

        cut.Render(p => p.Add(n => n.Message, first));
        Assert.Single(module.Invocations, i => i.Identifier == "focusIfLost");

        cut.Render(p => p.Add(n => n.Message, new NoticeMessage("Zwei", NoticeSeverity.Error)));
        Assert.Equal(2, module.Invocations.Count(i => i.Identifier == "focusIfLost"));
    }

    [Fact]
    public void An_error_in_a_dialog_takes_the_focus_when_it_appears()
    {
        var cut = Render<ErrorAlert>(p => p.Add(e => e.Text, null));
        Assert.Empty(cut.FindAll("[data-testid='dialog-error']"));

        cut.Render(p => p.Add(e => e.Text, "Das hat nicht geklappt."));

        var box = cut.Find("[data-testid='dialog-error']");
        Assert.Contains("Das hat nicht geklappt.", box.TextContent);
        var focused = JSInterop.VerifyFocusAsyncInvoke();
        Assert.Equal(cut.Find("[tabindex='-1']").GetAttribute("blazor:elementreference"), ((ElementReference)focused.Arguments[0]!).Id);
    }
}
