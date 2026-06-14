using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Builds HLTB sort keys from cached per-game times and plugin render settings.
    /// </summary>
    internal static class HltbSortKeyBuilder
    {
        internal readonly struct SortKey
        {
            public SortKey(bool hasValue, long seconds, int playlistRankIndex)
            {
                HasValue = hasValue;
                Seconds = seconds;
                PlaylistRankIndex = playlistRankIndex;
            }

            public bool HasValue { get; }
            public long Seconds { get; }
            public int PlaylistRankIndex { get; }
        }

        internal static SortKey BuildSortKey(
            HltbCachedTimes times,
            HltbRenderSettings renderSettings,
            int playlistRankIndex)
        {
            if (times == null)
            {
                return new SortKey(false, long.MaxValue, playlistRankIndex);
            }

            HltbTimeVariants variants = SelectVariants(
                times,
                renderSettings?.PreferredForTimeToBeat ?? HltbPreferredTimeType.MainStory);
            long seconds = ResolvePreferredSeconds(variants, renderSettings);
            return new SortKey(seconds > 0, seconds, playlistRankIndex);
        }

        internal static int CompareSortKeys(SortKey keyX, SortKey keyY, int directionSign)
        {
            if (keyX.HasValue && !keyY.HasValue)
            {
                return -1;
            }

            if (!keyX.HasValue && keyY.HasValue)
            {
                return 1;
            }

            if (keyX.HasValue && keyY.HasValue)
            {
                int timeCompare = keyX.Seconds.CompareTo(keyY.Seconds);
                if (timeCompare != 0)
                {
                    return directionSign * timeCompare;
                }
            }

            return directionSign * keyX.PlaylistRankIndex.CompareTo(keyY.PlaylistRankIndex);
        }

        internal static HltbTimeVariants SelectVariants(HltbCachedTimes times, HltbPreferredTimeType style)
        {
            switch (style)
            {
                case HltbPreferredTimeType.MainStoryExtra:
                    return times.MainExtra;
                case HltbPreferredTimeType.Completionist:
                    return times.Completionist;
                case HltbPreferredTimeType.Solo:
                    return times.Solo;
                case HltbPreferredTimeType.CoOp:
                    return times.CoOp;
                case HltbPreferredTimeType.Versus:
                    return times.Vs;
                case HltbPreferredTimeType.MainStory:
                default:
                    return times.MainStory;
            }
        }

        internal static long ResolvePreferredSeconds(HltbTimeVariants variants, HltbRenderSettings renderSettings)
        {
            if (variants == null)
            {
                return 0;
            }

            var preferred = new List<long>();
            if (renderSettings?.UseClassic == true)
            {
                preferred.Add(variants.Classic);
            }

            if (renderSettings?.UseMedian == true)
            {
                preferred.Add(variants.Median);
            }

            if (renderSettings?.UseAverage == true)
            {
                preferred.Add(variants.Average);
            }

            if (renderSettings?.UseRushed == true)
            {
                preferred.Add(variants.Rushed);
            }

            if (renderSettings?.UseLeisure == true)
            {
                preferred.Add(variants.Leisure);
            }

            foreach (long seconds in preferred)
            {
                if (seconds > 0)
                {
                    return seconds;
                }
            }

            long[] fallback = { variants.Classic, variants.Median, variants.Average, variants.Rushed, variants.Leisure };
            foreach (long seconds in fallback)
            {
                if (seconds > 0)
                {
                    return seconds;
                }
            }

            return 0;
        }
    }
}
