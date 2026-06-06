using Xunit;

namespace Playlist.UnitTests;

public class LastPlayedSortBucketComparerTests
{
    private const int Moments = 0;
    private const int Minute1 = 1;
    private const int Minute59 = 59;
    private const int Hour1 = 60;
    private const int Hour23 = 82;
    private const int Day1 = 83;
    private const int Day6 = 88;
    private const int Week1 = 89;
    private const int Week3 = 91;
    private const int Month1 = 92;
    private const int Month11 = 102;
    private const int Year = 103;
    private const int LongAgo = 104;
    private const int Unplayed = 105;

    [Theory]
    [InlineData(Minute1, Minute59)]
    [InlineData(Minute59, Hour1)]
    [InlineData(Hour1, Hour23)]
    [InlineData(Hour23, Day1)]
    [InlineData(Day1, Day6)]
    [InlineData(Day6, Week1)]
    [InlineData(Week1, Week3)]
    [InlineData(Week3, Month1)]
    [InlineData(Month1, Month11)]
    [InlineData(Month11, Year)]
    [InlineData(Year, LongAgo)]
    [InlineData(LongAgo, Unplayed)]
    public void Compare_Ascending_RespectsChronologicalBoundaryOrder(int newerBucket, int olderBucket)
    {
        var comparer = new LastPlayedSortBucketComparer(descending: false);
        var newer = new LastPlayedSortKey(newerBucket, lastPlayedTicksUtc: 100, playlistRankIndex: 10);
        var older = new LastPlayedSortKey(olderBucket, lastPlayedTicksUtc: 100, playlistRankIndex: 1);

        Assert.True(comparer.Compare(newer, older) < 0);
        Assert.True(comparer.Compare(older, newer) > 0);
    }

    [Theory]
    [InlineData(Minute59, Hour1)]
    [InlineData(Hour23, Day1)]
    [InlineData(Week3, Month1)]
    [InlineData(Month11, Year)]
    [InlineData(LongAgo, Unplayed)]
    public void Compare_Descending_RespectsChronologicalBoundaryOrder(int newerBucket, int olderBucket)
    {
        var comparer = new LastPlayedSortBucketComparer(descending: true);
        var newer = new LastPlayedSortKey(newerBucket, lastPlayedTicksUtc: 100, playlistRankIndex: 10);
        var older = new LastPlayedSortKey(olderBucket, lastPlayedTicksUtc: 100, playlistRankIndex: 1);

        Assert.True(comparer.Compare(newer, older) > 0);
        Assert.True(comparer.Compare(older, newer) < 0);
    }

    [Fact]
    public void Compare_Ascending_PrioritizesMoreRecentBucket()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: false);
        var recent = new LastPlayedSortKey(sortBucket: Hour1, lastPlayedTicksUtc: 0, playlistRankIndex: 10);
        var older = new LastPlayedSortKey(sortBucket: Month1, lastPlayedTicksUtc: 0, playlistRankIndex: 1);

        Assert.True(comparer.Compare(recent, older) < 0);
        Assert.True(comparer.Compare(older, recent) > 0);
    }

    [Fact]
    public void Compare_Descending_ReversesBucketPriority()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: true);
        var recent = new LastPlayedSortKey(sortBucket: Hour1, lastPlayedTicksUtc: 0, playlistRankIndex: 10);
        var older = new LastPlayedSortKey(sortBucket: Month1, lastPlayedTicksUtc: 0, playlistRankIndex: 1);

        Assert.True(comparer.Compare(recent, older) > 0);
        Assert.True(comparer.Compare(older, recent) < 0);
    }

    [Fact]
    public void Compare_UsesPlaylistRankAsTieBreakerAscending()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: false);
        var first = new LastPlayedSortKey(sortBucket: Week1, lastPlayedTicksUtc: 0, playlistRankIndex: 1);
        var second = new LastPlayedSortKey(sortBucket: Week1, lastPlayedTicksUtc: 0, playlistRankIndex: 5);

        Assert.True(comparer.Compare(first, second) < 0);
    }

    [Fact]
    public void Compare_UsesPlaylistRankAsTieBreakerDescending()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: true);
        var first = new LastPlayedSortKey(sortBucket: Week1, lastPlayedTicksUtc: 0, playlistRankIndex: 1);
        var second = new LastPlayedSortKey(sortBucket: Week1, lastPlayedTicksUtc: 0, playlistRankIndex: 5);

        Assert.True(comparer.Compare(first, second) > 0);
    }

    [Fact]
    public void Compare_UnplayedFallsAfterLongAgoInAscending()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: false);
        var longAgo = new LastPlayedSortKey(sortBucket: LongAgo, lastPlayedTicksUtc: 0, playlistRankIndex: 1);
        var unplayed = new LastPlayedSortKey(sortBucket: Unplayed, lastPlayedTicksUtc: 0, playlistRankIndex: 1);

        Assert.True(comparer.Compare(longAgo, unplayed) < 0);
        Assert.True(comparer.Compare(unplayed, longAgo) > 0);
    }

    [Fact]
    public void Compare_MomentsBucket_UsesExactTimestampAscending()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: false);
        var newer = new LastPlayedSortKey(sortBucket: Moments, lastPlayedTicksUtc: 200, playlistRankIndex: 10);
        var older = new LastPlayedSortKey(sortBucket: Moments, lastPlayedTicksUtc: 100, playlistRankIndex: 1);

        Assert.True(comparer.Compare(newer, older) < 0);
    }

    [Fact]
    public void Compare_MomentsBucket_UsesExactTimestampDescending()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: true);
        var newer = new LastPlayedSortKey(sortBucket: Moments, lastPlayedTicksUtc: 200, playlistRankIndex: 10);
        var older = new LastPlayedSortKey(sortBucket: Moments, lastPlayedTicksUtc: 100, playlistRankIndex: 1);

        Assert.True(comparer.Compare(newer, older) > 0);
    }

    [Fact]
    public void Compare_NonMomentsBucket_IgnoresTimestampAndUsesRank()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: false);
        var first = new LastPlayedSortKey(sortBucket: Hour1, lastPlayedTicksUtc: 9999, playlistRankIndex: 1);
        var second = new LastPlayedSortKey(sortBucket: Hour1, lastPlayedTicksUtc: 1, playlistRankIndex: 2);

        Assert.True(comparer.Compare(first, second) < 0);
    }

    [Fact]
    public void Compare_IdenticalKeys_ReturnZero()
    {
        var comparer = new LastPlayedSortBucketComparer(descending: false);
        var key = new LastPlayedSortKey(sortBucket: Day1, lastPlayedTicksUtc: 1234, playlistRankIndex: 4);

        Assert.Equal(0, comparer.Compare(key, key));
    }
}
