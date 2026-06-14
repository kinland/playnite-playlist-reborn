using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Playlist
{
    /// <summary>
    /// Maps OS UI cultures to Playlist <c>Localization/*.xaml</c> locale file names.
    /// Data source: <c>playlist-os-locale-culture-map.json</c> (OS prompt + multi-pattern mappings).
    /// </summary>
    internal static class PlaylistLocaleCultureMap
    {
        private const string MapFileName = "playlist-os-locale-culture-map.json";

        private static readonly Lazy<CultureMapData> LoadedMap = new Lazy<CultureMapData>(LoadMap);

        internal static bool TryResolveLocaleFromCulture(CultureInfo culture, out string localeId)
        {
            if (culture == null)
            {
                localeId = null;
                return false;
            }

            IReadOnlyDictionary<string, string> cultureNameToLocale = LoadedMap.Value.CultureNameToLocale;
            for (CultureInfo current = culture; current != null; current = current.Parent)
            {
                if (current.Equals(CultureInfo.InvariantCulture))
                {
                    break;
                }

                if (TryMapCultureName(cultureNameToLocale, current.Name, out localeId))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(current.TwoLetterISOLanguageName)
                    && TryMapCultureName(cultureNameToLocale, current.TwoLetterISOLanguageName, out localeId))
                {
                    return true;
                }
            }

            localeId = null;
            return false;
        }

        internal static bool IsSupplementalLocale(string localeId)
        {
            return !string.IsNullOrEmpty(localeId)
                && LoadedMap.Value.SupplementalLocaleIds.Contains(localeId);
        }

        internal static IReadOnlyCollection<string> GetSupplementalLocaleIds()
        {
            return LoadedMap.Value.SupplementalLocaleIds;
        }

        internal static IReadOnlyDictionary<string, string> GetCultureNameMappings()
        {
            return LoadedMap.Value.CultureNameToLocale;
        }

        internal static LocaleEntry TryGetLocaleEntry(string localeId)
        {
            if (string.IsNullOrEmpty(localeId))
            {
                return null;
            }

            LoadedMap.Value.LocaleEntries.TryGetValue(localeId, out LocaleEntry entry);
            return entry;
        }

        internal static OsLocaleMismatchPrompt GetOsLocaleMismatchPrompt()
        {
            return LoadedMap.Value.OsLocaleMismatchPrompt;
        }

        internal static string GetMapFilePath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                string basePath = Path.Combine(baseDirectory, MapFileName);
                if (File.Exists(basePath))
                {
                    return basePath;
                }
            }

            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(assemblyDirectory ?? string.Empty, MapFileName);
        }

        private static bool TryMapCultureName(
            IReadOnlyDictionary<string, string> cultureNameToLocale,
            string cultureName,
            out string localeId)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                localeId = null;
                return false;
            }

            return cultureNameToLocale.TryGetValue(cultureName.Trim(), out localeId);
        }

        private static CultureMapData LoadMap()
        {
            string mapPath = GetMapFilePath();
            if (!File.Exists(mapPath))
            {
                throw new FileNotFoundException($"Playlist OS locale culture map not found: {mapPath}");
            }

            string json = File.ReadAllText(mapPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            var cultureNameToLocale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var localeEntries = new Dictionary<string, LocaleEntry>(StringComparer.OrdinalIgnoreCase);
            var supplementalLocaleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (JsonProperty localeProperty in root.GetProperty("locales").EnumerateObject())
            {
                string localeId = localeProperty.Name;
                JsonElement localeNode = localeProperty.Value;
                string category = localeNode.GetProperty("category").GetString();
                var patterns = localeNode.GetProperty("osCulturePatterns")
                    .EnumerateArray()
                    .Select(item => item.GetString())
                    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .ToArray();

                var entry = new LocaleEntry
                {
                    LocaleId = localeId,
                    EnglishName = localeNode.GetProperty("englishName").GetString(),
                    Autonym = localeNode.GetProperty("autonym").GetString(),
                    Category = category,
                    WindowsDisplayLanguage = localeNode.GetProperty("windowsDisplayLanguage").GetBoolean(),
                    OsCulturePatterns = patterns,
                };
                localeEntries[localeId] = entry;

                if (string.Equals(category, "supplemental", StringComparison.OrdinalIgnoreCase))
                {
                    supplementalLocaleIds.Add(localeId);
                }

                foreach (string pattern in patterns)
                {
                    cultureNameToLocale[pattern] = localeId;
                }
            }

            JsonElement promptNode = root.GetProperty("osLocaleMismatchPrompt");
            var prompt = new OsLocaleMismatchPrompt
            {
                MessageKey = promptNode.GetProperty("messageKey").GetString(),
                FallbackMessage = promptNode.GetProperty("fallbackMessage").GetString(),
            };

            return new CultureMapData
            {
                CultureNameToLocale = cultureNameToLocale,
                LocaleEntries = localeEntries,
                SupplementalLocaleIds = supplementalLocaleIds,
                OsLocaleMismatchPrompt = prompt,
            };
        }

        internal sealed class LocaleEntry
        {
            internal string LocaleId { get; set; }
            internal string EnglishName { get; set; }
            internal string Autonym { get; set; }
            internal string Category { get; set; }
            internal bool WindowsDisplayLanguage { get; set; }
            internal string[] OsCulturePatterns { get; set; }
        }

        internal sealed class OsLocaleMismatchPrompt
        {
            internal string MessageKey { get; set; }
            internal string FallbackMessage { get; set; }
        }

        private sealed class CultureMapData
        {
            internal Dictionary<string, string> CultureNameToLocale { get; set; }
            internal Dictionary<string, LocaleEntry> LocaleEntries { get; set; }
            internal HashSet<string> SupplementalLocaleIds { get; set; }
            internal OsLocaleMismatchPrompt OsLocaleMismatchPrompt { get; set; }
        }
    }
}
