using Xunit;

namespace Playlist.UnitTests;

public class SearchQueryMatcherTests
{
    [Fact]
    public void EmptyQuery_MatchesAllCandidates()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create(string.Empty);
        Assert.True(matcher.IsMatch("Halo Infinite"));
    }

    [Fact]
    public void Wildcard_MatchesPrefix()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("halo*");
        Assert.True(matcher.IsMatch("Halo Infinite"));
    }

    [Fact]
    public void Wildcard_MatchesSingleCharacter()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("h?lo");
        Assert.True(matcher.IsMatch("halo"));
    }

    [Fact]
    public void Wildcard_MatchesWithTrailingWords()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("A*W");
        Assert.True(matcher.IsMatch("Alan Wake II"));
    }

    [Fact]
    public void Wildcard_MatchesAcrossMiddleAndSuffix()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("Alan W*k");
        Assert.True(matcher.IsMatch("Alan Wake II"));
    }

    [Fact]
    public void PartialMatch_IsCaseInsensitive()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("TURN");
        Assert.True(matcher.IsMatch("Returnal"));
    }

    [Fact]
    public void FuzzyMatch_AllowsSmallTypo()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("retunral");
        Assert.True(matcher.IsMatch("Returnal"));
    }

    [Fact]
    public void FuzzyMatch_AllowsTokenTypo()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("Alan Wke");
        Assert.True(matcher.IsMatch("Alan Wake II"));
    }

    [Fact]
    public void FuzzyMatch_RejectsDistantText()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("zzzzzz");
        Assert.False(matcher.IsMatch("Returnal"));
    }

    [Fact]
    public void SingleCharacterQuery_DoesNotUseTypoFallback()
    {
        SearchQueryMatcher matcher = SearchQueryMatcher.Create("a");
        Assert.False(matcher.IsMatch("zzzz"));
    }
}
