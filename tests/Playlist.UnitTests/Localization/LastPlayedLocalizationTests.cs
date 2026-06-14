using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace Playlist.UnitTests.Localization;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class LastPlayedLocalizationTests : IDisposable
{
    private readonly Func<string, string> previousGetter;

    public LastPlayedLocalizationTests()
    {
        previousGetter = PlaylistLocalization.TestGetString;
    }

    public void Dispose()
    {
        PlaylistLocalization.TestGetString = previousGetter;
    }

    [Theory]
    [InlineData("ja_JP", 60, "1分前")]
    [InlineData("ja_JP", 120, "2分前")]
    [InlineData("de_DE", 60, "vor 1 Minute")]
    [InlineData("de_DE", 86400, "vor 1 Tag")]
    [InlineData("cy_GB", 120, "2 munud yn ôl")]
    [InlineData("fr_FR", 120, "il y a 2 minutes")]
    public void Format_uses_locale_relative_labels(string localeFile, int secondsAgo, string expectedLabel)
    {
        InstallLocale(localeFile);
        DateTime now = new DateTime(2026, 06, 06, 12, 0, 0, DateTimeKind.Utc);
        DateTime played = now.AddSeconds(-secondsAgo);
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(played, now);
        Assert.Equal(expectedLabel, value.Label);
    }

    [Theory]
    [InlineData("ja_JP", "たった今")]
    [InlineData("de_DE", "Gerade eben")]
    [InlineData("cy_GB", "Eiliad yn ôl")]
    public void Format_momentsAgo_uses_locale_label(string localeFile, string expectedLabel)
    {
        InstallLocale(localeFile);
        DateTime now = new DateTime(2026, 06, 06, 12, 0, 0, DateTimeKind.Utc);
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(now.AddSeconds(-30), now);
        Assert.Equal(expectedLabel, value.Label);
    }

    private static void InstallLocale(string localeFile)
    {
        string localizationDir = LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        IReadOnlyDictionary<string, string> entries = LocalizationXamlTestReader.ReadEntries(
            System.IO.Path.Combine(localizationDir, localeFile + ".xaml"));
        PlaylistLocalization.TestGetString = key =>
            entries.TryGetValue(key, out string value) ? value : key;
    }
}
