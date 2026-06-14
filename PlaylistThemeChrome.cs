using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Resolved visual appearances for playlist surfaces. Callers apply the returned brushes/colors
    /// without branching on theme luminance or managed-chrome detection.
    /// </summary>
    internal static class PlaylistThemeChrome
    {
        private static readonly HltbEmptyTrackAppearance HltbEmptyTrackNormalAppearance =
            new HltbEmptyTrackAppearance(PlaylistThemeColors.EmptyHltbTrackFillNormal, PlaylistThemeColors.EmptyHltbTrackBorderNormal);

        private static readonly HltbEmptyTrackAppearance HltbEmptyTrackOnDarkRowAppearance =
            new HltbEmptyTrackAppearance(PlaylistThemeColors.EmptyHltbTrackFillOnDarkRow, PlaylistThemeColors.EmptyHltbTrackBorderOnDarkRow);

        /// <summary>
        /// Active sort-header / hover highlight fill derived from sampled header label color.
        /// </summary>
        internal readonly struct SortHeaderHighlightAppearance
        {
            public SortHeaderHighlightAppearance(
                SolidColorBrush background,
                SolidColorBrush border,
                SolidColorBrush foreground,
                bool useLightHeaderText)
            {
                Background = background;
                Border = border;
                Foreground = foreground;
                UseLightHeaderText = useLightHeaderText;
            }

            public SolidColorBrush Background { get; }

            public SolidColorBrush Border { get; }

            public SolidColorBrush Foreground { get; }

            /// <summary>When true, header label text should use <see cref="Foreground"/> for legibility.</summary>
            public bool UseLightHeaderText { get; }

            /// <summary>When true, highlight chrome uses a darkening overlay (force full opacity on apply).</summary>
            public bool UseDarkeningOverlay => UseLightHeaderText;
        }

        /// <summary>
        /// Accent colors for the full-height column reorder drop guide.
        /// </summary>
        internal readonly struct DropMarkerPalette
        {
            public DropMarkerPalette(Color slotCenter, Color glyph)
            {
                SlotCenter = slotCenter;
                Glyph = glyph;
            }

            public Color SlotCenter { get; }

            public Color Glyph { get; }
        }

        /// <summary>
        /// Empty-state HLTB progress track chrome for the current row highlight context.
        /// </summary>
        internal readonly struct HltbEmptyTrackAppearance
        {
            public HltbEmptyTrackAppearance(SolidColorBrush fill, SolidColorBrush border)
            {
                Fill = fill;
                Border = border;
            }

            public SolidColorBrush Fill { get; }

            public SolidColorBrush Border { get; }
        }

        internal enum RowEmbeddedChromeStyle
        {
            None,
            /// <summary>Light control panel + dark glyph — active dark HoverBrush mouseover when managed chrome applies.</summary>
            LightPanelDarkGlyph,
            /// <summary>Dark control panel + light glyph — active GlyphBrush selection when managed chrome applies.</summary>
            DarkPanelLightGlyph,
        }

        internal static SortHeaderHighlightAppearance GetSortHeaderHighlightAppearance(
            Color? headerLabelColor,
            Func<string, object> tryFindResource)
        {
            bool useDarkeningOverlay = PlaylistThemeColors.UseDarkeningOverlay(headerLabelColor, tryFindResource);
            (SolidColorBrush background, SolidColorBrush border, SolidColorBrush foreground) =
                PlaylistThemeColors.CreateActiveSortHighlightBrushes(useDarkeningOverlay);
            return new SortHeaderHighlightAppearance(background, border, foreground, useDarkeningOverlay);
        }

        internal static SortHeaderHighlightAppearance GetSortHeaderHighlightAppearance(GridViewColumnHeader sampleHeader)
        {
            return GetSortHeaderHighlightAppearance(TryGetHeaderLabelColor(sampleHeader), sampleHeader == null ? null : sampleHeader.TryFindResource);
        }

        internal static DropMarkerPalette GetDropMarkerPalette(
            Color? headerLabelColor,
            Func<string, object> tryFindResource = null)
        {
            (Color slotCenter, Color glyph) = PlaylistThemeColors.GetDropMarkerPaletteColors(
                PlaylistThemeColors.UseDarkeningOverlay(headerLabelColor, tryFindResource));
            return new DropMarkerPalette(slotCenter, glyph);
        }

        internal static DropMarkerPalette GetDropMarkerPalette(GridViewColumnHeader sampleHeader)
        {
            return GetDropMarkerPalette(
                TryGetHeaderLabelColor(sampleHeader),
                sampleHeader == null ? null : sampleHeader.TryFindResource);
        }

        internal static HltbEmptyTrackAppearance GetHltbEmptyTrackAppearance(
            ListViewItem row,
            bool isRowHoverActive,
            Func<string, object> tryFindResource)
        {
            return IsManagedDarkHoverRow(row, isRowHoverActive, tryFindResource)
                ? HltbEmptyTrackOnDarkRowAppearance
                : HltbEmptyTrackNormalAppearance;
        }

        internal static bool IsManagedDarkHoverRow(
            ListViewItem row,
            bool isHoverActive,
            Func<string, object> tryFindResource)
        {
            return GetRowEmbeddedChromeStyle(row, isHoverActive, tryFindResource) == RowEmbeddedChromeStyle.LightPanelDarkGlyph;
        }

        internal static RowEmbeddedChromeStyle GetRowEmbeddedChromeStyle(
            ListViewItem item,
            bool isHoverActive,
            Func<string, object> tryFindResource)
        {
            return GetRowEmbeddedChromeStyle(item, isHoverActive, isDirectHover: false, tryFindResource);
        }

        internal static RowEmbeddedChromeStyle GetRowEmbeddedChromeStyle(
            ListViewItem item,
            bool isHoverActive,
            bool isDirectHover,
            Func<string, object> tryFindResource)
        {
            if (item == null
                || !PlaylistThemeColors.UsesInvertedRowHighlightChrome(tryFindResource)
                || !TryGetVisibleRowHighlightColor(item, isHoverActive, tryFindResource, out _))
            {
                return RowEmbeddedChromeStyle.None;
            }

            if (item.IsSelected)
            {
                return RowEmbeddedChromeStyle.DarkPanelLightGlyph;
            }

            if (isHoverActive)
            {
                return RowEmbeddedChromeStyle.LightPanelDarkGlyph;
            }

            return RowEmbeddedChromeStyle.None;
        }

        /// <summary>
        /// Foreground that contrasts with a solid fill (segment labels, badges, etc.).
        /// </summary>
        internal static Color GetContrastTextColor(Color background)
            => PlaylistThemeColors.GetContrastTextColor(background);

        /// <summary>
        /// Samples inherited header label color without code-applied hover/active foreground overrides.
        /// </summary>
        internal static Color? TryGetHeaderLabelColor(GridViewColumnHeader header)
        {
            if (header == null)
            {
                return null;
            }

            if (header.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
            {
                Brush headerForeground = (Brush)header.GetValue(Control.ForegroundProperty);
                if (PlaylistThemeColors.TryGetColor(headerForeground, out Color headerColor) && headerColor.A > 16)
                {
                    return headerColor;
                }
            }

            header.ApplyTemplate();
            foreach (TextBlock textBlock in FindVisualChildren<TextBlock>(header))
            {
                if (textBlock.Visibility != Visibility.Visible)
                {
                    continue;
                }

                if (textBlock.Tag as string == "RankHeaderGlyph")
                {
                    continue;
                }

                if (textBlock.Text == "▲" || textBlock.Text == "▼")
                {
                    continue;
                }

                if (textBlock.ReadLocalValue(TextBlock.ForegroundProperty) != DependencyProperty.UnsetValue)
                {
                    continue;
                }

                if (PlaylistThemeColors.TryGetColor(textBlock.Foreground as Brush, out Color labelColor) && labelColor.A > 16)
                {
                    return labelColor;
                }

                return null;
            }

            return null;
        }

        /// <summary>
        /// Idle header chrome border after clearing a code-applied hover/active highlight.
        /// </summary>
        internal static Brush ResolveIdleHeaderBorderBrush(GridViewColumnHeader header)
        {
            if (header == null)
            {
                return null;
            }

            if (header.Tag is bool useDarkStyle)
            {
                string brushKey = useDarkStyle ? "DarkControlBorderBrush" : "ControlBorderBrush";
                if (header.TryFindResource(brushKey) is Brush resourceBorder)
                {
                    return resourceBorder;
                }
            }

            if (header.TryFindResource("NormalBorderBrush") is Brush normalBorder)
            {
                return normalBorder;
            }

            return null;
        }

        internal static string GetVisibleRowHighlightBrushKey(ListViewItem item, bool isHoverActive)
        {
            if (item == null)
            {
                return null;
            }

            // Match row template: selection fill wins over hover fill.
            if (item.IsSelected)
            {
                return "GlyphBrush";
            }

            if (isHoverActive)
            {
                return "HoverBrush";
            }

            return null;
        }

        internal static bool TryGetVisibleRowHighlightColor(
            ListViewItem item,
            bool isHoverActive,
            Func<string, object> tryFindResource,
            out Color color)
        {
            color = default;
            string brushKey = GetVisibleRowHighlightBrushKey(item, isHoverActive);
            if (brushKey == null)
            {
                return false;
            }

            if (!(tryFindResource?.Invoke(brushKey) is SolidColorBrush brush)
                || !PlaylistThemeColors.TryGetColor(brush, out color))
            {
                return false;
            }

            return true;
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
            {
                yield break;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T nestedChild in FindVisualChildren<T>(child))
                {
                    yield return nestedChild;
                }
            }
        }
    }
}
