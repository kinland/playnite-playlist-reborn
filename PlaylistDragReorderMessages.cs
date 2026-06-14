using Playnite.SDK;
using System;

namespace Playlist
{
    /// <summary>
    /// Localized drag-reorder feedback shown on the playlist list tooltip while a drop is blocked.
    /// </summary>
    internal static class PlaylistDragReorderMessages
    {
        /// <summary>Test seam for substituting localized strings in unit tests.</summary>
        internal static Func<string, string> TestGetString { get; set; }

        internal static string BuildSortBlockedMessage(string activeSortColumnKey)
        {
            string sortLabel = ResolveSortColumnLabel(activeSortColumnKey);
            string rankLabel = GetString("LOCPlaylist_Column_Rank");
            return string.Format(
                GetString("LOCPlaylist_DragReorderBlocked_SortActive"),
                sortLabel,
                rankLabel);
        }

        internal static string BuildBucketBlockedMessage(string bucketLabel)
        {
            return string.Format(
                GetString("LOCPlaylist_DragReorderBlocked_Bucket"),
                bucketLabel);
        }

        internal static string GetString(string resourceKey)
        {
            if (TestGetString != null)
            {
                return TestGetString(resourceKey);
            }

            return PlaylistLocalization.GetString(resourceKey);
        }

        internal static string ResolveSortColumnLabel(string columnKey)
        {
            if (string.IsNullOrEmpty(columnKey))
            {
                return GetString("LOCPlaylist_Column_Rank");
            }

            switch (columnKey)
            {
                case "Rank":
                    return GetString("LOCPlaylist_Column_Rank");
                case "Name":
                    return GetString("LOCGameNameTitle");
                case "Playtime":
                    return GetString("LOCTimePlayed");
                case "CompletionStatus":
                    return GetString("LOCCompletionStatus");
                case "LastPlayed":
                    return GetString("LOCPlaylist_LastPlayedColumn");
                case "LastActivity":
                    return GetString("LOCPlaylist_LastActivityColumn");
                case "HowLongToBeat":
                    return HltbColumnHeaderLabels.GetColumnBaseText();
                default:
                    return columnKey;
            }
        }
    }
}
