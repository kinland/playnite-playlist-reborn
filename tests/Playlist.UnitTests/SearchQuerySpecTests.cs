using Xunit;

namespace Playlist.UnitTests;

public class SearchQuerySpecTests
{
    [Fact]
    public void Parse_HandlesNameOnlyQuery()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("Alan Wake");
        Assert.Equal("Alan Wake", spec.NameQuery);
        Assert.Empty(spec.TagQueries);
        Assert.Empty(spec.GenreQueries);
        Assert.Empty(spec.DeveloperQueries);
        Assert.Empty(spec.PublisherQueries);
        Assert.Empty(spec.CategoryQueries);
        Assert.Empty(spec.FeatureQueries);
    }

    [Fact]
    public void Parse_HandlesSingleScopedQuery()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("genre:shooter");
        Assert.Equal(string.Empty, spec.NameQuery);
        Assert.Empty(spec.TagQueries);
        Assert.Single(spec.GenreQueries);
        Assert.Equal("shooter", spec.GenreQueries[0]);
        Assert.Empty(spec.DeveloperQueries);
        Assert.Empty(spec.PublisherQueries);
        Assert.Empty(spec.CategoryQueries);
        Assert.Empty(spec.FeatureQueries);
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

        string scoped = spec.TagQueries.Count > 0 ? spec.TagQueries[0] : spec.GenreQueries[0];
        Assert.Equal(expectedScopedValue, scoped);
    }

    [Fact]
    public void Parse_HandlesSeparatedScopeTokenAndValue()
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse("tag: puzzle genre: shooter");
        Assert.Equal(string.Empty, spec.NameQuery);
        Assert.Equal("puzzle", spec.TagQueries[0]);
        Assert.Equal("shooter", spec.GenreQueries[0]);
    }

    [Theory]
    [InlineData("developer:remedy", "remedy")]
    [InlineData("dev: remedy", "remedy")]
    public void Parse_HandlesDeveloperAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.DeveloperQueries);
        Assert.Equal(expected, spec.DeveloperQueries[0]);
    }

    [Theory]
    [InlineData("publisher:annapurna", "annapurna")]
    [InlineData("pub: annapurna", "annapurna")]
    public void Parse_HandlesPublisherAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.PublisherQueries);
        Assert.Equal(expected, spec.PublisherQueries[0]);
    }

    [Theory]
    [InlineData("category:indie", "indie")]
    [InlineData("cat: indie", "indie")]
    public void Parse_HandlesCategoryAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.CategoryQueries);
        Assert.Equal(expected, spec.CategoryQueries[0]);
    }

    [Theory]
    [InlineData("feature:controller", "controller")]
    [InlineData("feat: controller", "controller")]
    public void Parse_HandlesFeatureAliases(string input, string expected)
    {
        SearchQuerySpec spec = SearchQuerySpec.Parse(input);
        Assert.Single(spec.FeatureQueries);
        Assert.Equal(expected, spec.FeatureQueries[0]);
    }
}
