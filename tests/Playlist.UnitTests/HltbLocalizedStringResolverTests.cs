using Moq;
using Playnite.SDK;
using Playlist.UnitTests.Localization;
using System.Globalization;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class HltbLocalizedStringResolverTests : IDisposable
{
    private readonly Mock<IResourceProvider> resourceProvider = new();

    public HltbLocalizedStringResolverTests()
    {
        PlaylistLocalizationOverride.SetActiveLocale(null);
        HltbLocalizedStringResolver.TestResourceProvider = resourceProvider.Object;
    }

    public void Dispose()
    {
        HltbLocalizedStringResolver.TestResourceProvider = null;
        PlaylistLocalizationOverride.SetActiveLocale(null);
    }

    [Theory]
    [InlineData("Histoire principale", "LOCHowLongToBeatMainStory", "Main story", "fr-FR", true)]
    [InlineData("Main story", "LOCHowLongToBeatMainStory", "Main story", "de-DE", false)]
    [InlineData("Main story", "LOCHowLongToBeatMainStory", "Main story", "en-US", true)]
    [InlineData("LOCHowLongToBeatMainStory", "LOCHowLongToBeatMainStory", "Main story", "fr-FR", false)]
    [InlineData("", "LOCHowLongToBeatMainStory", "Main story", "fr-FR", false)]
    [InlineData("メインストーリー", "LOCHowLongToBeatMainStory", "Main story", "ja-JP", true)]
    public void ShouldPreferHltbValue_respects_translation_and_ui_culture(
        string hltbValue,
        string hltbResourceKey,
        string englishBaseline,
        string uiCulture,
        bool expected)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
            Assert.Equal(
                expected,
                HltbLocalizedStringResolver.ShouldPreferHltbValue(hltbValue, hltbResourceKey, englishBaseline));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void ShouldPreferHltbValue_allows_unbaselined_values_for_non_english_cultures()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            Assert.True(HltbLocalizedStringResolver.ShouldPreferHltbValue(
                "攻关历时 - How Long To Beat",
                "LOCHowLongToBeat",
                string.Empty));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Resolve_prefers_playlist_override_when_supplemental_locale_is_active()
    {
        const string hltbKey = "LOCHowLongToBeatMainStory";
        const string playlistKey = "LOCPlaylist_HLTB_TimeType_MainStory";
        const string englishBaseline = "Main story";

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            resourceProvider.Setup(provider => provider.GetString(hltbKey)).Returns(englishBaseline);
            resourceProvider.Setup(provider => provider.GetString(playlistKey)).Returns(playlistKey);

            PlaylistLocalizationTestPaths.RunWithSupplementalOverride(entries =>
            {
                Assert.Equal(
                    entries[playlistKey],
                    HltbLocalizedStringResolver.Resolve(hltbKey, playlistKey, englishBaseline));
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Resolve_falls_back_to_english_baseline_when_hltb_and_playlist_are_missing()
    {
        const string hltbKey = "LOCHowLongToBeatMainStory";
        const string playlistKey = "LOCPlaylist_HLTB_TimeType_MainStory";
        const string englishBaseline = "Main story";

        resourceProvider.Setup(provider => provider.GetString(hltbKey)).Returns(hltbKey);
        resourceProvider.Setup(provider => provider.GetString(playlistKey)).Returns(playlistKey);

        Assert.Equal(
            englishBaseline,
            HltbLocalizedStringResolver.Resolve(hltbKey, playlistKey, englishBaseline));
    }

    [Fact]
    public void Resolve_uses_playlist_fallback_when_hltb_left_english_on_non_english_ui()
    {
        const string hltbKey = "LOCHowLongToBeatMainStory";
        const string playlistKey = "LOCPlaylist_HLTB_TimeType_MainStory";
        const string englishBaseline = "Main story";
        const string playlistValue = "Hauptgeschichte";

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            resourceProvider.Setup(provider => provider.GetString(hltbKey)).Returns(englishBaseline);
            resourceProvider.Setup(provider => provider.GetString(playlistKey)).Returns(playlistValue);

            Assert.Equal(
                playlistValue,
                HltbLocalizedStringResolver.Resolve(hltbKey, playlistKey, englishBaseline));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Resolve_uses_hltb_translation_when_playlist_key_is_absent()
    {
        const string hltbKey = "LOCHowLongToBeatMainStory";
        const string playlistKey = "LOCPlaylist_HLTB_TimeType_MainStory";
        const string englishBaseline = "Main story";
        const string hltbValue = "メインストーリー";

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
            resourceProvider.Setup(provider => provider.GetString(hltbKey)).Returns(hltbValue);
            resourceProvider.Setup(provider => provider.GetString(playlistKey))
                .Returns("<!" + playlistKey + "!>");

            Assert.Equal(
                hltbValue,
                HltbLocalizedStringResolver.Resolve(hltbKey, playlistKey, englishBaseline));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Resolve_uses_hltb_english_on_english_ui_when_playlist_key_is_absent()
    {
        const string hltbKey = "LOCHowLongToBeatMainStory";
        const string playlistKey = "LOCPlaylist_HLTB_TimeType_MainStory";
        const string englishBaseline = "Main story";

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            resourceProvider.Setup(provider => provider.GetString(hltbKey)).Returns(englishBaseline);
            resourceProvider.Setup(provider => provider.GetString(playlistKey)).Returns(playlistKey);

            Assert.Equal(
                englishBaseline,
                HltbLocalizedStringResolver.Resolve(hltbKey, playlistKey, englishBaseline));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
