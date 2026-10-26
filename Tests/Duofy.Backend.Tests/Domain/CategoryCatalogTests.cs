using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Die eingebauten Kategorien, übernommen aus dem Original (A4).</summary>
public class CategoryCatalogTests
{
    [Fact]
    public void Catalog_holds_the_45_categories_of_the_original()
    {
        Assert.Equal(45, CategoryCatalog.All.Count);
    }

    [Fact]
    public void Keys_are_unique_and_start_with_their_group()
    {
        Assert.Equal(CategoryCatalog.All.Count, CategoryCatalog.All.Select(c => c.Key).Distinct().Count());
        foreach (var category in CategoryCatalog.All)
        {
            Assert.StartsWith(CategoryGroups.ToKey(category.Group) + ".", category.Key);
        }
    }

    [Fact]
    public void Groceries_suggest_the_pot_needs()
    {
        var groceries = CategoryCatalog.All.Single(c => c.Key == "household.groceries");

        Assert.Equal(Pot.Needs, groceries.SuggestedPot);
        Assert.Equal(CategoryGroup.Household, groceries.Group);
    }

    [Fact]
    public void Dining_out_suggests_the_pot_wants()
    {
        Assert.Equal(Pot.Wants, CategoryCatalog.All.Single(c => c.Key == "leisure.dining").SuggestedPot);
    }

    [Fact]
    public void Income_categories_suggest_income_and_all_others_do_not()
    {
        foreach (var category in CategoryCatalog.All)
        {
            Assert.Equal(category.Group == CategoryGroup.Income, category.SuggestedPot == Pot.Income);
        }
    }

    [Fact]
    public void Every_group_has_at_least_one_category()
    {
        foreach (var group in Enum.GetValues<CategoryGroup>())
        {
            Assert.Contains(CategoryCatalog.All, c => c.Group == group);
        }
    }

    [Fact]
    public void Every_category_has_a_german_name()
    {
        foreach (var category in CategoryCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(CategoryCatalog.GermanName(category.Key)), category.Key);
        }
        Assert.Equal("Lebensmittel", CategoryCatalog.GermanName("household.groceries"));
        Assert.Equal("Essen gehen", CategoryCatalog.GermanName("leisure.dining"));
    }

    [Fact]
    public void Ids_are_stable_and_different_per_key()
    {
        var ids = CategoryCatalog.All.Select(c => c.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(CategoryCatalog.IdFor("household.groceries"), CategoryCatalog.IdFor("household.groceries"));
        Assert.Equal(CategoryCatalog.IdFor("household.groceries"),
            CategoryCatalog.All.Single(c => c.Key == "household.groceries").Id);
    }

    [Theory]
    [InlineData("household", CategoryGroup.Household)]
    [InlineData("children", CategoryGroup.Children)]
    [InlineData("income", CategoryGroup.Income)]
    public void Group_keys_round_trip(string key, CategoryGroup group)
    {
        Assert.Equal(key, CategoryGroups.ToKey(group));
        Assert.True(CategoryGroups.TryParse(key, out var parsed));
        Assert.Equal(group, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Household")]
    [InlineData("nonsense")]
    public void Unknown_group_keys_are_not_parsed(string key)
    {
        Assert.False(CategoryGroups.TryParse(key, out _));
    }
}
