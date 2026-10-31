using Duofy.Shared.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Tests;

/// <summary>Fund N5: Texte, die keine Seite mehr benutzt, stehen nicht mehr in den Ressourcen.</summary>
public class MonthResourcesTests
{
    [Theory]
    [InlineData("Month_DueOpen")]
    [InlineData("Month_ReadOnly")]
    public void A_text_no_page_uses_is_gone(string key)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var localizer = services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResources>>();

        Assert.True(localizer[key].ResourceNotFound);
    }
}
