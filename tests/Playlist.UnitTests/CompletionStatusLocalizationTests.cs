using Playlist.UnitTests.Localization;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class CompletionStatusLocalizationTests
{
    [Fact]
    public void LocalizeDisplayName_uses_supplemental_hawaiian_override_for_default_status_names()
    {
        PlaylistLocalizationTestPaths.RunWithSupplementalOverride(entries =>
        {
            Assert.Equal(entries["LOCCompletionStatusPlayed"], CompletionStatusLocalization.LocalizeDisplayName("Played"));
            Assert.Equal(entries["LOCCompletionStatusNotPlayed"], CompletionStatusLocalization.LocalizeDisplayName("Not Played"));
        });
    }

    [Fact]
    public void LocalizeDisplayName_returns_english_name_on_native_playnite_locale_path()
    {
        PlaylistLocalizationTestPaths.RunWithNativePlayniteProvider(
            PlaylistLocalizationTestPaths.NativePlayniteGermanStrings(),
            () =>
            {
                Assert.Equal("Played", CompletionStatusLocalization.LocalizeDisplayName("Played"));
                Assert.Equal("Not Played", CompletionStatusLocalization.LocalizeDisplayName("Not Played"));
            });
    }

    [Fact]
    public void LocalizeDisplayName_returns_custom_status_names_unchanged()
    {
        Assert.Equal("Endless", CompletionStatusLocalization.LocalizeDisplayName("Endless"));
    }
}
