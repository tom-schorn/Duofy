using AngleSharp.Dom;
using Bunit;
using Duofy.Shared.Pages.Month;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Pfeiltasten bei den Tabs der Monatsseite (Fund S4): Es zählt der aktive Tab, und der Fokus wandert mit, damit der
/// nächste Tastendruck beim neuen Tab ankommt.
/// </summary>
public class MonthTabsTests : PageTestContext
{
    /// <summary>Hält den gewählten Tab wie die Monatsseite.</summary>
    private sealed class Host : ComponentBase
    {
        private MonthTab active = MonthTab.Overview;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MonthTabs>(0);
            builder.AddComponentParameter(1, nameof(MonthTabs.Active), active);
            builder.AddComponentParameter(2, nameof(MonthTabs.ActiveChanged), EventCallback.Factory.Create<MonthTab>(this, tab => active = tab));
            builder.CloseComponent();
        }
    }

    private static IElement Tab(IRenderedComponent<Host> cut, string tab) => cut.Find($"[data-testid='month-tab-{tab}']");

    private static bool Selected(IRenderedComponent<Host> cut, string tab) => Tab(cut, tab).GetAttribute("aria-selected") == "true";

    private static void Press(IRenderedComponent<Host> cut, string tab, string key) => Tab(cut, tab).KeyDown(new KeyboardEventArgs { Key = key });

    // bUnit zeigt die Kennung eines Elements nur im ersten Zeichnen; deshalb wird sie vor dem ersten Tastendruck gelesen.
    private static string ReferenceOf(IRenderedComponent<Host> cut, string tab) => Tab(cut, tab).GetAttribute("blazor:elementreference")!;

    private static string FocusedReference(JSRuntimeInvocation invocation) => ((ElementReference)invocation.Arguments[0]!).Id;

    [Fact]
    public void Two_presses_on_the_same_tab_move_two_tabs_on_because_the_active_tab_counts()
    {
        var cut = Render<Host>();

        Press(cut, "overview", "ArrowRight");
        Press(cut, "overview", "ArrowRight");

        Assert.True(Selected(cut, "bookings"));
    }

    [Fact]
    public void The_focus_follows_each_press_to_the_new_tab()
    {
        var cut = Render<Host>();
        var plan = ReferenceOf(cut, "plan");
        var bookings = ReferenceOf(cut, "bookings");

        Press(cut, "overview", "ArrowRight");
        Press(cut, "plan", "ArrowRight");

        Assert.True(Selected(cut, "bookings"));
        var focus = JSInterop.VerifyFocusAsyncInvoke(2);
        Assert.Equal(plan, FocusedReference(focus[0]));
        Assert.Equal(bookings, FocusedReference(focus[1]));
    }

    [Fact]
    public void The_left_arrow_goes_round_from_the_first_tab_to_the_last()
    {
        var cut = Render<Host>();
        var bookings = ReferenceOf(cut, "bookings");

        Press(cut, "overview", "ArrowLeft");

        Assert.True(Selected(cut, "bookings"));
        Assert.Equal(bookings, FocusedReference(JSInterop.VerifyFocusAsyncInvoke()));
    }

    [Fact]
    public void A_click_or_another_key_moves_no_focus()
    {
        var cut = Render<Host>();

        Press(cut, "overview", "Tab");
        Tab(cut, "plan").Click();

        Assert.True(Selected(cut, "plan"));
        JSInterop.VerifyNotInvoke("Blazor._internal.domWrapper.focus");
    }
}
