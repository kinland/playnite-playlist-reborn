using Xunit;

namespace Playlist.UnitTests;

public class PlaylistRankInputTests
{
    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(-3, 5, 1)]
    [InlineData(1, 5, 1)]
    [InlineData(3, 5, 3)]
    [InlineData(5, 5, 5)]
    [InlineData(9999, 5, 5)]
    public void ClampToPlaylistBounds_ClampsOutOfRangeValues(int rank, int count, int expected)
    {
        Assert.Equal(expected, PlaylistRankInput.ClampToPlaylistBounds(rank, count));
    }
}
