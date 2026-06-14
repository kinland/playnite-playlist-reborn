using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests.Localization;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class DragReorderLocalizationTests : IDisposable
{
    private readonly Func<string, string> previousGetter;

    public DragReorderLocalizationTests()
    {
        previousGetter = PlaylistDragReorderMessages.TestGetString;
    }

    public void Dispose()
    {
        PlaylistDragReorderMessages.TestGetString = previousGetter;
    }

    [Theory]
    [InlineData("fr_FR", "LastPlayed", "Dernière partie", "Rang (#)")]
    [InlineData("de_DE", "LastPlayed", "Zuletzt gespielt", "Rang (#)")]
    [InlineData("cy_GB", "LastPlayed", "Chwaraewyd Diwethaf", "Safle (#)")]
    public void BuildSortBlockedMessage_uses_locale_column_labels(
        string localeFile,
        string sortColumnKey,
        string expectedActiveColumn,
        string expectedRankColumn)
    {
        InstallLocale(localeFile);
        string message = PlaylistDragReorderMessages.BuildSortBlockedMessage(sortColumnKey);
        Assert.Contains(expectedActiveColumn, message);
        Assert.Contains(expectedRankColumn, message);
    }

    [Theory]
    [InlineData("ja_JP", "3日前")]
    [InlineData("es_ES", "hace 3 días")]
    public void BuildBucketBlockedMessage_uses_locale_bucket_template(string localeFile, string bucketLabel)
    {
        InstallLocale(localeFile);
        string message = PlaylistDragReorderMessages.BuildBucketBlockedMessage(bucketLabel);
        Assert.Contains(bucketLabel, message);
    }

    private static void InstallLocale(string localeFile)
    {
        string localizationDir = LocalizationXamlTestReader.FindRepoLocalizationDirectory();
        IReadOnlyDictionary<string, string> entries = LocalizationXamlTestReader.ReadEntries(
            System.IO.Path.Combine(localizationDir, localeFile + ".xaml"));
        PlaylistDragReorderMessages.TestGetString = key =>
            entries.TryGetValue(key, out string value) ? value : key;
    }
}
