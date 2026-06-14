using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Playlist.UnitTests;

public class PlaylistLocaleCultureMapTests
{
    [Theory]
    [InlineData("chr-Cher-US", "chr_US")]
    [InlineData("iu-Latn-CA", "iu_CA")]
    [InlineData("eu-ES", "eu_ES")]
    [InlineData("moh-CA", "moh_CA")]
    [InlineData("lkt-US", "lkt_US")]
    [InlineData("nv-US", "nv_US")]
    [InlineData("cr-Latn-CA", "cr_CA")]
    [InlineData("oj-Latn-CA", "oj_CA")]
    [InlineData("cy-GB", "cy_GB")]
    [InlineData("ga-IE", "ga_IE")]
    [InlineData("gd-GB", "gd_GB")]
    [InlineData("sco-GB", "sco_GB")]
    [InlineData("haw-US", "haw_US")]
    [InlineData("mi-NZ", "mi_NZ")]
    [InlineData("sm-WS", "sm_WS")]
    [InlineData("sm-AS", "sm_WS")]
    [InlineData("gn-PY", "gn_PY")]
    public void TryResolveLocaleFromCulture_maps_os_culture_names(string cultureName, string expectedLocaleId)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.True(PlaylistLocaleCultureMap.TryResolveLocaleFromCulture(culture, out string localeId));
        Assert.Equal(expectedLocaleId, localeId);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void TryResolveLocaleFromCulture_returns_false_for_unmapped_playnite_locales(string cultureName)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.False(PlaylistLocaleCultureMap.TryResolveLocaleFromCulture(culture, out string localeId));
        Assert.Null(localeId);
    }

    [Fact]
    public void Every_supplemental_locale_has_os_culture_mapping()
    {
        IReadOnlyDictionary<string, string> mappings = PlaylistLocaleCultureMap.GetCultureNameMappings();
        var localesCovered = new HashSet<string>(mappings.Values, System.StringComparer.OrdinalIgnoreCase);

        foreach (string supplementalLocale in PlaylistLocaleCultureMap.GetSupplementalLocaleIds())
        {
            Assert.Contains(supplementalLocale, localesCovered);
        }
    }

    [Theory]
    [InlineData("gd_GB", true)]
    [InlineData("sco_GB", true)]
    [InlineData("cy_GB", false)]
    [InlineData("ga_IE", false)]
    [InlineData("de_DE", false)]
    public void IsSupplementalLocale_distinguishes_supplemental_from_playnite_locales(string localeId, bool expected)
    {
        Assert.Equal(expected, PlaylistLocaleCultureMap.IsSupplementalLocale(localeId));
    }

    [Fact]
    public void Supplemental_locale_ids_match_marker_file()
    {
        string localizationDir = Localization.LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        string markerPath = Path.Combine(localizationDir, ".supplemental-locales");
        var markerLocales = File.ReadAllLines(markerPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .OrderBy(line => line, System.StringComparer.Ordinal)
            .ToArray();

        var catalogLocales = PlaylistLocaleCultureMap.GetSupplementalLocaleIds()
            .OrderBy(line => line, System.StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(markerLocales, catalogLocales);
    }

    [Fact]
    public void Samoan_locale_maps_multiple_windows_region_patterns_to_one_playlist_locale()
    {
        PlaylistLocaleCultureMap.LocaleEntry entry = PlaylistLocaleCultureMap.TryGetLocaleEntry("sm_WS");
        Assert.NotNull(entry);
        Assert.Contains("sm-WS", entry.OsCulturePatterns);
        Assert.Contains("sm-AS", entry.OsCulturePatterns);
        Assert.Contains("sm", entry.OsCulturePatterns);
    }

    [Fact]
    public void Os_locale_mismatch_prompt_is_configured_for_settings_dialog()
    {
        PlaylistLocaleCultureMap.OsLocaleMismatchPrompt prompt = PlaylistLocaleCultureMap.GetOsLocaleMismatchPrompt();
        Assert.False(string.IsNullOrWhiteSpace(prompt.MessageKey));
        Assert.Contains("{0}", prompt.FallbackMessage);
        Assert.Contains("{1}", prompt.FallbackMessage);
    }

    [Fact]
    public void Culture_map_json_lists_all_supplemental_marker_locales()
    {
        string localizationDir = Localization.LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        string markerPath = Path.Combine(localizationDir, ".supplemental-locales");
        var markerLocales = new HashSet<string>(
            File.ReadAllLines(markerPath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        foreach (string localeId in PlaylistLocaleCultureMap.GetSupplementalLocaleIds())
        {
            Assert.Contains(localeId, markerLocales);
        }
    }

    [Fact]
    public void Culture_map_json_has_no_remaining_documented_windows_gaps()
    {
        string mapPath = Path.Combine(
            Localization.LocalizationXamlTestReader.FindRepoLocalizationDirectory(),
            "..",
            "scripts",
            "data",
            "playlist-os-locale-culture-map.json");
        mapPath = Path.GetFullPath(mapPath);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(mapPath));
        int gapCount = document.RootElement.GetProperty("windowsEndangeredGaps").GetArrayLength();
        Assert.Equal(0, gapCount);
    }
}
