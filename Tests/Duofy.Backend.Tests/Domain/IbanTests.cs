using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Die IBAN eines Kontos (Spec 58, R6): ohne Leerzeichen, groß geschrieben, Prüfziffern geprüft.</summary>
public class IbanTests
{
    // Spec 58, A8
    [Fact]
    public void Spaces_are_removed_and_letters_upper_cased()
    {
        Assert.True(Iban.TryNormalize("de89 3704 0044 0532 0130 00", out var iban));
        Assert.Equal("DE89370400440532013000", iban);
    }

    // Spec 58, A8
    [Fact]
    public void Wrong_check_digits_are_rejected()
    {
        Assert.False(Iban.TryNormalize("DE89370400440532013001", out _));
    }

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    [InlineData("AT61 1904 3002 3457 3201")]
    [InlineData("NL91ABNA0417164300")]
    public void Valid_ibans_of_other_countries_pass(string input)
    {
        Assert.True(Iban.TryNormalize(input, out _));
    }

    [Theory]
    [InlineData("DE89")]                                  // zu kurz
    [InlineData("1289370400440532013000")]                // fängt nicht mit dem Land an
    [InlineData("DEAB370400440532013000")]                // Prüfziffern sind keine Ziffern
    [InlineData("DE89-3704-0044-0532-0130-00")]           // Bindestriche sind nicht erlaubt
    [InlineData("DE8937040044053201300012345678901234")]  // länger als 34 Zeichen
    public void Malformed_ibans_are_rejected(string input)
    {
        Assert.False(Iban.TryNormalize(input, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_means_no_iban(string? input)
    {
        Assert.True(Iban.IsEmpty(input));
        Assert.False(Iban.IsEmpty("DE89"));
    }
}
