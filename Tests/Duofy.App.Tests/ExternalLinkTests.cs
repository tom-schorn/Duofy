namespace Duofy.App.Tests;

/// <summary>
/// Spec 109, A6: Die App fragt für jede Adresse, die die Ansicht laden will, die Funktion <c>ExternalLinks.Decide</c>.
/// Was sie entscheidet, prüfen die Tests in Duofy.Shared.Tests mit echten Adressen; hier steht nur, dass die Seite sie
/// benutzt und keinen eigenen Weg daneben hat. Das Verhalten auf dem Gerät sieht man nur im Emulator.
/// </summary>
public class ExternalLinkTests
{
    private static string MainPage() =>
        File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "MainPage.xaml.cs"));

    [Fact]
    public void A6_the_app_asks_the_decision_function_for_every_address()
    {
        var source = MainPage();

        Assert.Contains("blazorWebView.UrlLoading +=", source);
        Assert.Contains("ExternalLinks.Decide", source);
        Assert.Contains("UrlLoadingStrategy.OpenExternally", source);
        Assert.Contains("UrlLoadingStrategy.CancelLoad", source);
    }

    // Issue 111: Auf Windows liefert die Ansicht die Oberfläche unter 0.0.0.1 aus. Welche Rechnernamen der App gehören, steht nur
    // in ExternalLinks (mit Test); die Seite darf keine eigene, engere Prüfung daneben haben, die Windows aussperrt.
    [Fact]
    public void Issue_111_the_page_has_no_host_check_of_its_own_next_to_the_decision_function()
    {
        var source = MainPage();

        Assert.DoesNotContain("0.0.0.", source);
        Assert.DoesNotContain("AppHostAddressAlways0000", source);
    }
}
