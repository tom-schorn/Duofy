using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 109, A6: Was die App mit einer Adresse tut, die die Ansicht laden will. Die Entscheidung ist eine reine Funktion
/// (Adresse hinein, Entscheidung heraus), damit sie mit echten Adressen prüfbar ist und nicht nur im Quelltext.
/// </summary>
public class ExternalLinkDecisionTests
{
    [Theory]
    [InlineData("https://0.0.0.0/", LinkDecision.StayInApp)]
    [InlineData("https://0.0.0.0/rechtliches/impressum", LinkDecision.StayInApp)]
    [InlineData("app://0.0.0.0/konto", LinkDecision.StayInApp)]
    [InlineData("https://plagevo.de/impressum/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://example.org/datenschutz?x=1#a", LinkDecision.OpenSystemBrowser)]
    [InlineData("HTTPS://Example.org/", LinkDecision.OpenSystemBrowser)]
    // Ein Rechner, der nur so heißt wie die App, ist nicht die App.
    [InlineData("https://0.0.0.0.evil.example/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://0.0.0.0@evil.example/", LinkDecision.OpenSystemBrowser)]
    [InlineData("http://example.org/", LinkDecision.Cancel)]
    [InlineData("http://0.0.0.0/", LinkDecision.Cancel)]
    [InlineData("javascript:alert(1)", LinkDecision.Cancel)]
    [InlineData("data:text/html,<b>x</b>", LinkDecision.Cancel)]
    [InlineData("intent://scan/#Intent;scheme=zxing;end", LinkDecision.Cancel)]
    [InlineData("file:///sdcard/secret.txt", LinkDecision.Cancel)]
    [InlineData("content://contacts/people", LinkDecision.Cancel)]
    [InlineData("mailto:a@example.org", LinkDecision.Cancel)]
    [InlineData("tel:+4912345", LinkDecision.Cancel)]
    [InlineData("app://evil.example/", LinkDecision.Cancel)]
    [InlineData("//example.org/", LinkDecision.Cancel)]
    [InlineData("/rechtliches/impressum", LinkDecision.Cancel)]
    [InlineData("", LinkDecision.Cancel)]
    [InlineData(null, LinkDecision.Cancel)]
    public void An_address_leads_to_exactly_one_decision(string? url, LinkDecision expected) =>
        Assert.Equal(expected, ExternalLinks.Decide(url));
}
