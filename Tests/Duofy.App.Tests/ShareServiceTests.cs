namespace Duofy.App.Tests;

/// <summary>
/// Teilen in der App (Spec 67, A17): Die App öffnet mit dem Einladungslink den Teilen-Dialog von Android.
/// Die App selbst lässt sich hier nicht starten; geprüft wird, dass sie den Dienst anmeldet und dass er
/// den Link an MAUI Share übergibt.
/// </summary>
public class ShareServiceTests
{
    [Fact]
    public void The_app_registers_the_maui_share_service()
    {
        var program = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "MauiProgram.cs"));

        Assert.Contains("AddSingleton<IShareService, MauiShareService>()", program);
    }

    [Fact]
    public void The_share_service_hands_the_link_to_the_android_share_dialog()
    {
        var service = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "Services", "MauiShareService.cs"));

        Assert.Contains("Share.Default.RequestAsync(new ShareTextRequest", service);
        Assert.Contains("Uri = link", service);
        Assert.Contains("Title = title", service);
    }
}
