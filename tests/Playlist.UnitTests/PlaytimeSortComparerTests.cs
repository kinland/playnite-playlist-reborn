using Xunit;

namespace Playlist.UnitTests;

public class PlaytimeSortComparerTests
{
    [Fact]
    public void Compare_Descending_PrioritizesHigherPlaytime()
    {
        var comparer = new PlaytimeSortComparer(descending: true);

        Assert.True(comparer.Compare(7200, 3600) < 0);
        Assert.True(comparer.Compare(3600, 7200) > 0);
    }

    [Fact]
    public void Compare_Ascending_PrioritizesLowerPlaytime()
    {
        var comparer = new PlaytimeSortComparer(descending: false);

        Assert.True(comparer.Compare(3600, 7200) < 0);
        Assert.True(comparer.Compare(7200, 3600) > 0);
    }

    [Fact]
    public void Compare_Descending_PinsUnplayedToBottom()
    {
        var comparer = new PlaytimeSortComparer(descending: true);

        Assert.True(comparer.Compare(3600, 0) < 0);
        Assert.True(comparer.Compare(0, 3600) > 0);
    }

    [Fact]
    public void Compare_Ascending_PinsUnplayedToBottom()
    {
        var comparer = new PlaytimeSortComparer(descending: false);

        Assert.True(comparer.Compare(3600, 0) < 0);
        Assert.True(comparer.Compare(0, 3600) > 0);
    }
}
