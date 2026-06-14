using Xunit;

namespace Playlist.UnitTests;

public class ScopedSearchClauseGroupTests
{
    [Fact]
    public void Matches_negated_clause_excludes_matching_candidate()
    {
        var group = new ScopedSearchClauseGroup(new[]
        {
            new ScopedSearchClause(ScopedFilterKind.Tag, negated: true, ScopedValueCombine.Or, new[] { "fps" }),
        });

        Assert.True(group.Matches(new[] { "RPG", "Action" }));
        Assert.False(group.Matches(new[] { "FPS", "Action" }));
    }

    [Fact]
    public void Matches_and_within_clause_requires_all_values()
    {
        var group = new ScopedSearchClauseGroup(new[]
        {
            new ScopedSearchClause(ScopedFilterKind.Tag, negated: false, ScopedValueCombine.And, new[] { "fps", "roguelike" }),
        });

        Assert.True(group.Matches(new[] { "fps", "roguelike", "indie" }));
        Assert.False(group.Matches(new[] { "fps", "action" }));
    }

    [Fact]
    public void Matches_multiple_clauses_requires_every_clause()
    {
        var group = new ScopedSearchClauseGroup(new[]
        {
            new ScopedSearchClause(ScopedFilterKind.Tag, negated: false, ScopedValueCombine.Or, new[] { "fps" }),
            new ScopedSearchClause(ScopedFilterKind.Genre, negated: false, ScopedValueCombine.Or, new[] { "shooter" }),
        });

        Assert.True(group.Matches(new[] { "fps", "shooter" }));
        Assert.False(group.Matches(new[] { "fps", "rpg" }));
    }
}
