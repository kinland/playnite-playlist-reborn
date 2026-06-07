using System;
using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Compares playtime values with unplayed rows pinned to the bottom in either direction.
    /// </summary>
    internal sealed class PlaytimeSortComparer : IComparer<ulong>
    {
        private readonly int directionSign;

        /// <summary>
        /// Creates comparer with ascending or descending direction.
        /// </summary>
        public PlaytimeSortComparer(bool descending)
        {
            directionSign = descending ? -1 : 1;
        }

        /// <summary>
        /// Compares playtime seconds, treating zero playtime as unplayed and always sorting it last.
        /// </summary>
        public int Compare(ulong x, ulong y)
        {
            bool xUnplayed = x == 0;
            bool yUnplayed = y == 0;
            if (xUnplayed != yUnplayed)
            {
                return xUnplayed ? 1 : -1;
            }

            if (xUnplayed)
            {
                return 0;
            }

            return directionSign * x.CompareTo(y);
        }
    }
}
