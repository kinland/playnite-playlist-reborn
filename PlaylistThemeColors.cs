using System;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Shared theme brush sampling, luminance classification, and playlist-owned palette constants.
    /// HLTB segment colors come from <see cref="HltbRenderSettings"/> (HLTB plugin settings), not here.
    /// </summary>
    internal static class PlaylistThemeColors
    {
        #region Palette constants

        /// <summary>
        /// ITU-R BT.601 luma weights for RGB → perceived brightness (0–255 scale before normalization).
        /// Used by <see cref="GetRelativeLuminance"/> and <see cref="GetContrastTextColorFromByteLuminance"/>.
        /// </summary>
        internal const double RelativeLuminanceRed = 0.299;
        internal const double RelativeLuminanceGreen = 0.587;
        internal const double RelativeLuminanceBlue = 0.114;

        /// <summary>
        /// Luminance cutoff below which foreground reads as dark on light chrome backgrounds.
        /// </summary>
        internal const double DarkForegroundLuminanceThreshold = 0.5;

        /// <summary>
        /// Byte-scale luminance cutoff for quick contrast text on segment fills (0–255 channel sum).
        /// </summary>
        internal const double ReadableTextByteLuminanceThreshold = 150.0;

        internal const int LowChromaHighlightDelta = 45;

        internal static readonly Color ActiveSortHighlightForegroundColor = Colors.White;

        internal static readonly Color ActiveSortDarkOverlayBackgroundColor = Color.FromArgb(0x99, 0x00, 0x00, 0x00);
        internal static readonly Color ActiveSortDarkOverlayBorderColor = Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A);
        internal static readonly Color ActiveSortLightOverlayBackgroundColor = Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
        internal static readonly Color ActiveSortLightOverlayBorderColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        internal static readonly Color DropMarkerDarkSlotCenterColor = Color.FromArgb(0xBE, 0x00, 0x66, 0xCC);
        internal static readonly Color DropMarkerDarkGlyphColor = Color.FromRgb(0x00, 0x66, 0xCC);
        internal static readonly Color DropMarkerLightSlotCenterColor = Color.FromArgb(0xC8, 0x50, 0xC8, 0xFF);
        internal static readonly Color DropMarkerLightGlyphColor = Color.FromRgb(0x78, 0xDC, 0xFF);

        internal static readonly Color ContrastTextOnLightFillColor = Color.FromRgb(0x14, 0x14, 0x14);
        internal static readonly Color ContrastTextOnDarkFillColor = Color.FromRgb(0xF5, 0xF5, 0xF5);

        internal static readonly Color EmbeddedChromeDarkGlyphFallbackColor = Color.FromRgb(0x18, 0x18, 0x18);
        internal static readonly Color EmbeddedChromeDarkPanelFallbackColor = Color.FromRgb(0x2A, 0x2C, 0x30);

        /// <summary>
        /// Empty HLTB progress track (-- state) on a normal row. Playlist row chrome, not HLTB segment colors.
        /// </summary>
        internal static readonly Color EmptyHltbTrackFillNormalColor = Color.FromArgb(0x46, 0x0A, 0x14, 0x1E);
        internal static readonly Color EmptyHltbTrackFillOnDarkRowColor = Color.FromArgb(0xC8, 0xE1, 0xE4, 0xE8);
        internal static readonly Color EmptyHltbTrackBorderNormalColor = Color.FromArgb(0x8C, 0x50, 0x5A, 0x64);
        internal static readonly Color EmptyHltbTrackBorderOnDarkRowColor = Color.FromArgb(0xDC, 0x78, 0x80, 0x8A);

        internal const byte SegmentOutlineAlpha = 0xD2;
        internal const byte SegmentOutlineDarkenAmount = 0x1E;

        private static readonly SolidColorBrush EmptyHltbTrackFillNormalBrush = CreateFrozenBrush(EmptyHltbTrackFillNormalColor);
        private static readonly SolidColorBrush EmptyHltbTrackFillOnDarkRowBrush = CreateFrozenBrush(EmptyHltbTrackFillOnDarkRowColor);
        private static readonly SolidColorBrush EmptyHltbTrackBorderNormalBrush = CreateFrozenBrush(EmptyHltbTrackBorderNormalColor);
        private static readonly SolidColorBrush EmptyHltbTrackBorderOnDarkRowBrush = CreateFrozenBrush(EmptyHltbTrackBorderOnDarkRowColor);
        private static readonly SolidColorBrush EmbeddedChromeDarkGlyphFallbackBrush = CreateFrozenBrush(EmbeddedChromeDarkGlyphFallbackColor);
        private static readonly SolidColorBrush EmbeddedChromeDarkPanelFallbackBrush = CreateFrozenBrush(EmbeddedChromeDarkPanelFallbackColor);

        #endregion

        internal static SolidColorBrush EmptyHltbTrackFillNormal => EmptyHltbTrackFillNormalBrush;
        internal static SolidColorBrush EmptyHltbTrackFillOnDarkRow => EmptyHltbTrackFillOnDarkRowBrush;
        internal static SolidColorBrush EmptyHltbTrackBorderNormal => EmptyHltbTrackBorderNormalBrush;
        internal static SolidColorBrush EmptyHltbTrackBorderOnDarkRow => EmptyHltbTrackBorderOnDarkRowBrush;
        internal static SolidColorBrush EmbeddedChromeDarkGlyphFallback => EmbeddedChromeDarkGlyphFallbackBrush;
        internal static SolidColorBrush EmbeddedChromeDarkPanelFallback => EmbeddedChromeDarkPanelFallbackBrush;

        /// <summary>
        /// True when <c>HoverBrush</c> is dark and low-chroma and <c>GlyphBrush</c> is light and low-chroma.
        /// When false, list rows keep resource-dictionary styling for embedded controls.
        /// </summary>
        internal static bool UsesInvertedRowHighlightChrome(Func<string, object> tryFindResource)
        {
            if (!(tryFindResource?.Invoke("HoverBrush") is SolidColorBrush hoverBrush)
                || !(tryFindResource?.Invoke("GlyphBrush") is SolidColorBrush glyphBrush)
                || !TryGetColor(hoverBrush, out Color hoverColor)
                || !TryGetColor(glyphBrush, out Color glyphColor))
            {
                return false;
            }

            return IsDarkForeground(hoverColor)
                && IsLightForeground(glyphColor)
                && IsLowChromaHighlightColor(hoverColor)
                && IsLowChromaHighlightColor(glyphColor);
        }

        /// <summary>
        /// True when a solid color reads as dark text or fill on a light panel.
        /// </summary>
        internal static bool IsDarkForeground(Color color)
        {
            return GetRelativeLuminance(color) < DarkForegroundLuminanceThreshold;
        }

        /// <summary>
        /// True when a solid color reads as light text or fill on a dark panel.
        /// </summary>
        internal static bool IsLightForeground(Color color)
        {
            return GetRelativeLuminance(color) >= DarkForegroundLuminanceThreshold;
        }

        /// <summary>
        /// Chooses a darkening overlay when header text reads dark, otherwise a lightening overlay.
        /// </summary>
        internal static bool UseDarkeningOverlay(Color? headerTextColor, Func<string, object> tryFindResource = null)
        {
            if (headerTextColor.HasValue)
            {
                return IsDarkForeground(headerTextColor.Value);
            }

            if (tryFindResource != null && tryFindResource("TextBrush") is SolidColorBrush textBrush)
            {
                return IsDarkForeground(textBrush.Color);
            }

            return false;
        }

        /// <summary>
        /// Brushes used for active sort-header and selected-row emphasis overlays.
        /// </summary>
        internal static (SolidColorBrush Background, SolidColorBrush Border, SolidColorBrush Foreground) CreateActiveSortHighlightBrushes(bool useDarkeningOverlay)
        {
            Color backgroundColor = useDarkeningOverlay
                ? ActiveSortDarkOverlayBackgroundColor
                : ActiveSortLightOverlayBackgroundColor;
            Color borderColor = useDarkeningOverlay
                ? ActiveSortDarkOverlayBorderColor
                : ActiveSortLightOverlayBorderColor;

            var background = new SolidColorBrush(backgroundColor);
            var border = new SolidColorBrush(borderColor);
            var foreground = new SolidColorBrush(ActiveSortHighlightForegroundColor);
            background.Freeze();
            border.Freeze();
            foreground.Freeze();
            return (background, border, foreground);
        }

        internal static (Color SlotCenter, Color Glyph) GetDropMarkerPaletteColors(bool useDarkeningOverlay)
        {
            return useDarkeningOverlay
                ? (DropMarkerDarkSlotCenterColor, DropMarkerDarkGlyphColor)
                : (DropMarkerLightSlotCenterColor, DropMarkerLightGlyphColor);
        }

        internal static Color GetContrastTextColorFromByteLuminance(Color background)
        {
            double luminance = (RelativeLuminanceRed * background.R)
                + (RelativeLuminanceGreen * background.G)
                + (RelativeLuminanceBlue * background.B);
            return luminance >= ReadableTextByteLuminanceThreshold
                ? ContrastTextOnLightFillColor
                : ContrastTextOnDarkFillColor;
        }

        internal static Color GetSegmentOutlineColor(Color fill)
        {
            byte r = (byte)Math.Max(0, fill.R - SegmentOutlineDarkenAmount);
            byte g = (byte)Math.Max(0, fill.G - SegmentOutlineDarkenAmount);
            byte b = (byte)Math.Max(0, fill.B - SegmentOutlineDarkenAmount);
            return Color.FromArgb(SegmentOutlineAlpha, r, g, b);
        }

        internal static double GetRelativeLuminance(Color color)
        {
            return ((RelativeLuminanceRed * color.R) + (RelativeLuminanceGreen * color.G) + (RelativeLuminanceBlue * color.B)) / 255.0;
        }

        internal static bool TryGetColor(Brush brush, out Color color)
        {
            color = default;
            if (brush is SolidColorBrush solid)
            {
                color = solid.Color;
                return true;
            }

            return false;
        }

        /// <summary>
        /// True for low-chroma fills used as row hover/selection backgrounds on managed-chrome themes.
        /// </summary>
        internal static bool IsLowChromaHighlightColor(Color color)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));
            return max - min < LowChromaHighlightDelta;
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
