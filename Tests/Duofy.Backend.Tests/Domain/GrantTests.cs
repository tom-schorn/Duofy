using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Die Freigaben (Spec 68): Stufen nach Rang, keine Zeile heißt Nichts.</summary>
public class GrantTests
{
    // R1: Jede Stufe schließt die davor ein.
    [Theory]
    [InlineData(AccessLevel.Delete, AccessLevel.Edit, true)]
    [InlineData(AccessLevel.Edit, AccessLevel.Create, true)]
    [InlineData(AccessLevel.Create, AccessLevel.View, true)]
    [InlineData(AccessLevel.View, AccessLevel.View, true)]
    [InlineData(AccessLevel.View, AccessLevel.Create, false)]
    [InlineData(AccessLevel.None, AccessLevel.View, false)]
    [InlineData(AccessLevel.Edit, AccessLevel.Delete, false)]
    public void A_level_includes_every_level_before_it(AccessLevel granted, AccessLevel needed, bool includes)
    {
        Assert.Equal(includes, granted.Includes(needed));
    }

    [Fact]
    public void The_levels_rank_from_none_to_delete()
    {
        Assert.Equal(
            [AccessLevel.None, AccessLevel.View, AccessLevel.Create, AccessLevel.Edit, AccessLevel.Delete],
            Enum.GetValues<AccessLevel>().OrderBy(level => (int)level));
        Assert.Equal([0, 1, 2, 3, 4], Enum.GetValues<AccessLevel>().Select(level => (int)level));
    }

    [Fact]
    public void There_are_exactly_the_six_areas_plan_book_accounts_commitments_savings_and_categories()
    {
        Assert.Equal([Area.Plan, Area.Book, Area.Accounts, Area.Commitments, Area.Savings, Area.Categories], Enum.GetValues<Area>());
    }

    [Fact]
    public void Missing_areas_read_as_none()
    {
        var granter = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        Grant[] rows = [new() { GranterId = granter, GranteeId = grantee, Area = Area.Book, Level = AccessLevel.Create }];

        var levels = Grant.LevelsOf(rows);

        Assert.Equal(AccessLevel.Create, levels[Area.Book]);
        Assert.Equal(AccessLevel.None, levels[Area.Plan]);
        Assert.Equal(AccessLevel.None, levels[Area.Accounts]);
        Assert.Equal(AccessLevel.None, levels[Area.Commitments]);
        Assert.Equal(AccessLevel.None, levels[Area.Savings]);
        // Kategorien ohne Zeile: Löschen für jedes Mitglied (Spec 140).
        Assert.Equal(AccessLevel.Delete, levels[Area.Categories]);
    }

    [Fact]
    public void An_explicit_none_row_for_categories_reads_as_none()
    {
        Grant[] rows = [new() { GranterId = Guid.NewGuid(), GranteeId = Guid.NewGuid(), Area = Area.Categories, Level = AccessLevel.None }];

        Assert.Equal(AccessLevel.None, Grant.LevelsOf(rows)[Area.Categories]);
    }
}
