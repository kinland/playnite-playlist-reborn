using System;
using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Sort key for Last Played ordering.
    /// </summary>
    internal readonly struct LastPlayedSortKey
    {
        public int SortBucket { get; }
        public long LastPlayedTicksUtc { get; }
        public int PlaylistRankIndex { get; }

        public LastPlayedSortKey(int sortBucket, long lastPlayedTicksUtc, int playlistRankIndex)
        {
            SortBucket = sortBucket;
            LastPlayedTicksUtc = lastPlayedTicksUtc;
            PlaylistRankIndex = playlistRankIndex;
        }
    }

    /// <summary>
    /// Compares Last Played keys by display bucket order with rank/recency tie-breakers.
    /// </summary>
    internal sealed class LastPlayedSortBucketComparer : IComparer<LastPlayedSortKey>
    {
        private readonly int directionSign;

        /// <summary>
        /// Creates comparer with ascending or descending direction.
        /// </summary>
        public LastPlayedSortBucketComparer(bool descending)
        {
            directionSign = descending ? -1 : 1;
        }

        /// <summary>
        /// Compares by SortBucket first, then exact recency in Moments bucket, then playlist rank.
        /// </summary>
        public int Compare(LastPlayedSortKey x, LastPlayedSortKey y)
        {
            if (directionSign < 0)
            {
                bool xUnplayed = x.SortBucket == LastPlayedRelativeFormatter.UnplayedSortBucketOrder;
                bool yUnplayed = y.SortBucket == LastPlayedRelativeFormatter.UnplayedSortBucketOrder;
                if (xUnplayed != yUnplayed)
                {
                    return xUnplayed ? 1 : -1;
                }
            }

            int bucketCmp = x.SortBucket.CompareTo(y.SortBucket);
            if (bucketCmp != 0)
            {
                return directionSign * bucketCmp;
            }

            if (x.SortBucket == LastPlayedRelativeFormatter.MomentsBucketOrder
                && x.LastPlayedTicksUtc != y.LastPlayedTicksUtc)
            {
                // "Moments ago" is the only label sorted by exact recency.
                int recencyCmp = y.LastPlayedTicksUtc.CompareTo(x.LastPlayedTicksUtc);
                return directionSign * recencyCmp;
            }

            return directionSign * x.PlaylistRankIndex.CompareTo(y.PlaylistRankIndex);
        }
    }
}
