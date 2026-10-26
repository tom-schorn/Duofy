using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace Duofy.Domain;

/// <summary>Eine eingebaute Kategorie als Datensatz: fester Schlüssel, Gruppe und vorgeschlagener Topf.</summary>
public sealed record BuiltInCategory(string Key, CategoryGroup Group, Pot SuggestedPot)
{
    /// <summary>Die feste Kennung, immer dieselbe für denselben Schlüssel.</summary>
    public Guid Id => CategoryCatalog.IdFor(Key);
}

/// <summary>
/// Die eingebauten Kategorien, aus dem Original übernommen. Die Migration legt sie
/// in der Tabelle an; dieser Katalog ist die Quelle dafür.
///
/// Der Schlüssel (zum Beispiel "household.groceries") ändert sich nie, auch wenn
/// der deutsche Name später umformuliert wird. Alles, was eine eingebaute
/// Kategorie braucht, hängt am Schlüssel.
/// </summary>
public static class CategoryCatalog
{
    private static readonly ResourceManager Names =
        new("Duofy.Domain.Resources.CategoryNames", typeof(CategoryCatalog).Assembly);

    public static IReadOnlyList<BuiltInCategory> All { get; } =
    [
        new("household.groceries", CategoryGroup.Household, Pot.Needs),
        new("household.clothing", CategoryGroup.Household, Pot.Needs),
        new("household.healthcare", CategoryGroup.Household, Pot.Needs),
        new("household.personal_care", CategoryGroup.Household, Pot.Needs),
        new("household.cleaning", CategoryGroup.Household, Pot.Needs),
        new("household.pets", CategoryGroup.Household, Pot.Needs),
        new("housing.rent", CategoryGroup.Housing, Pot.Needs),
        new("housing.utilities", CategoryGroup.Housing, Pot.Needs),
        new("housing.repairs", CategoryGroup.Housing, Pot.Needs),
        new("housing.interior", CategoryGroup.Housing, Pot.Wants),
        new("housing.outdoor", CategoryGroup.Housing, Pot.Wants),
        new("housing.insurance", CategoryGroup.Housing, Pot.Needs),
        new("transport.public", CategoryGroup.Transport, Pot.Needs),
        new("transport.fuel", CategoryGroup.Transport, Pot.Needs),
        new("transport.repairs", CategoryGroup.Transport, Pot.Needs),
        new("transport.fines", CategoryGroup.Transport, Pot.Needs),
        new("transport.purchase", CategoryGroup.Transport, Pot.Wants),
        new("transport.insurance", CategoryGroup.Transport, Pot.Needs),
        new("children.care", CategoryGroup.Children, Pot.Needs),
        new("children.school", CategoryGroup.Children, Pot.Needs),
        new("children.allowance", CategoryGroup.Children, Pot.Wants),
        new("leisure.vacation", CategoryGroup.Leisure, Pot.Wants),
        new("leisure.hobbies", CategoryGroup.Leisure, Pot.Wants),
        new("leisure.entertainment", CategoryGroup.Leisure, Pot.Wants),
        new("leisure.memberships", CategoryGroup.Leisure, Pot.Wants),
        new("leisure.dining", CategoryGroup.Leisure, Pot.Wants),
        new("leisure.subscriptions", CategoryGroup.Leisure, Pot.Wants),
        new("leisure.indulgences", CategoryGroup.Leisure, Pot.Wants),
        new("personal.insurance", CategoryGroup.Personal, Pot.Needs),
        new("personal.communication", CategoryGroup.Personal, Pot.Needs),
        new("personal.work", CategoryGroup.Personal, Pot.Needs),
        new("personal.legal", CategoryGroup.Personal, Pot.Needs),
        new("personal.gifts", CategoryGroup.Personal, Pot.Wants),
        new("personal.donations", CategoryGroup.Personal, Pot.Wants),
        new("personal.education", CategoryGroup.Personal, Pot.Needs),
        new("personal.taxes", CategoryGroup.Personal, Pot.Needs),
        new("income.earned", CategoryGroup.Income, Pot.Income),
        new("income.benefits", CategoryGroup.Income, Pot.Income),
        new("income.interest", CategoryGroup.Income, Pot.Income),
        new("income.other", CategoryGroup.Income, Pot.Income),
        new("finance.savings", CategoryGroup.Finance, Pot.Savings),
        new("finance.debt", CategoryGroup.Finance, Pot.Savings),
        new("finance.investment", CategoryGroup.Finance, Pot.Wants),
        new("finance.fees", CategoryGroup.Finance, Pot.Needs),
        new("finance.settlement", CategoryGroup.Finance, Pot.Needs),
    ];

    /// <summary>
    /// Die feste Kennung zu einem Schlüssel. Sie wird aus dem Schlüssel berechnet,
    /// damit die Migration und jede Datenbank dieselben Kennungen bekommen.
    /// </summary>
    public static Guid IdFor(string key) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("duofy.category:" + key))[..16]);

    /// <summary>Der deutsche Name einer eingebauten Kategorie aus <c>Resources/CategoryNames.resx</c>.</summary>
    public static string GermanName(string key) =>
        Names.GetString(key) ?? throw new KeyNotFoundException($"Kein deutscher Name für die Kategorie '{key}'.");
}
