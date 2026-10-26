using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Die Kontoart und ihr Vorschlag für "zählt als verfügbar" (Spec 58, R2).</summary>
public class AccountRulesTests
{
    [Theory]
    [InlineData(AccountType.Checking, true)]
    [InlineData(AccountType.Cash, true)]
    [InlineData(AccountType.Savings, false)]
    public void Type_presets_counts_as_available(AccountType type, bool expected)
    {
        Assert.Equal(expected, Account.CountsAsAvailableFor(type));
    }
}
