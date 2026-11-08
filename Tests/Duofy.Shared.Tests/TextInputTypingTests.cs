using Bunit;
using Duofy.Shared.Blocks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue 111: Im Web (Blazor Server) kommt die Antwort des Servers auf ein Zeichen erst an, wenn der Nutzer schon weitergetippt hat.
/// Schreibt das Feld diesen älteren Wert zurück, gehen Zeichen verloren und ein Passwort kommt verstümmelt an. Das Feld darf den
/// Browser deshalb nur dann überschreiben, wenn der Wert von außen kommt und nicht das Echo des eigenen Tippens ist.
/// </summary>
public class TextInputTypingTests : UiTestContext
{
    private const string Module = "./_content/Duofy.Shared/text-input.js";

    private IRenderedComponent<TextInput> Open(string? value, List<string> typed) =>
        Render<TextInput>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string>(this, typed.Add)));

    // Eine Seite wie @bind: sie nimmt jeden Wert an (Accept) oder lehnt ihn ab und zeichnet den alten neu.
    private sealed class Host : ComponentBase
    {
        public string Current = "";
        public bool Accept = true;
        public int Reset;
        public List<string> Typed { get; } = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<TextInput>(0);
            builder.AddAttribute(1, nameof(TextInput.Value), Current);
            builder.AddAttribute(2, nameof(TextInput.ValueChanged), EventCallback.Factory.Create<string>(this, Take));
            builder.AddAttribute(3, nameof(TextInput.Reset), Reset);
            builder.CloseComponent();
        }

        private void Take(string text)
        {
            Typed.Add(text);
            if (Accept)
            {
                Current = text;
            }
            else
            {
                Reset++;
            }
        }
    }

    [Fact]
    public void What_the_page_hands_back_while_typing_does_not_overwrite_the_field()
    {
        var cut = Render<Host>();
        cut.Find("input").Focus();

        // Drei Zeichen schnell nacheinander; die Seite gibt jedes zurück, das Feld behält, was dasteht.
        cut.Find("input").Input("a");
        cut.Find("input").Input("ab");
        cut.Find("input").Input("abc");

        Assert.Equal(new[] { "a", "ab", "abc" }, cut.Instance.Typed);
        Assert.Equal("", cut.Find("input").GetAttribute("value"));
    }

    // Review von Issue 111: Lehnt die Seite einen Wert ab und zeichnet den alten Wert neu, muss er ins Feld des Browsers.
    [Fact]
    public void A_refused_value_is_reset_in_the_field_even_though_the_page_value_did_not_change()
    {
        var module = JSInterop.SetupModule(Module);
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var cut = Render<Host>();
        cut.Instance.Accept = false;

        cut.Find("input").Input("25:99");

        cut.WaitForAssertion(() => Assert.Contains(module.Invocations, i => i.Identifier == "setValue" && Equals(i.Arguments[1], "")));
    }

    [Fact]
    public void An_accepted_value_is_not_pushed_back_into_the_field()
    {
        var module = JSInterop.SetupModule(Module);
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var cut = Render<Host>();

        cut.Find("input").Input("ok");
        cut.Find("input").Input("okay");

        Assert.DoesNotContain(module.Invocations, i => i.Identifier == "setValue");
    }

    [Fact]
    public void Every_typed_value_is_still_reported_in_order()
    {
        var typed = new List<string>();
        var cut = Open("", typed);

        foreach (var text in new[] { "p", "pa", "pas", "pass" })
        {
            cut.Find("input").Input(text);
        }
        cut.Find("input").Change("pass");

        Assert.Equal("pass", typed[^1]);
        Assert.Equal(new[] { "p", "pa", "pas", "pass" }, typed.Take(4));
    }

    [Fact]
    public void A_value_set_from_outside_reaches_the_field()
    {
        var module = JSInterop.SetupModule(Module);
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        var typed = new List<string>();
        var cut = Open("alt", typed);
        cut.Find("input").Input("tipp");

        // Die Seite setzt das Feld zurück (zum Beispiel nach dem Absenden).
        cut.Render(p => p.Add(x => x.Value, ""));

        cut.WaitForAssertion(() => Assert.Contains(module.Invocations, i => i.Identifier == "setValue"));
        Assert.Equal("", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void The_initial_value_is_shown()
    {
        var cut = Open("start", new List<string>());

        Assert.Equal("start", cut.Find("input").GetAttribute("value"));
    }
}
