using System.Globalization;
using Xunit;

namespace Playlist.UnitTests;

public class PlaylistLanguageOptionCatalogTests
{
    [Fact]
    public void BuildOptions_orders_playnite_os_then_supplemental_alphabetically()
    {
        IReadOnlyList<PlaylistLanguageOption> options = PlaylistLanguageOptionCatalog.BuildOptions(
            "en_US",
            CultureInfo.GetCultureInfo("gd-GB"));

        Assert.Equal(PlaylistLanguageOptionKind.Playnite, options[0].Kind);
        Assert.Equal(string.Empty, options[0].LocaleId);
        Assert.Contains("English", options[0].DisplayName, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(PlaylistLanguageOptionKind.Os, options[1].Kind);
        Assert.Equal("gd_GB", options[1].LocaleId);
        Assert.Equal("Gàidhlig", options[1].DisplayName);

        Assert.True(options.Count >= 15);
        Assert.All(options.Skip(2), option => Assert.Equal(PlaylistLanguageOptionKind.Supplemental, option.Kind));
        Assert.Equal(
            options.Skip(2).Select(option => option.DisplayName).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase),
            options.Skip(2).Select(option => option.DisplayName));
        Assert.DoesNotContain(options.Skip(2), option => string.Equals(option.LocaleId, "en_US", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(options.Skip(2), option => string.Equals(option.LocaleId, "ga_IE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildOptions_omits_os_choice_when_os_matches_playnite_language()
    {
        IReadOnlyList<PlaylistLanguageOption> options = PlaylistLanguageOptionCatalog.BuildOptions(
            "ga_IE",
            CultureInfo.GetCultureInfo("ga-IE"));

        Assert.Equal(string.Empty, options[0].LocaleId);
        Assert.Equal("Gaeilge", options[0].DisplayName);
        Assert.DoesNotContain(options, option => option.Kind == PlaylistLanguageOptionKind.Os);
    }

    [Fact]
    public void BuildOptions_omits_os_choice_when_os_is_unmapped_playnite_locale()
    {
        IReadOnlyList<PlaylistLanguageOption> options = PlaylistLanguageOptionCatalog.BuildOptions(
            "en_US",
            CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal(1, options.Count(option => option.Kind == PlaylistLanguageOptionKind.Playnite));
        Assert.DoesNotContain(options, option => option.Kind == PlaylistLanguageOptionKind.Os);
    }

    [Fact]
    public void ShouldOfferOsLocaleMismatchPrompt_matches_configured_dialog_criteria()
    {
        Assert.True(PlaylistLanguageOptionCatalog.ShouldOfferOsLocaleMismatchPrompt(
            hasPrompted: false,
            playniteLanguage: "en_US",
            osUiCulture: CultureInfo.GetCultureInfo("gd-GB"),
            out string osLocaleId));
        Assert.Equal("gd_GB", osLocaleId);

        Assert.False(PlaylistLanguageOptionCatalog.ShouldOfferOsLocaleMismatchPrompt(
            hasPrompted: true,
            playniteLanguage: "en_US",
            osUiCulture: CultureInfo.GetCultureInfo("gd-GB"),
            out _));

        Assert.False(PlaylistLanguageOptionCatalog.ShouldOfferOsLocaleMismatchPrompt(
            hasPrompted: false,
            playniteLanguage: "ga_IE",
            osUiCulture: CultureInfo.GetCultureInfo("ga-IE"),
            out _));

        Assert.False(PlaylistLanguageOptionCatalog.ShouldOfferOsLocaleMismatchPrompt(
            hasPrompted: false,
            playniteLanguage: "en_US",
            osUiCulture: CultureInfo.GetCultureInfo("de-DE"),
            out _));
    }

    [Fact]
    public void FormatOsLocaleMismatchPrompt_uses_configured_placeholders()
    {
        string message = PlaylistLanguageOptionCatalog.FormatOsLocaleMismatchPrompt(
            "en_US",
            "gd_GB",
            CultureInfo.GetCultureInfo("gd-GB"));

        Assert.Contains("Scottish Gaelic", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Gàidhlig", message);
    }
}
