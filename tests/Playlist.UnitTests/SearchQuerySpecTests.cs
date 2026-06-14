using System.Linq;
using Xunit;

namespace Playlist.UnitTests;

public class SearchQuerySpecTests
{
    private static string FirstValue(SearchQuerySpec spec, ScopedFilterKind kind)
    {
        return spec.GetClauses(kind).First().Values[0];
    }

    [Fact]
    public void Parse_HandlesNameOnlyQuery()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("Alan Wake");
        Assert.Equal("Alan Wake", spec.NameQuery);
        Assert.Empty(spec.Clauses);
    }

    [Fact]
    public void Parse_HandlesSingleScopedQuery()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("genre:shooter");
        Assert.Equal(string.Empty, spec.NameQuery);
        Assert.Single(spec.GetClauses(ScopedFilterKind.Genre));
        Assert.Equal("shooter", FirstValue(spec, ScopedFilterKind.Genre));
    }

    [Theory]
    [InlineData("Alan genre:shooter", "Alan", "shooter")]
    [InlineData("genre:shooter Alan", "Alan", "shooter")]
    [InlineData("tag:puzzle Court", "Court", "puzzle")]
    [InlineData("Court tag:puzzle", "Court", "puzzle")]
    public void Parse_HandlesMixedNameAndScopedTerms(string input, string expectedName, string expectedScopedValue)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Equal(expectedName, spec.NameQuery);

        string scoped = spec.GetClauses(ScopedFilterKind.Tag).Any()
            ? FirstValue(spec, ScopedFilterKind.Tag)
            : FirstValue(spec, ScopedFilterKind.Genre);
        Assert.Equal(expectedScopedValue, scoped);
    }

    [Fact]
    public void Parse_HandlesSeparatedScopeTokenAndValue()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("tag: puzzle genre: shooter");
        Assert.Equal(string.Empty, spec.NameQuery);
        Assert.Equal("puzzle", FirstValue(spec, ScopedFilterKind.Tag));
        Assert.Equal("shooter", FirstValue(spec, ScopedFilterKind.Genre));
    }

    [Theory]
    [InlineData("developer:remedy", "remedy")]
    [InlineData("dev: remedy", "remedy")]
    public void Parse_HandlesDeveloperAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.GetClauses(ScopedFilterKind.Developer));
        Assert.Equal(expected, FirstValue(spec, ScopedFilterKind.Developer));
    }

    [Theory]
    [InlineData("publisher:annapurna", "annapurna")]
    [InlineData("pub: annapurna", "annapurna")]
    public void Parse_HandlesPublisherAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.GetClauses(ScopedFilterKind.Publisher));
        Assert.Equal(expected, FirstValue(spec, ScopedFilterKind.Publisher));
    }

    [Theory]
    [InlineData("category:indie", "indie")]
    [InlineData("cat: indie", "indie")]
    public void Parse_HandlesCategoryAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.GetClauses(ScopedFilterKind.Category));
        Assert.Equal(expected, FirstValue(spec, ScopedFilterKind.Category));
    }

    [Theory]
    [InlineData("feature:controller", "controller")]
    [InlineData("feat: controller", "controller")]
    public void Parse_HandlesFeatureAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.GetClauses(ScopedFilterKind.Feature));
        Assert.Equal(expected, FirstValue(spec, ScopedFilterKind.Feature));
    }

    [Fact]
    public void Parse_HandlesQuotedValuesWithSpaces()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("dev:\"11 bit studios\"");
        Assert.Equal("11 bit studios", FirstValue(spec, ScopedFilterKind.Developer));
    }

    [Fact]
    public void Parse_HandlesOrListViaComma()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("dev:10tons,17-BIT");
        ScopedSearchClause clause = spec.GetClauses(ScopedFilterKind.Developer).Single();
        Assert.Equal(ScopedValueCombine.Or, clause.CombineWithin);
        Assert.Equal(new[] { "10tons", "17-BIT" }, clause.Values);
    }

    [Fact]
    public void Parse_HandlesAndViaRepeatedScope()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("tag:fps tag:roguelike");
        Assert.Equal(2, spec.GetClauses(ScopedFilterKind.Tag).Count());
        Assert.Equal("fps", spec.GetClauses(ScopedFilterKind.Tag).First().Values[0]);
        Assert.Equal("roguelike", spec.GetClauses(ScopedFilterKind.Tag).Last().Values[0]);
    }

    [Fact]
    public void Parse_HandlesNegation()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("Alan !dev:remedy");
        Assert.Equal("Alan", spec.NameQuery);
        ScopedSearchClause clause = spec.GetClauses(ScopedFilterKind.Developer).Single();
        Assert.True(clause.Negated);
        Assert.Equal("remedy", clause.Values[0]);
        Assert.True(spec.HasPlaylistOnlySyntax);
    }

    [Fact]
    public void Parse_HandlesExplicitAndWithinToken()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("tag:fps&roguelike");
        ScopedSearchClause clause = spec.GetClauses(ScopedFilterKind.Tag).Single();
        Assert.Equal(ScopedValueCombine.And, clause.CombineWithin);
        Assert.Equal(new[] { "fps", "roguelike" }, clause.Values);
    }

    [Theory]
    [InlineData("dev:'11 bit studios'", "Developer", "11 bit studios", false)]
    [InlineData("tag:\"fps|roguelike\"", "Tag", "fps|roguelike", false)]
    [InlineData("dev:\"a&b\"", "Developer", "a&b", false)]
    [InlineData("!dev:'remedy'", "Developer", "remedy", true)]
    public void Parse_QuotedValues_DoNotSplitOnSpecialCharactersInsideQuotes(
        string input,
        string kindName,
        string expectedValue,
        bool negated)
    {
        ScopedFilterKind kind = kindName == "Tag" ? ScopedFilterKind.Tag : ScopedFilterKind.Developer;
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        ScopedSearchClause clause = spec.GetClauses(kind).Single();
        Assert.Equal(negated, clause.Negated);
        Assert.Equal(expectedValue, clause.Values[0]);
    }

    [Fact]
    public void Parse_DoubleQuotedName_WithScopedTerms()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("\"Alan Wake\" dev:remedy");
        Assert.Equal("Alan Wake", spec.NameQuery);
        Assert.Equal("remedy", FirstValue(spec, ScopedFilterKind.Developer));
    }

    [Fact]
    public void HasNonSyncableClauseStructure_true_for_and_list_across_multiple_clauses()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("dev:a&b dev:c");
        Assert.True(spec.HasNonSyncableClauseStructure());
        Assert.True(spec.HasPlaylistOnlySyntax);
    }

    [Fact]
    public void HasNonSyncableClauseStructure_false_for_single_and_within_clause()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("tag:fps&roguelike");
        Assert.False(spec.HasNonSyncableClauseStructure());
        Assert.False(spec.HasPlaylistOnlySyntax);
    }

    [Fact]
    public void KindHasPlaylistOnlySyntax_true_for_negated_kind_only()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("Alan !dev:remedy");
        Assert.True(spec.KindHasPlaylistOnlySyntax(ScopedFilterKind.Developer));
        Assert.False(spec.KindHasPlaylistOnlySyntax(ScopedFilterKind.Tag));
    }
}
