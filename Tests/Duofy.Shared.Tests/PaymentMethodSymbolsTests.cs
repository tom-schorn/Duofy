using AngleSharp.Dom;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Zahlungsart als Symbol-Knöpfe mit Text darunter (Issue 15): eine Gruppe von Optionsfeldern wie die Kontoauswahl,
/// mit Symbol je Zahlungsart, ohne dass die Wahl nur an der Farbe hängt.
/// </summary>
public class PaymentMethodSymbolsTests : PageTestContext
{
    private IRenderedComponent<CommitmentPaymentMethodSelect> RenderSelect(string value, Action<string>? changed = null) =>
        Render<CommitmentPaymentMethodSelect>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.InputId, "pm-input")
            .Add(x => x.TestId, "pm")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => changed?.Invoke(v))));

    private static string AppCss => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css"));

    [Fact]
    public void Every_payment_method_has_a_symbol_that_exists_in_the_collection()
    {
        foreach (var method in PaymentMethods.All)
        {
            Assert.Matches("^[a-z0-9-]+$", PaymentMethodIconSet.SymbolName(method));
        }
        Assert.Equal(PaymentMethods.All.Count, PaymentMethods.All.Select(PaymentMethodIconSet.SymbolName).Distinct().Count());
    }

    [Fact]
    public void An_unknown_method_gets_the_symbol_of_other()
    {
        Assert.Equal(PaymentMethodIconSet.SymbolName(PaymentMethods.Other), PaymentMethodIconSet.SymbolName("gibt_es_nicht"));
        Assert.Equal(PaymentMethodIconSet.SymbolName(PaymentMethods.Other), PaymentMethodIconSet.SymbolName(null));
    }

    [Fact]
    public void The_select_is_a_labelled_group_of_radio_buttons_with_symbol_and_text_for_each_method()
    {
        var cut = RenderSelect("");

        var group = cut.Find("[data-testid='pm']");
        Assert.Equal("FIELDSET", group.TagName);
        Assert.Equal("Zahlungsart", group.QuerySelector("legend")!.TextContent.Trim());
        var options = group.QuerySelectorAll("label.payment-option").ToList();
        Assert.Equal(["Lastschrift", "Dauerauftrag", "Überweisung", "Abhebung", "Sonstiges"],
            options.Select(o => o.QuerySelector(".payment-option-name")!.TextContent.Trim()));
        Assert.Equal(PaymentMethods.All, options.Select(o => o.QuerySelector("[data-icon]")!.GetAttribute("data-icon")));
        Assert.All(options, o => Assert.NotNull(o.QuerySelector("svg")));
        Assert.Single(group.QuerySelectorAll("input[type='radio']").Select(r => r.GetAttribute("name")).Distinct());
    }

    [Fact]
    public void Without_a_value_nothing_is_chosen_and_a_stored_value_is_shown_as_chosen()
    {
        Assert.Empty(RenderSelect("").FindAll("input[type='radio'][checked]"));

        var chosen = RenderSelect(PaymentMethods.Withdrawal).FindAll("input[type='radio'][checked]");
        Assert.Equal(PaymentMethods.Withdrawal, Assert.Single(chosen).GetAttribute("value"));
    }

    [Fact]
    public void Choosing_a_method_reports_its_key()
    {
        var reported = new List<string>();
        var cut = RenderSelect("", reported.Add);

        cut.Find($"input[value='{PaymentMethods.Transfer}']").Change(true);
        cut.Find($"input[value='{PaymentMethods.DirectDebit}']").Change(true);

        Assert.Equal([PaymentMethods.Transfer, PaymentMethods.DirectDebit], reported);
    }

    [Fact]
    public void The_symbols_are_decoration_because_the_text_always_stands_below()
    {
        var cut = RenderSelect("");

        Assert.All(cut.FindAll("i.rzi"), s => Assert.Equal("true", s.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void The_style_marks_the_chosen_option_not_by_colour_alone_and_shows_the_focus()
    {
        var css = AppCss;

        Assert.Contains("overflow-wrap: anywhere", css[css.IndexOf(".payment-option-name", StringComparison.Ordinal)..][..200]);
        var chosen = css[css.IndexOf(".payment-option:has(input:checked) {", StringComparison.Ordinal)..][..200];
        Assert.Contains("font-weight", chosen);
        Assert.Contains("box-shadow: inset", chosen);
        Assert.Contains(":has(input:focus-visible)", css);
    }
}
