using System.Windows;

namespace Playlist
{
    /// <summary>
    /// Layout constants for the playlist GridView that are fixed by WPF or by our icon artwork size,
    /// not from host brush resources. Referenced from XAML via <c>{x:Static}</c>.
    /// </summary>
    public static class PlaylistGridViewLayout
    {
        /// <summary>
        /// Outer <c>Grid.Margin</c> on sortable/nonsortable playlist column header templates.
        /// </summary>
        public const double HeaderChromeInset = 1;

        /// <summary><c>ColumnHeader</c> host horizontal padding in sortable header templates.</summary>
        public const double ColumnTextHostPaddingHorizontal = 8;

        /// <summary>Header label and body text left padding inside the host.</summary>
        public const double ColumnTextLabelPaddingLeft = 6;

        /// <summary>Header label vertical padding inside the host.</summary>
        public const double ColumnTextLabelPaddingVertical = 2;

        /// <summary>Playlist game icon width/height in pixels.</summary>
        public static readonly double IconArtworkSize = 38;

        /// <summary>Grid column slot width — equals artwork size; header/body offset is handled separately.</summary>
        public static readonly double IconColumnWidth = IconArtworkSize;

        /// <summary>Matches sortable/nonsortable header template chrome.</summary>
        public static readonly Thickness HeaderChromeMargin = new Thickness(HeaderChromeInset);

        /// <summary>Sortable header <c>ColumnHeader</c> host padding; mirrored on text body cells.</summary>
        public static readonly Thickness ColumnTextHostPadding = new Thickness(ColumnTextHostPaddingHorizontal, 0, ColumnTextHostPaddingHorizontal, 0);

        /// <summary>Sortable header label margin; mirrored on text body cells.</summary>
        public static readonly Thickness ColumnTextLabelMargin = new Thickness(ColumnTextLabelPaddingLeft, ColumnTextLabelPaddingVertical, 0, ColumnTextLabelPaddingVertical);

        /// <summary>Icon column header + cell: vertical inset only (no horizontal grid margin).</summary>
        public static readonly Thickness IconColumnChromeMargin = new Thickness(0, HeaderChromeInset, 0, HeaderChromeInset);

        /// <summary>Shared-size group for sub-hour minute digits in <c>x minutes</c> rows.</summary>
        public const string PlaytimeSubHourMinuteDigitsSharedSizeGroup = "PlaylistPlaytimeSubHourMinuteDigits";

        /// <summary>Shared-size group for playtime hour units (e.g. <c>46h</c>) across Time Played rows.</summary>
        public const string PlaytimeHourUnitSharedSizeGroup = "PlaylistPlaytimeHourUnit";

        /// <summary>Shared-size group for playtime minute units (e.g. <c>44m</c>) across Time Played rows.</summary>
        public const string PlaytimeMinuteUnitSharedSizeGroup = "PlaylistPlaytimeMinuteUnit";

        /// <summary>HLTB colored segment strip height (matches HowLongToBeatCachedProgressBar segmentStrip).</summary>
        public const double HltbSegmentStripHeight = 22;

        /// <summary>Shared row-block height for HLTB bar + plugin button vertical alignment.</summary>
        public const double HltbCellBlockHeight = 30;

        /// <summary>Pill chrome for completion status menu entries.</summary>
        public const double CompletionStatusMenuHeaderCornerRadius = 3;

        public static readonly Thickness CompletionStatusMenuHeaderPadding = new Thickness(6, 1, 6, 1);
    }
}
