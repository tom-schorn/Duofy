using System.Reflection;
using Bunit;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Impressum, Datenschutz und Nutzungsbedingungen in der App: die Seiten und die Links in "Mehr" (Spec 70, R7 und R8).</summary>
public class LegalPagesTests : PageTestContext
{
    // A13
    [Fact]
    public void Mehr_links_only_the_configured_documents_with_their_german_names()
    {
        LegalApiFake.Documents["imprint"] = "Max Muster";
        LegalApiFake.Documents["privacy"] = "Wir speichern wenig.";

        var cut = Render<Mehr>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-legal-imprint']")));
        var imprint = cut.Find("[data-testid='more-legal-imprint']");
        var privacy = cut.Find("[data-testid='more-legal-privacy']");
        Assert.Equal("/rechtliches/impressum", imprint.GetAttribute("href"));
        Assert.EndsWith("Impressum", imprint.TextContent.Trim());
        Assert.Equal("/rechtliches/datenschutz", privacy.GetAttribute("href"));
        Assert.EndsWith("Datenschutz", privacy.TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid='more-legal-terms']"));
        Assert.DoesNotContain("Nutzungsbedingungen", cut.Markup);
    }

    [Fact]
    public void Mehr_links_the_terms_as_nutzungsbedingungen()
    {
        LegalApiFake.Documents["terms"] = "Bedingungen";

        var cut = Render<Mehr>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='more-legal-terms']")));
        var terms = cut.Find("[data-testid='more-legal-terms']");
        Assert.Equal("/rechtliches/nutzungsbedingungen", terms.GetAttribute("href"));
        Assert.EndsWith("Nutzungsbedingungen", terms.TextContent.Trim());
    }

    [Fact]
    public void Mehr_shows_no_legal_links_when_nothing_is_configured_or_the_server_does_not_answer()
    {
        var none = Render<Mehr>();
        Assert.Empty(none.FindAll("[data-testid^='more-legal-']"));

        LegalApiFake.Failure = Duofy.ApiClient.ClientErrorCodes.ServerUnreachable;
        var unreachable = Render<Mehr>();
        Assert.Empty(unreachable.FindAll("[data-testid^='more-legal-']"));
        Assert.NotEmpty(unreachable.FindAll("[data-testid='more-licenses']"));
    }

    // A13
    [Fact]
    public void The_imprint_page_shows_the_text_and_renders_markup_as_literal_text()
    {
        LegalApiFake.Documents["imprint"] = "Max Muster\nMusterweg <b>1</b>\n\n<b>x</b>";
        Navigation.NavigateTo("/rechtliches/impressum");

        var cut = Render<Legal>(p => p.Add(l => l.Slug, "impressum"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-text']")));
        var text = cut.Find("[data-testid='legal-text']");
        Assert.Equal("Max Muster\nMusterweg <b>1</b>\n\n<b>x</b>", text.TextContent);
        Assert.Empty(text.QuerySelectorAll("b"));
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", cut.Markup);
        Assert.Contains("plain-text", text.ClassList);
        Assert.Equal(["imprint"], LegalApiFake.Read);
    }

    [Theory]
    [InlineData("impressum", "imprint", "Impressum")]
    [InlineData("datenschutz", "privacy", "Datenschutz")]
    [InlineData("nutzungsbedingungen", "terms", "Nutzungsbedingungen")]
    public void Each_address_reads_its_own_document_and_carries_its_name_as_title(string slug, string name, string title)
    {
        LegalApiFake.Documents[name] = "Text von " + name;

        var cut = Render<Legal>(p => p.Add(l => l.Slug, slug));

        cut.WaitForAssertion(() => Assert.Equal("Text von " + name, cut.Find("[data-testid='legal-text']").TextContent));
        Assert.Contains(title, cut.Find("h1,h4").TextContent);
        Assert.Equal([name], LegalApiFake.Read);
    }

    [Fact]
    public void A_document_the_server_does_not_have_says_so_without_asking_for_it_twice()
    {
        var cut = Render<Legal>(p => p.Add(l => l.Slug, "datenschutz"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-missing']")));
        Assert.Contains("Diesen Text gibt es auf diesem Server nicht.", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='legal-text']"));
    }

    [Fact]
    public void An_unknown_address_never_reaches_the_server_and_says_the_same()
    {
        var cut = Render<Legal>(p => p.Add(l => l.Slug, "../konto"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-missing']")));
        Assert.Empty(LegalApiFake.Read);
    }

    [Fact]
    public void An_unreachable_server_shows_the_error_text()
    {
        LegalApiFake.Failure = Duofy.ApiClient.ClientErrorCodes.ServerUnreachable;

        var cut = Render<Legal>(p => p.Add(l => l.Slug, "impressum"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(AlertSelector)));
        Assert.Empty(cut.FindAll("[data-testid='legal-text']"));
    }

    // R7: lesbar ohne Anmeldung, also keine Anmeldepflicht an der Seite; die Adressen stehen fest.
    [Fact]
    public void The_page_needs_no_sign_in_and_has_the_three_german_addresses()
    {
        Assert.Null(typeof(Legal).GetCustomAttribute<AuthorizeAttribute>());
        var routes = typeof(Legal).GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToArray();
        Assert.Equal(["/rechtliches/{Slug}"], routes);
    }

    // Radzen-Umstellung R13
    [Fact]
    public void The_legal_page_is_built_with_radzen_for_its_heading_and_its_missing_text()
    {
        Navigation.NavigateTo("/rechtliches/impressum");

        var cut = Render<Legal>(p => p.Add(l => l.Slug, "impressum"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='legal-missing']")));
        Assert.Contains(cut.Find("h1").ClassList, c => c.StartsWith("rz-text"));
        Assert.Contains(cut.Find("[data-testid='legal-missing']").ClassList, c => c.StartsWith("rz-text"));
    }
}
