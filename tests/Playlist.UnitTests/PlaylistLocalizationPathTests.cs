using Playlist.UnitTests.Localization;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class PlaylistLocalizationPathTests
{
    [Fact]
    public void GetString_uses_supplemental_hawaiian_override_for_playlist_and_borrowed_keys()
    {
        PlaylistLocalizationTestPaths.RunWithSupplementalOverride(entries =>
        {
            Assert.Equal(entries["LOCPlaylist_Column_Rank"], PlaylistLocalization.GetString("LOCPlaylist_Column_Rank"));
            Assert.Equal(entries["LOCTimePlayed"], PlaylistLocalization.GetString("LOCTimePlayed"));
            Assert.Equal(entries["LOCCompletionStatus"], PlaylistLocalization.GetString("LOCCompletionStatus"));
        });
    }

    [Fact]
    public void GetString_uses_native_playnite_resource_provider_when_no_override()
    {
        IReadOnlyDictionary<string, string> german = PlaylistLocalizationTestPaths.NativePlayniteGermanStrings();
        PlaylistLocalizationTestPaths.RunWithNativePlayniteProvider(german, () =>
        {
            Assert.Equal(german["LOCPlaylist_Column_Rank"], PlaylistLocalization.GetString("LOCPlaylist_Column_Rank"));
            Assert.Equal(german["LOCTimePlayed"], PlaylistLocalization.GetString("LOCTimePlayed"));
            Assert.Equal(german["LOCCompletionStatus"], PlaylistLocalization.GetString("LOCCompletionStatus"));
        });
    }

    [Fact]
    public void GetColumnLabel_resolves_borrowed_playnite_headers_on_both_locale_paths()
    {
        IReadOnlyDictionary<string, string> german = PlaylistLocalizationTestPaths.NativePlayniteGermanStrings();
        PlaylistLocalizationTestPaths.RunWithNativePlayniteProvider(german, () =>
        {
            Assert.Equal(
                german["LOCTimePlayed"],
                PlaylistColumnVisibilitySettings.GetColumnLabel(PlaylistColumnWidthLayout.PlaytimeColumnKey));
            Assert.Equal(
                german["LOCCompletionStatus"],
                PlaylistColumnVisibilitySettings.GetColumnLabel(PlaylistColumnWidthLayout.CompletionStatusColumnKey));
        });

        PlaylistLocalizationTestPaths.RunWithSupplementalOverride(entries =>
        {
            Assert.Equal(
                entries["LOCTimePlayed"],
                PlaylistColumnVisibilitySettings.GetColumnLabel(PlaylistColumnWidthLayout.PlaytimeColumnKey));
            Assert.Equal(
                entries["LOCCompletionStatus"],
                PlaylistColumnVisibilitySettings.GetColumnLabel(PlaylistColumnWidthLayout.CompletionStatusColumnKey));
        });
    }

    [Fact]
    public void GetColumnLabel_resolves_playlist_owned_rank_column_on_both_locale_paths()
    {
        IReadOnlyDictionary<string, string> german = PlaylistLocalizationTestPaths.NativePlayniteGermanStrings();
        PlaylistLocalizationTestPaths.RunWithNativePlayniteProvider(german, () =>
        {
            Assert.Equal(
                german["LOCPlaylist_Column_Rank"],
                PlaylistColumnVisibilitySettings.GetColumnLabel(PlaylistColumnWidthLayout.RankColumnKey));
        });

        PlaylistLocalizationTestPaths.RunWithSupplementalOverride(entries =>
        {
            Assert.Equal(
                entries["LOCPlaylist_Column_Rank"],
                PlaylistColumnVisibilitySettings.GetColumnLabel(PlaylistColumnWidthLayout.RankColumnKey));
        });
    }
}
