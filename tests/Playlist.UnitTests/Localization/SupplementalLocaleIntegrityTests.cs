using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Playlist.UnitTests.Localization;

public class SupplementalLocaleIntegrityTests
{
    [Fact]
    public void Supplemental_locales_are_listed_in_marker_file()
    {
        string localizationDir = LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        string markerPath = Path.Combine(localizationDir, ".supplemental-locales");
        Assert.True(File.Exists(markerPath), "Missing .supplemental-locales marker file.");

        var registered = new HashSet<string>(
            File.ReadAllLines(markerPath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        foreach (string localePath in LocalizationXamlTestReader.EnumerateLocaleFiles(localizationDir, includeSupplemental: true))
        {
            string locale = Path.GetFileNameWithoutExtension(localePath);
            if (registered.Contains(locale))
            {
                Assert.True(File.Exists(localePath), $"Marker lists supplemental locale {locale} but file is missing.");
            }
        }

        Assert.True(registered.Count > 0, "Expected at least one supplemental locale.");
    }

    [Theory]
    [MemberData(nameof(SupplementalLocaleFiles))]
    public void Supplemental_locale_includes_borrowed_playnite_keys(string localePath)
    {
        IReadOnlyDictionary<string, string> locale = LocalizationXamlTestReader.ReadEntries(localePath);
        foreach (string borrowedKey in PlaylistBorrowedPlayniteKeys.All)
        {
            Assert.True(
                locale.ContainsKey(borrowedKey),
                $"{Path.GetFileNameWithoutExtension(localePath)} missing borrowed key {borrowedKey}.");
        }
    }

    [Theory]
    [MemberData(nameof(SupplementalLocaleFiles))]
    public void Supplemental_locale_does_not_include_deprecated_hltb_plugin_keys(string localePath)
    {
        IReadOnlyDictionary<string, string> locale = LocalizationXamlTestReader.ReadEntries(localePath);
        foreach (string deprecatedKey in PlaylistBorrowedPlayniteKeys.DeprecatedHltbPluginKeys)
        {
            Assert.False(
                locale.ContainsKey(deprecatedKey),
                $"{Path.GetFileNameWithoutExtension(localePath)} should not include deprecated key {deprecatedKey}.");
        }
    }

    [Theory]
    [MemberData(nameof(SupplementalLocaleFiles))]
    public void Supplemental_locale_matches_enUs_baseline_keys(string localePath)
    {
        IReadOnlyDictionary<string, string> baseline = LocalizationXamlTestReader.ReadEntries(
            Path.Combine(LocalizationXamlTestReader.FindRepoLocalizationDirectory(), "en_US.xaml"));
        IReadOnlyDictionary<string, string> locale = LocalizationXamlTestReader.ReadEntries(localePath);

        foreach (KeyValuePair<string, string> entry in baseline)
        {
            Assert.True(locale.ContainsKey(entry.Key), $"{Path.GetFileNameWithoutExtension(localePath)} missing key {entry.Key}");
        }
    }

    public static IEnumerable<object[]> SupplementalLocaleFiles()
    {
        string localizationDir = LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        string markerPath = Path.Combine(localizationDir, ".supplemental-locales");
        if (!File.Exists(markerPath))
        {
            yield break;
        }

        var registered = new HashSet<string>(
            File.ReadAllLines(markerPath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        foreach (string locale in registered.OrderBy(name => name, StringComparer.Ordinal))
        {
            yield return new object[] { Path.Combine(localizationDir, locale + ".xaml") };
        }
    }
}
