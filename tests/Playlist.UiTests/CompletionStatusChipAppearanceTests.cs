using System;
using System.Windows.Media;
using Xunit;

namespace Playlist.UiTests;

public class CompletionStatusChipAppearanceTests
{
    [Fact]
    public void GetCompletionStatusChipAppearance_syncable_normal_row_uses_glyph_tint_on_light_themes()
    {
        Func<string, object> lightThemeResources = CreateLightThemeResources();

        PlaylistThemeChrome.CompletionStatusChipAppearance appearance =
            PlaylistThemeChrome.GetCompletionStatusChipAppearance(
                isSyncableTier: true,
                row: null,
                isRowHoverActive: false,
                lightThemeResources);

        Assert.Equal(PlaylistThemeColors.SyncableChipBackgroundAlpha, appearance.Background.Color.A);
        Assert.Equal(0x50, appearance.Background.Color.R);
        Assert.Equal(0xA0, appearance.Background.Color.G);
        Assert.Equal(0xE8, appearance.Background.Color.B);
        Assert.Equal(1.0, appearance.ForegroundOpacity);
    }

    [Fact]
    public void GetCompletionStatusChipAppearance_non_syncable_normal_row_uses_empty_track_chrome_on_light_themes()
    {
        PlaylistThemeChrome.CompletionStatusChipAppearance appearance =
            PlaylistThemeChrome.GetCompletionStatusChipAppearance(
                isSyncableTier: false,
                row: null,
                isRowHoverActive: false,
                CreateLightThemeResources());

        Assert.Equal(PlaylistThemeColors.EmptyHltbTrackFillNormalColor, appearance.Background.Color);
        Assert.Equal(PlaylistThemeColors.EmptyHltbTrackBorderNormalColor, appearance.Border.Color);
        Assert.Equal(PlaylistThemeColors.NonSyncableChipForegroundOpacity, appearance.ForegroundOpacity);
    }

    [Fact]
    public void UsesInvertedRowHighlightChrome_detects_managed_row_chrome_on_dark_themes()
    {
        Assert.True(PlaylistThemeColors.UsesInvertedRowHighlightChrome(CreateDarkThemeResources()));
        Assert.False(PlaylistThemeColors.UsesInvertedRowHighlightChrome(CreateLightThemeResources()));
    }

    private static Func<string, object> CreateLightThemeResources()
    {
        return key =>
        {
            switch (key)
            {
                case "GlyphBrush":
                    return new SolidColorBrush(Color.FromRgb(0x50, 0xA0, 0xE8));
                case "TextBrush":
                    return new SolidColorBrush(Colors.White);
                case "HoverBrush":
                    return new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
                default:
                    return null;
            }
        };
    }

    private static Func<string, object> CreateDarkThemeResources()
    {
        return key =>
        {
            switch (key)
            {
                case "HoverBrush":
                    return new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
                case "GlyphBrush":
                    return new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                case "TextBrush":
                    return new SolidColorBrush(Colors.White);
                case "PopupBackgroundBrush":
                    return new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                case "ControlBorderBrush":
                    return new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));
                default:
                    return null;
            }
        };
    }
}
