using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Applies theme-responsive embedded-control chrome on playlist list rows.
    /// Style detection lives in <see cref="PlaylistThemeChrome"/>.
    /// </summary>
    internal static class PlaylistManagedRowChrome
    {
        /// <summary>
        /// Row text: light foreground when managed chrome applies on a dark HoverBrush mouseover row.
        /// </summary>
        internal static void ApplyListRowHighlightForeground(
            ListViewItem item,
            Func<string, object> tryFindResource,
            bool isHoverActive)
        {
            if (item == null
                || !PlaylistThemeColors.UsesInvertedRowHighlightChrome(tryFindResource)
                || item.IsSelected
                || !isHoverActive
                || !PlaylistThemeChrome.TryGetVisibleRowHighlightColor(item, isHoverActive, tryFindResource, out Color rowColor)
                || PlaylistThemeColors.IsLightForeground(rowColor))
            {
                item.ClearValue(Control.ForegroundProperty);
                return;
            }

            item.Foreground = PlaylistThemeColors.CreateActiveSortHighlightBrushes(useDarkeningOverlay: true).Foreground;
        }

        /// <summary>
        /// Managed play-button chrome when <see cref="PlaylistThemeColors.UsesInvertedRowHighlightChrome"/> is true
        /// (see docs/row-highlight-chrome-investigation.md). Otherwise use
        /// <c>playlistIconPlayButtonStyleThemed</c> and do not call this.
        /// </summary>
        internal static void ApplyListRowPlayButtonChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            Func<string, object> tryFindResource)
        {
            ApplyListRowPlayButtonChrome(control, row, isHoverActive, isDirectHover: false, tryFindResource);
        }

        internal static void ApplyListRowPlayButtonChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            bool isDirectHover,
            Func<string, object> tryFindResource)
        {
            if (control == null || !PlaylistThemeColors.UsesInvertedRowHighlightChrome(tryFindResource))
            {
                return;
            }

            PlaylistThemeChrome.RowEmbeddedChromeStyle baseStyle =
                PlaylistThemeChrome.GetRowEmbeddedChromeStyle(row, isHoverActive, isDirectHover, tryFindResource);
            switch (baseStyle)
            {
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.LightPanelDarkGlyph:
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    ApplyManagedEmbeddedChrome(tryFindResource, control, baseStyle, isDirectHover, useBaseBorder: true);
                    break;
                default:
                    ClearListRowControlChrome(control);
                    break;
            }
        }

        /// <summary>
        /// HLTB plugin button host: managed embedded chrome when <see cref="PlaylistThemeColors.UsesInvertedRowHighlightChrome"/> is true.
        /// </summary>
        internal static void ApplyListRowEmbeddedControlChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            Func<string, object> tryFindResource)
        {
            ApplyListRowEmbeddedControlChrome(control, row, isHoverActive, isDirectHover: false, tryFindResource);
        }

        internal static void ApplyListRowEmbeddedControlChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            bool isDirectHover,
            Func<string, object> tryFindResource)
        {
            if (control == null)
            {
                return;
            }

            if (!PlaylistThemeColors.UsesInvertedRowHighlightChrome(tryFindResource))
            {
                ClearEmbeddedChrome(control);
                return;
            }

            PlaylistThemeChrome.RowEmbeddedChromeStyle baseStyle =
                PlaylistThemeChrome.GetRowEmbeddedChromeStyle(row, isHoverActive, isDirectHover, tryFindResource);
            switch (baseStyle)
            {
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.LightPanelDarkGlyph:
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    ApplyManagedEmbeddedChrome(tryFindResource, control, baseStyle, isDirectHover, useBaseBorder: false);
                    break;
                default:
                    ClearEmbeddedChrome(control);
                    break;
            }
        }

        internal static void ClearListRowControlChrome(Control control)
        {
            ClearEmbeddedChrome(control);
        }

        /// <summary>
        /// Row-level chrome from <paramref name="baseStyle"/>; direct control hover inverts panel/glyph for clickability.
        /// </summary>
        private static void ApplyManagedEmbeddedChrome(
            Func<string, object> tryFindResource,
            Control control,
            PlaylistThemeChrome.RowEmbeddedChromeStyle baseStyle,
            bool isDirectHover,
            bool useBaseBorder)
        {
            PlaylistThemeChrome.RowEmbeddedChromeStyle appliedStyle = isDirectHover ? InvertEmbeddedChromeStyle(baseStyle) : baseStyle;
            bool showBorder = isDirectHover || useBaseBorder;
            ApplyEmbeddedChromeStyle(tryFindResource, control, appliedStyle, showBorder, isDirectHover);
        }

        private static PlaylistThemeChrome.RowEmbeddedChromeStyle InvertEmbeddedChromeStyle(PlaylistThemeChrome.RowEmbeddedChromeStyle style)
        {
            switch (style)
            {
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    return PlaylistThemeChrome.RowEmbeddedChromeStyle.LightPanelDarkGlyph;
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.LightPanelDarkGlyph:
                    return PlaylistThemeChrome.RowEmbeddedChromeStyle.DarkPanelLightGlyph;
                default:
                    return PlaylistThemeChrome.RowEmbeddedChromeStyle.None;
            }
        }

        private static void ApplyEmbeddedChromeStyle(
            Func<string, object> tryFindResource,
            Control control,
            PlaylistThemeChrome.RowEmbeddedChromeStyle style,
            bool showBorder,
            bool directHoverBorder)
        {
            Brush borderBrush = showBorder
                ? ResolveEmbeddedBorderBrush(tryFindResource, style, directHoverBorder)
                : null;

            switch (style)
            {
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.LightPanelDarkGlyph:
                    ApplyEmbeddedChrome(
                        control,
                        ResolveResourceBrush(tryFindResource, "PopupBackgroundBrush", "ControlBackgroundBrush"),
                        ResolveDarkGlyphBrush(tryFindResource),
                        borderBrush);
                    break;
                case PlaylistThemeChrome.RowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    ApplyEmbeddedChrome(
                        control,
                        ResolveDarkPanelBackground(tryFindResource),
                        PlaylistThemeColors.CreateActiveSortHighlightBrushes(useDarkeningOverlay: true).Foreground,
                        borderBrush);
                    break;
                default:
                    ClearEmbeddedChrome(control);
                    break;
            }
        }

        private static Brush ResolveDarkGlyphBrush(Func<string, object> tryFindResource)
        {
            Brush textBrush = ResolveResourceBrush(tryFindResource, "TextBrush");
            if (textBrush is SolidColorBrush solidText
                && PlaylistThemeColors.TryGetColor(solidText, out Color textColor)
                && PlaylistThemeColors.IsDarkForeground(textColor))
            {
                return textBrush;
            }

            return PlaylistThemeColors.EmbeddedChromeDarkGlyphFallback;
        }

        private static Brush ResolveEmbeddedBorderBrush(
            Func<string, object> tryFindResource,
            PlaylistThemeChrome.RowEmbeddedChromeStyle style,
            bool directHoverBorder)
        {
            if (directHoverBorder)
            {
                if (style == PlaylistThemeChrome.RowEmbeddedChromeStyle.LightPanelDarkGlyph)
                {
                    return ResolveResourceBrush(tryFindResource, "HoverBrush", "ControlBorderBrush", "DarkControlBorderBrush");
                }

                // Dark panel over dark row highlight — light outer border for contrast.
                return ResolveResourceBrush(tryFindResource, "GlyphBrush", "PopupBackgroundBrush", "ControlBackgroundBrush", "ControlBorderBrush");
            }

            if (style == PlaylistThemeChrome.RowEmbeddedChromeStyle.LightPanelDarkGlyph)
            {
                return ResolveResourceBrush(tryFindResource, "ControlBorderBrush", "DarkControlBorderBrush");
            }

            return ResolveResourceBrush(tryFindResource, "DarkControlBorderBrush", "ControlBorderBrush");
        }

        private static readonly Thickness StableEmbeddedBorderThickness = new Thickness(1);

        private static void ApplyEmbeddedChrome(Control control, Brush background, Brush foreground, Brush borderBrush)
        {
            if (background != null)
            {
                control.Background = background;
            }

            if (foreground != null)
            {
                control.Foreground = foreground;
            }

            control.BorderThickness = StableEmbeddedBorderThickness;
            control.BorderBrush = borderBrush ?? Brushes.Transparent;
        }

        private static void ClearEmbeddedChrome(Control control)
        {
            control.ClearValue(Control.BackgroundProperty);
            control.ClearValue(Control.ForegroundProperty);
            control.BorderBrush = Brushes.Transparent;
            control.BorderThickness = new Thickness(0);
        }

        private static Brush ResolveResourceBrush(Func<string, object> tryFindResource, params string[] keys)
        {
            if (tryFindResource == null)
            {
                return null;
            }

            foreach (string key in keys)
            {
                if (tryFindResource.Invoke(key) is Brush brush)
                {
                    return brush;
                }
            }

            return null;
        }

        private static Brush ResolveDarkPanelBackground(Func<string, object> tryFindResource)
        {
            Brush hoverBrush = ResolveResourceBrush(tryFindResource, "HoverBrush");
            if (hoverBrush is SolidColorBrush solidHover
                && PlaylistThemeColors.TryGetColor(solidHover, out Color hoverColor)
                && PlaylistThemeColors.IsDarkForeground(hoverColor))
            {
                return hoverBrush;
            }

            return PlaylistThemeColors.EmbeddedChromeDarkPanelFallback;
        }
    }
}
