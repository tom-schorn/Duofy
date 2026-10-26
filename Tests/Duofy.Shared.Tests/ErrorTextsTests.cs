using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Resources;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Tests;

/// <summary>Jeder Fehlercode der API und des Clients hat einen eigenen deutschen Text.</summary>
public class ErrorTextsTests
{
    private static IStringLocalizer<SharedResources> Localizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResources>>();
    }

    public static TheoryData<string> AllCodes()
    {
        var data = new TheoryData<string>();
        foreach (var type in new[] { typeof(ErrorCodes), typeof(ClientErrorCodes) })
        {
            foreach (var field in type.GetFields().Where(f => f.IsLiteral))
            {
                data.Add((string)field.GetRawConstantValue()!);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllCodes))]
    public void Every_code_has_a_text_in_the_resource_file(string code)
    {
        var localizer = Localizer();

        var text = ErrorTexts.For(localizer, code);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.False(localizer["Error_" + code].ResourceNotFound, $"Text für {code} fehlt");
    }

    // Spec 66: Dieselbe Antwort kommt beim Löschen eines Postens und beim Wechsel zwischen Einnahme und Ausgabe;
    // der Text passt zu beidem.
    [Fact]
    public void A_position_with_bookings_explains_both_deleting_and_changing_the_kind_of_pot()
    {
        var text = ErrorTexts.For(Localizer(), ErrorCodes.PositionHasBookings);

        Assert.Equal("Auf dem Posten liegen schon Buchungen. Deshalb lässt er sich weder löschen noch zwischen Einnahme und Ausgabe wechseln.", text);
    }

    [Fact]
    public void Unknown_code_gets_the_general_text()
    {
        Assert.Equal("Etwas ist schiefgelaufen. Bitte versuche es noch einmal.", ErrorTexts.For(Localizer(), "never_heard_of_it"));
    }
}
