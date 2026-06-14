using Playnite.SDK.Models;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class CompletionStatusDisplayConverterTests
{
    private readonly CompletionStatusDisplayConverter converter = new();

    static CompletionStatusDisplayConverterTests()
    {
        PlaylistTestLocalization.Install();
    }

    [Fact]
    public void Convert_localizes_completion_status_object()
    {
        var status = new CompletionStatus { Name = "Played" };
        Assert.Equal("Played", converter.Convert(status, typeof(string), null, null));
    }

    [Fact]
    public void Convert_returns_empty_for_null_status()
    {
        Assert.Null(converter.Convert(null, typeof(string), null, null));
    }
}
