using System;

namespace Playlist
{
    public static class PlaylistRankInput
    {
        public static int ClampToPlaylistBounds(int rank, int playlistCount)
        {
            if (playlistCount <= 0)
            {
                return rank;
            }

            return Math.Max(1, Math.Min(rank, playlistCount));
        }
    }
}
