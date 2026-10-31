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

    // Tom 31.10.: Im Übertrag erklärt die Meldung, dass das Zielkonto zu spät startet.
    [Fact]
    public void Carry_over_explains_a_target_account_that_opens_too_late()
    {
        var text = ErrorTexts.ForCarryOver(Localizer(), ErrorCodes.DateBeforeOpening);

        Assert.Equal("Das Konto startet zu spät für diesen Übertrag. Bitte ein anderes Konto wählen.", text);
    }

    // Der allgemeine Text für normale Buchungen bleibt, wie er ist.
    [Fact]
    public void A_normal_booking_keeps_the_general_opening_text()
    {
        Assert.Equal("Das Datum muss nach dem Starttag des Kontos liegen.", ErrorTexts.For(Localizer(), ErrorCodes.DateBeforeOpening));
    }

    // Alle anderen Codes zeigen im Übertrag denselben Text wie überall.
    [Theory]
    [MemberData(nameof(AllCodes))]
    public void Carry_over_changes_only_the_opening_code(string code)
    {
        var localizer = Localizer();
        if (code == ErrorCodes.DateBeforeOpening)
        {
            Assert.NotEqual(ErrorTexts.For(localizer, code), ErrorTexts.ForCarryOver(localizer, code));
        }
        else
        {
            Assert.Equal(ErrorTexts.For(localizer, code), ErrorTexts.ForCarryOver(localizer, code));
        }
    }

    [Fact]
    public void Unknown_code_gets_the_general_text()
    {
        Assert.Equal("Etwas ist schiefgelaufen. Bitte versuche es noch einmal.", ErrorTexts.For(Localizer(), "never_heard_of_it"));
    }
}
