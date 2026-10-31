using Duofy.Schemas;
using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Der Name des ersten Kontos nach der Sprache (Spec 13, R2 und R4): A2, A3, A5.</summary>
public class DefaultAccountNameTests
{
    // A2
    [Theory]
    [InlineData("de")]
    [InlineData("de-DE")]
    [InlineData("DE")]
    public void German_gives_Standardkonto(string language)
    {
        Assert.Equal("Standardkonto", DefaultAccountName.For(language));
    }

    // A3
    [Theory]
    [InlineData("en")]
    [InlineData("en-GB")]
    [InlineData("EN")]
    public void English_gives_Default_account(string language)
    {
        Assert.Equal("Default account", DefaultAccountName.For(language));
    }

    // A5
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fr")]
    [InlineData("xx-YY")]
    [InlineData("nicht-eine-sprache!")]
    public void Unknown_or_missing_language_gives_the_German_name(string? language)
    {
        Assert.Equal("Standardkonto", DefaultAccountName.For(language));
    }

    [Fact]
    public void The_name_fits_the_rules_for_account_names()
    {
        foreach (var language in new[] { "de", "en" })
        {
            var name = DefaultAccountName.For(language);
            Assert.InRange(name.Length, 1, MoneyAccountRules.NameMaxLength);
        }
    }
}
