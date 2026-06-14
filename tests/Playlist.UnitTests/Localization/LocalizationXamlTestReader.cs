using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Playlist.UnitTests.Localization;

internal static class LocalizationXamlTestReader
{
    private static readonly Regex EntryPattern = new Regex(
        @"(?s)[ \t]*<sys:String\s+x:Key=""([^""]+)""([^>]*)>(.*?)</sys:String>",
        RegexOptions.Compiled);

    internal static IReadOnlyDictionary<string, string> ReadEntries(string filePath)
    {
        string content = File.ReadAllText(filePath);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in EntryPattern.Matches(content))
        {
            string key = match.Groups[1].Value;
            if (entries.ContainsKey(key))
            {
                throw new InvalidOperationException($"Duplicate localization key '{key}' in {filePath}");
            }

            entries[key] = match.Groups[3].Value;
        }

        return entries;
    }

    internal static IReadOnlyList<string> ReadKeyOrder(string filePath)
    {
        string content = File.ReadAllText(filePath);
        return EntryPattern.Matches(content)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    internal static string ResolveHourPlusUnitSeparator(
        string hoursMinutes,
        string hoursOnly,
        string minuteUnit)
    {
        const string hourPlaceholder = "8";
        const string minutePlaceholder = "9";
        string combined = string.Format(hoursMinutes, hourPlaceholder, minutePlaceholder);
        string hourPart = string.Format(hoursOnly, hourPlaceholder);
        string minutePart = string.Format(minuteUnit, minutePlaceholder);

        if (string.IsNullOrEmpty(combined)
            || !combined.StartsWith(hourPart, StringComparison.Ordinal)
            || !combined.EndsWith(minutePart, StringComparison.Ordinal))
        {
            return " ";
        }

        int separatorLength = combined.Length - hourPart.Length - minutePart.Length;
        return separatorLength <= 0
            ? string.Empty
            : combined.Substring(hourPart.Length, separatorLength);
    }

    internal static string ReconstructHourPlusDisplay(
        string hoursMinutes,
        string hoursOnly,
        string minuteUnit,
        long hours,
        long minutes)
    {
        string hourPart = string.Format(hoursOnly, hours);
        string minutePart = string.Format(minuteUnit, minutes);
        string separator = ResolveHourPlusUnitSeparator(hoursMinutes, hoursOnly, minuteUnit);
        return hourPart + separator + minutePart;
    }

    internal static string FindRepoLocalizationDirectory()
    {
        string dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "Localization"));
        if (!Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException($"Localization directory not found: {dir}");
        }

        return dir;
    }

    internal static IEnumerable<string> EnumerateLocaleFiles(string localizationDir, bool includeSupplemental)
    {
        var files = Directory.GetFiles(localizationDir, "*.xaml")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.Equals(name, "en_US", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (!includeSupplemental)
        {
            string supplementalMarker = Path.Combine(localizationDir, ".supplemental-locales");
            if (File.Exists(supplementalMarker))
            {
                var supplemental = new HashSet<string>(
                    File.ReadAllLines(supplementalMarker)
                        .Select(line => line.Trim())
                        .Where(line => line.Length > 0),
                    StringComparer.OrdinalIgnoreCase);
                files = files.Where(locale => !supplemental.Contains(locale)).ToList();
            }
        }

        return files.Select(locale => Path.Combine(localizationDir, locale + ".xaml"));
    }
}
