using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

public class SearchQueryTokenizerTests
{
    [Theory]
    [InlineData("Alan Wake", new[] { "Alan", "Wake" })]
    [InlineData("\"Alan Wake\"", new[] { "\"Alan Wake\"" })]
    [InlineData("'Alan Wake'", new[] { "'Alan Wake'" })]
    [InlineData("dev:\"11 bit studios\"", new[] { "dev:\"11 bit studios\"" })]
    [InlineData("dev:'11 bit studios'", new[] { "dev:'11 bit studios'" })]
    [InlineData("!dev:\"remedy\"", new[] { "!dev:\"remedy\"" })]
    public void Tokenize_HandlesQuotedTokens(string query, string[] expected)
    {
        List<string> tokens = SearchQueryTokenizer.Tokenize(query);
        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData("a,b", "Or", new[] { "a", "b" })]
    [InlineData("a|b", "Or", new[] { "a", "b" })]
    [InlineData("a&b", "And", new[] { "a", "b" })]
    [InlineData("\"a,b\"", "Or", new[] { "a,b" })]
    [InlineData("'a,b'", "Or", new[] { "a,b" })]
    [InlineData("\"a|b\"", "Or", new[] { "a|b" })]
    [InlineData("\"a&b\"", "Or", new[] { "a&b" })]
    [InlineData("a,\"b,c\"", "Or", new[] { "a", "b,c" })]
    [InlineData("\"x|y\"|z", "Or", new[] { "x|y", "z" })]
    [InlineData("\"x&y\"&z", "And", new[] { "x&y", "z" })]
    public void SplitScopedValues_RespectsQuotesAroundSpecialCharacters(
        string valuePart,
        string combineName,
        string[] expected)
    {
        ScopedValueCombine combine = combineName == "And" ? ScopedValueCombine.And : ScopedValueCombine.Or;
        List<string> values = SearchQueryTokenizer.SplitScopedValues(valuePart, combine);
        Assert.Equal(expected, values);
    }

    [Theory]
    [InlineData("11 bit studios", true)]
    [InlineData("a,b", true)]
    [InlineData("a|b", true)]
    [InlineData("a&b", true)]
    [InlineData("has!bang", false)]
    [InlineData("simple", false)]
    public void NeedsQuoting_DetectsSpecialCharacters(string value, bool expected)
    {
        Assert.Equal(expected, SearchQueryTokenizer.NeedsQuoting(value));
    }

    [Theory]
    [InlineData("11 bit studios", "\"11 bit studios\"")]
    [InlineData("a&b", "\"a&b\"")]
    [InlineData("a|b", "\"a|b\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("simple", "simple")]
    public void QuoteIfNeeded_WrapsValuesContainingSpecialCharacters(string value, string expected)
    {
        Assert.Equal(expected, SearchQueryTokenizer.QuoteIfNeeded(value));
    }
}
