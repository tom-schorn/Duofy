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
    // Windows: die Ansicht liefert die Oberfläche unter 0.0.0.1 aus (Issue 111).
    [InlineData("https://0.0.0.1/", LinkDecision.StayInApp)]
    [InlineData("https://0.0.0.1/rechtliches/impressum", LinkDecision.StayInApp)]
    [InlineData("app://0.0.0.1/konto", LinkDecision.StayInApp)]
    [InlineData("https://0.0.0.1.evil.example/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://0.0.0.1@evil.example/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://user@0.0.0.1/", LinkDecision.OpenSystemBrowser)]
    [InlineData("http://0.0.0.1/", LinkDecision.Cancel)]
    // Ein eigener Port oder eine andere Schreibweise des Rechnernamens ist nicht die Oberfläche der App (Review von Issue 111).
    [InlineData("https://0.0.0.1:8443/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://0.0.0.0:444/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://0x0.0.0.1/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://1/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://0/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://0.0.1/", LinkDecision.OpenSystemBrowser)]
    [InlineData("https://0.0.0.2/", LinkDecision.OpenSystemBrowser)]
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
