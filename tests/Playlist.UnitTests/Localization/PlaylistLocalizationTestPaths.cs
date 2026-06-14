using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Moq;
using Playnite.SDK;
using Xunit;

namespace Playlist.UnitTests.Localization;

/// <summary>
/// Exercises supplemental override (haw_US) vs native Playnite locale (de_DE via ResourceProvider).
/// </summary>
internal static class PlaylistLocalizationTestPaths
{
    internal const string SupplementalLocaleId = "haw_US";
    internal const string NativePlayniteLocaleId = "de_DE";

    internal static IReadOnlyDictionary<string, string> LoadEmbeddedSupplementalEntries()
    {
        using Stream stream = OpenEmbeddedLocaleStream(SupplementalLocaleId);
        return LocalizationXamlTestReader.ReadEntriesFromStream(stream, SupplementalLocaleId + ".xaml");
    }

    internal static void RunWithSupplementalOverride(Action<IReadOnlyDictionary<string, string>> action)
    {
        IReadOnlyDictionary<string, string> entries = LoadEmbeddedSupplementalEntries();
        using Stream stream = OpenEmbeddedLocaleStream(SupplementalLocaleId);
        PlaylistLocalizationOverride.SetActiveLocaleFromStream(SupplementalLocaleId, stream);
        try
        {
            action(entries);
        }
        finally
        {
            PlaylistLocalizationOverride.SetActiveLocale(null);
        }
    }

    internal static void RunWithNativePlayniteProvider(
        IReadOnlyDictionary<string, string> resourceStrings,
        Action action)
    {
        var resourceProvider = new Mock<IResourceProvider>();
        foreach (KeyValuePair<string, string> entry in resourceStrings)
        {
            resourceProvider.Setup(provider => provider.GetString(entry.Key)).Returns(entry.Value);
        }

        IResourceProvider previousProvider = PlaylistLocalization.TestResourceProvider;
        try
        {
            PlaylistLocalizationOverride.SetActiveLocale(null);
            PlaylistLocalization.TestGetString = null;
            PlaylistLocalization.TestResourceProvider = resourceProvider.Object;
            action();
        }
        finally
        {
            PlaylistLocalization.TestResourceProvider = previousProvider;
        }
    }

    internal static IReadOnlyDictionary<string, string> NativePlayniteGermanStrings()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LOCTimePlayed"] = "Gespielte Zeit",
            ["LOCCompletionStatus"] = "Fertigstellungsstatus",
            ["LOCPlaylist_Column_Rank"] = "Rang (#)",
            ["LOCPlaylist_LastPlayedColumn"] = "Zuletzt gespielt",
            ["LOCPlaylist_Menu_Columns"] = "Spaltensichtbarkeit",
        };
    }

    private static Stream OpenEmbeddedLocaleStream(string localeId)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(localeId + ".xaml");
        Assert.NotNull(stream);
        return stream;
    }
}
