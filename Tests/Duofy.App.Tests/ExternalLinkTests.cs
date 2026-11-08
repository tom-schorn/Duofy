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
}
