namespace Playlist
{
    /// <summary>
    /// Copy for the HowLongToBeat column header base label and hover/active sort suffixes.
    /// </summary>
    internal static class HltbColumnHeaderLabels
    {
        public const string BaseText = "HowLongToBeat";

        public static string FormatActiveSortSuffix(string typeLabel) => $" ({typeLabel})";

        public static string FormatHoverSortSuffix(string typeLabel) => $" (sort by {typeLabel})";
    }
}
