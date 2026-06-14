using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Playlist.UnitTests.Localization;

public class LocalizationFileIntegrityTests
{
    private static readonly string[] PlaytimeKeys =
    {
        "LOCPlaylist_Playtime_HoursMinutes",
        "LOCPlaylist_Playtime_HoursOnly",
        "LOCPlaylist_Playtime_MinuteUnit",
    };

    private static readonly string[] PlaylistOwnedKeys =
    {
        "LOCPlaylist_DragReorderBlocked_SortActive",
        "LOCPlaylist_DragReorderBlocked_Bucket",
        "LOCPlaylist_LastPlayed_MomentsAgo",
        "LOCPlaylist_LastPlayed_OneMinuteAgo",
        "LOCPlaylist_LastPlayed_MinutesAgo",
        "LOCPlaylist_LastPlayed_OneHourAgo",
        "LOCPlaylist_LastPlayed_HoursAgo",
        "LOCPlaylist_LastPlayed_OneDayAgo",
        "LOCPlaylist_LastPlayed_DaysAgo",
        "LOCPlaylist_LastPlayed_OneWeekAgo",
        "LOCPlaylist_LastPlayed_WeeksAgo",
        "LOCPlaylist_LastPlayed_OneMonthAgo",
        "LOCPlaylist_LastPlayed_MonthsAgo",
        "LOCPlaylist_LastPlayed_OneYearAgo",
        "LOCPlaylist_LastPlayed_LongAgo",
        "LOCPlaylist_Playtime_Minutes",
        "LOCPlaylist_Playtime_HoursMinutes",
        "LOCPlaylist_Playtime_HoursOnly",
        "LOCPlaylist_Playtime_MinuteUnit",
        "LOCPlaylist_HLTB_EmptyTime",
        "LOCPlaylist_HLTB_SortSuffix_Active",
        "LOCPlaylist_HLTB_SortSuffix_Hover",
    };

    [Fact]
    public void EnUs_baseline_contains_playlist_owned_keys()
    {
        IReadOnlyDictionary<string, string> entries = LoadBaseline();
        foreach (string key in PlaylistOwnedKeys)
        {
            Assert.True(entries.ContainsKey(key), $"Missing baseline key: {key}");
        }
    }

    [Theory]
    [MemberData(nameof(AllLocaleFiles))]
    public void Locale_contains_all_baseline_keys(string localePath)
    {
        IReadOnlyDictionary<string, string> baseline = LoadBaseline();
        IReadOnlyDictionary<string, string> locale = LocalizationXamlTestReader.ReadEntries(localePath);

        foreach (KeyValuePair<string, string> entry in baseline)
        {
            Assert.True(locale.ContainsKey(entry.Key), $"{PathFileName(localePath)} missing key {entry.Key}");
            Assert.False(string.IsNullOrEmpty(locale[entry.Key]), $"{PathFileName(localePath)} has empty value for {entry.Key}");
        }
    }

    [Theory]
    [MemberData(nameof(AllLocaleFiles))]
    public void Locale_keys_are_sorted_alphabetically(string localePath)
    {
        IReadOnlyList<string> keys = LocalizationXamlTestReader.ReadKeyOrder(localePath);
        // Match scripts/LocalizationXaml.ps1 (PowerShell Sort-Object default: culture-aware, case-insensitive).
        Assert.Equal(
            keys.OrderBy(key => key, StringComparer.InvariantCultureIgnoreCase).ToArray(),
            keys.ToArray());
    }

    [Theory]
    [MemberData(nameof(AllLocaleFiles))]
    public void Playtime_templates_include_hour_and_minute_placeholders(string localePath)
    {
        IReadOnlyDictionary<string, string> locale = LocalizationXamlTestReader.ReadEntries(localePath);
        string hoursMinutes = locale["LOCPlaylist_Playtime_HoursMinutes"];
        Assert.Contains("{0}", hoursMinutes);
        Assert.Contains("{1}", hoursMinutes);
    }

    [Theory]
    [MemberData(nameof(AllLocaleFiles))]
    public void Playtime_split_templates_reconstruct_sample_display(string localePath)
    {
        IReadOnlyDictionary<string, string> locale = LocalizationXamlTestReader.ReadEntries(localePath);
        string reconstructed = LocalizationXamlTestReader.ReconstructHourPlusDisplay(
            locale["LOCPlaylist_Playtime_HoursMinutes"],
            locale["LOCPlaylist_Playtime_HoursOnly"],
            locale["LOCPlaylist_Playtime_MinuteUnit"],
            hours: 46,
            minutes: 44);
        string expected = string.Format(
            locale["LOCPlaylist_Playtime_HoursMinutes"],
            46,
            44);
        Assert.Equal(expected, reconstructed);
    }

    [Theory]
    [MemberData(nameof(AllLocaleFiles))]
    public void Playtime_three_digit_hours_reconstruct_without_clipping_placeholders(string localePath)
    {
        IReadOnlyDictionary<string, string> locale = LocalizationXamlTestReader.ReadEntries(localePath);
        string reconstructed = LocalizationXamlTestReader.ReconstructHourPlusDisplay(
            locale["LOCPlaylist_Playtime_HoursMinutes"],
            locale["LOCPlaylist_Playtime_HoursOnly"],
            locale["LOCPlaylist_Playtime_MinuteUnit"],
            hours: 168,
            minutes: 59);
        string expected = string.Format(
            locale["LOCPlaylist_Playtime_HoursMinutes"],
            168,
            59);
        Assert.Equal(expected, reconstructed);
    }

    [Fact]
    public void SearchTooltip_baseline_is_multiline()
    {
        IReadOnlyDictionary<string, string> baseline = LoadBaseline();
        Assert.Contains("\n", baseline["LOCPlaylist_SearchTooltip"]);
        Assert.Contains("tag:", baseline["LOCPlaylist_SearchTooltip"]);
    }

    public static IEnumerable<object[]> AllLocaleFiles()
    {
        string localizationDir = LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        foreach (string localePath in LocalizationXamlTestReader.EnumerateLocaleFiles(localizationDir, includeSupplemental: true))
        {
            yield return new object[] { localePath };
        }
    }

    private static IReadOnlyDictionary<string, string> LoadBaseline()
    {
        string localizationDir = LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        return LocalizationXamlTestReader.ReadEntries(Path.Combine(localizationDir, "en_US.xaml"));
    }

    private static string PathFileName(string path)
    {
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }
}
