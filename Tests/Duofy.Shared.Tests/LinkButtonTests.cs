using Bunit;
using Duofy.Shared.Layout;
using Radzen;

namespace Duofy.Shared.Tests;

/// <summary>Ein Link im Aussehen eines Radzen-Knopfs: bleibt ein Link (Adresse, Tastatur, neuer Tab), sieht aber aus wie ein Knopf.</summary>
public class LinkButtonTests : UiTestContext
{
    [Fact]
    public void It_is_a_link_with_the_classes_of_a_radzen_button_and_passes_attributes_on()
    {
        var cut = Render<LinkButton>(p => p
            .Add(c => c.Href, "/anmelden")
            .AddUnmatched("data-testid", "to-sign-in")
            .AddChildContent("Zur Anmeldung"));

        var link = cut.Find("a[data-testid='to-sign-in']");
        Assert.Equal("/anmelden", link.GetAttribute("href"));
        Assert.Contains("rz-button", link.ClassList);
        Assert.Contains("rz-variant-filled", link.ClassList);
        Assert.Contains("rz-primary", link.ClassList);
        Assert.Equal("Zur Anmeldung", link.TextContent.Trim());
    }

    [Fact]
    public void The_kind_and_the_width_follow_the_parameters()
    {
        var cut = Render<LinkButton>(p => p
            .Add(c => c.Href, "/konto")
            .Add(c => c.Variant, Variant.Outlined)
            .Add(c => c.ButtonStyle, ButtonStyle.Base)
            .Add(c => c.FullWidth, true)
            .AddChildContent("Zurück"));

        var link = cut.Find("a");
        Assert.Contains("rz-variant-outlined", link.ClassList);
        Assert.Contains("rz-base", link.ClassList);
        Assert.Contains("rz-w-100", link.ClassList);
        Assert.Null(link.GetAttribute("style"));
    }
}
