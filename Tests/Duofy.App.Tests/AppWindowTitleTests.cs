namespace Duofy.App.Tests;

/// <summary>Der Titel des Fensters folgt der Marke des gewählten Servers (Plagevo oder Duofy), nicht einem festen Namen.</summary>
public class AppWindowTitleTests
{
    [Fact]
    public void The_window_title_comes_from_the_brand()
    {
        var source = File.ReadAllText(RepositoryFiles.Path("Mobile", "Duofy", "App.xaml.cs"));

        Assert.Contains("Title = brand.Name", source);
        Assert.DoesNotContain("Title = \"Duofy\"", source);
    }
}
