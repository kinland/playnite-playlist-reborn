using Playnite.SDK.Models;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

/// <summary>
/// Sync behavior for queries using quoted scoped values with special characters.
/// </summary>
public class SearchQueryQuoteSyncTests
{
    private const string ElevenBitStudios = "11 bit studios";
    private const string CommaInName = "foo,bar";
    private const string PipeInName = "a|b";
    private const string AmpersandInName = "x&y";

    private sealed class TestScopedFilterNameLookup : IScopedFilterNameLookup
    {
        public string ResolveId(ScopedFilterKind kind, System.Guid id) => null;

        public IdItemFilterItemProperties ResolveQuery(ScopedFilterKind kind, string query)
        {
            return new IdItemFilterItemProperties(query);
        }
    }

    private static readonly TestScopedFilterNameLookup Lookup = new TestScopedFilterNameLookup();

    private static void AssertClauseValues(
        SearchQuerySpec spec,
        ScopedFilterKind kind,
        bool negated,
        ScopedValueCombine combine,
        params string[] expectedValues)
    {
        ScopedSearchClause clause = Assert.Single(spec.GetClauses(kind));
        Assert.Equal(negated, clause.Negated);
        Assert.Equal(combine, clause.CombineWithin);
        Assert.Equal(expectedValues, clause.Values);
    }

    [Theory]
    [InlineData("dev:\"11 bit studios\"", ElevenBitStudios)]
    [InlineData("dev:'11 bit studios'", ElevenBitStudios)]
    [InlineData("dev:\"foo,bar\"", CommaInName)]
    [InlineData("dev:'foo,bar'", CommaInName)]
    [InlineData("dev:\"a|b\"", PipeInName)]
    [InlineData("dev:\"a&b\"", "a&b")]
    [InlineData("!dev:\"11 bit studios\"", ElevenBitStudios, true)]
    [InlineData("!dev:'11 bit studios'", ElevenBitStudios, true)]
    public void Parse_QuotedScopedValues_PreserveSpecialCharacters(
        string query,
        string expectedValue,
        bool negated = false)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(query);
        AssertClauseValues(
            spec,
            ScopedFilterKind.Developer,
            negated,
            ScopedValueCombine.Or,
            expectedValue);
        if (negated)
        {
            Assert.True(spec.HasPlaylistOnlySyntax);
        }
    }

    [Fact]
    public void Parse_QuotedOrList_MixedWithUnquotedValue()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("dev:alpha,\"foo,bar\"");
        AssertClauseValues(
            spec,
            ScopedFilterKind.Developer,
            false,
            ScopedValueCombine.Or,
            "alpha",
            CommaInName);
    }

    [Fact]
    public void Parse_QuotedAndList_MixedWithUnquotedValue()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("tag:fps&\"x&y\"");
        AssertClauseValues(
            spec,
            ScopedFilterKind.Tag,
            false,
            ScopedValueCombine.And,
            "fps",
            AmpersandInName);
    }

    [Fact]
    public void Parse_QuotedName_WithScopedAndNegatedQuotedScope()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("\"Alan Wake\" dev:\"11 bit studios\" !tag:backlog");
        Assert.Equal("Alan Wake", spec.NameQuery);
        AssertClauseValues(spec, ScopedFilterKind.Developer, false, ScopedValueCombine.Or, ElevenBitStudios);
        AssertClauseValues(spec, ScopedFilterKind.Tag, true, ScopedValueCombine.Or, "backlog");
    }

    [Theory]
    [InlineData("dev:\"11 bit studios\"")]
    [InlineData("dev:'11 bit studios'")]
    [InlineData("dev:\"foo,bar\"")]
    [InlineData("dev:alpha,\"foo,bar\"")]
    public void ApplySyncPush_PushesQuotedDeveloperTextToMain(string playlistQuery)
    {
        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            playlistQuery,
            Lookup);

        Assert.NotNull(pushed.Developer);
        if (playlistQuery.Contains("alpha"))
        {
            Assert.Equal("alpha, foo,bar", pushed.Developer.Text);
        }
        else if (playlistQuery.Contains("foo,bar") && !playlistQuery.Contains("alpha"))
        {
            Assert.Equal(CommaInName, pushed.Developer.Text);
        }
        else
        {
            Assert.Equal(ElevenBitStudios, pushed.Developer.Text);
        }
    }

    [Theory]
    [InlineData("!dev:\"11 bit studios\"")]
    [InlineData("!dev:'11 bit studios'")]
    public void ApplySyncPush_NegatedQuotedDeveloper_ClearsMainDeveloper(string playlistQuery)
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Developer = new IdItemFilterItemProperties(ElevenBitStudios),
        };

        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            current,
            playlistQuery,
            Lookup);

        Assert.Null(pushed.Developer);
    }

    [Theory]
    [InlineData("dev:\"11 bit studios\"", "dev:\"11 bit studios\"")]
    [InlineData("dev:'11 bit studios'", "dev:\"11 bit studios\"")]
    [InlineData("dev:\"foo,bar\"", "dev:\"foo,bar\"")]
    [InlineData("dev:alpha,\"foo,bar\"", "dev:alpha,\"foo,bar\"")]
    public void ToPlaylistQuery_EmitsDoubleQuotesForValuesNeedingQuoting(
        string playlistQuery,
        string expectedMainRoundTrip)
    {
        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            playlistQuery,
            Lookup);

        string rebuilt = MainSearchFilterMapper.ToPlaylistQuery(pushed, Lookup);
        Assert.Equal(expectedMainRoundTrip, rebuilt);
    }

    [Theory]
    [InlineData("dev:\"11 bit studios\"")]
    [InlineData("Alan !dev:'11 bit studios'")]
    [InlineData("dev:alpha,\"foo|bar\"")]
    public void RoundTrip_QuotedQueries_RestorePreservedWhenMainMatchesSnapshot(string playlistQuery)
    {
        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            playlistQuery,
            Lookup);
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            playlistQuery,
            Lookup);

        string restored = MainSearchFilterMapper.ResolveReturnQuery(
            playlistQuery,
            snapshot,
            pushed,
            Lookup);

        Assert.Equal(playlistQuery, restored);
    }

    [Fact]
    public void RoundTrip_QuotedNegation_MergesMainNameWithoutTouchingQuotedNegation()
    {
        const string playlistQuery = "!dev:\"11 bit studios\"";
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            playlistQuery,
            Lookup);

        FilterPresetSettings changedMain = new FilterPresetSettings { Name = "Alan" };
        string restored = MainSearchFilterMapper.ResolveReturnQuery(
            playlistQuery,
            snapshot,
            changedMain,
            Lookup);

        Assert.Equal("Alan !dev:\"11 bit studios\"", restored);
    }
}
