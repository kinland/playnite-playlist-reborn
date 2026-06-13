using System.Windows;

namespace Playlist
{
    /// <summary>
    /// Layout constants for the playlist GridView that are fixed by WPF or by our icon artwork size,
    /// not by the active Playnite theme. XAML mirrors these in UserControl.Resources (StaticResource);
    /// x:Static cannot reference this internal type.
    /// </summary>
    internal static class PlaylistGridViewLayout
    {
        /// <summary>
        /// WPF <see cref="System.Windows.Controls.GridViewRowPresenter"/> hard-codes this horizontal
        /// margin on every cell <see cref="System.Windows.Controls.ContentPresenter"/> (see dotnet/wpf#249).
        /// Cleared at runtime by <see cref="PlaylistGridViewRowPresenter"/>.
        /// </summary>
        internal const double CellMargin = 6;

        /// <summary>
        /// Outer <c>Grid.Margin</c> on sortable/nonsortable playlist column header templates.
        /// </summary>
        internal const double HeaderChromeInset = 1;

        /// <summary>Matches <c>HoverBg</c> <see cref="System.Windows.Controls.Border.BorderThickness"/> in header templates.</summary>
        internal const double IconColumnBorderThickness = 1;

        /// <summary>Playlist game icon width/height in pixels.</summary>
        internal static readonly double IconArtworkSize = 38;

        /// <summary>Grid column slot width — equals artwork size; header/body offset is handled separately.</summary>
        internal static readonly double IconColumnWidth = IconArtworkSize;

        /// <summary>Matches sortable/nonsortable header template chrome.</summary>
        internal static readonly Thickness HeaderChromeMargin = new Thickness(HeaderChromeInset);

        /// <summary>Icon column header + cell: vertical inset only (no horizontal grid margin).</summary>
        internal static readonly Thickness IconColumnChromeMargin = new Thickness(0, HeaderChromeInset, 0, HeaderChromeInset);
    }
}
