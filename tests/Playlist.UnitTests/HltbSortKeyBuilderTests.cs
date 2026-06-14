using Xunit;

namespace Playlist.UnitTests;

public class HltbSortKeyBuilderTests
{
    [Fact]
    public void ResolvePreferredSeconds_falls_back_to_median_when_classic_disabled()
    {
        var variants = new HltbTimeVariants { Classic = 0, Median = 3600 };
        var settings = new HltbRenderSettings { UseClassic = true, UseMedian = true };

        long seconds = HltbSortKeyBuilder.ResolvePreferredSeconds(variants, settings);

        Assert.Equal(3600, seconds);
    }

    [Fact]
    public void BuildSortKey_uses_completionist_variants_when_preferred()
    {
        var times = new HltbCachedTimes
        {
            MainStory = new HltbTimeVariants { Classic = 100 },
            Completionist = new HltbTimeVariants { Classic = 9000 },
        };
        var settings = new HltbRenderSettings
        {
            UseClassic = true,
            PreferredForTimeToBeat = HltbPreferredTimeType.Completionist,
        };

        HltbSortKeyBuilder.SortKey key = HltbSortKeyBuilder.BuildSortKey(times, settings, playlistRankIndex: 3);

        Assert.True(key.HasValue);
        Assert.Equal(9000, key.Seconds);
        Assert.Equal(3, key.PlaylistRankIndex);
    }

    [Fact]
    public void CompareSortKeys_sorts_games_without_times_after_games_with_times()
    {
        var withTime = new HltbSortKeyBuilder.SortKey(true, 100, playlistRankIndex: 0);
        var withoutTime = new HltbSortKeyBuilder.SortKey(false, long.MaxValue, playlistRankIndex: 1);

        Assert.True(HltbSortKeyBuilder.CompareSortKeys(withTime, withoutTime, directionSign: 1) < 0);
    }
}
