using Playnite.SDK;
using System;
using System.Globalization;

namespace Playlist
{
    /// <summary>
    /// Prefers HowLongToBeat plugin localization when it is present and translated for the active UI culture;
    /// otherwise falls back to Playlist-owned resource keys, then the HLTB English baseline.
    /// </summary>
    internal static class HltbLocalizedStringResolver
    {
        /// <summary>
        /// Test seam for substituting <see cref="ResourceProvider"/> in unit tests.
        /// </summary>
        internal static IResourceProvider TestResourceProvider { get; set; }

        internal static string Resolve(string hltbResourceKey, string playlistResourceKey, string hltbEnglishBaseline)
        {
            if (!string.IsNullOrEmpty(playlistResourceKey)
                && !string.IsNullOrEmpty(PlaylistLocalizationOverride.ActiveLocaleId)
                && PlaylistLocalizationOverride.TryGetString(playlistResourceKey, out string overrideValue))
            {
                return overrideValue;
            }

            string hltbValue = GetString(hltbResourceKey);
            if (ShouldPreferHltbValue(hltbValue, hltbResourceKey, hltbEnglishBaseline))
            {
                return hltbValue;
            }

            if (!string.IsNullOrEmpty(playlistResourceKey))
            {
                string playlistValue = GetString(playlistResourceKey);
                if (TryUseResolvedValue(playlistValue, playlistResourceKey))
                {
                    return playlistValue;
                }
            }

            if (TryUseResolvedValue(hltbValue, hltbResourceKey))
            {
                return hltbValue;
            }

            return hltbEnglishBaseline;
        }

        private static string GetString(string resourceKey)
        {
            if (TestResourceProvider != null)
            {
                return TestResourceProvider.GetString(resourceKey);
            }

            return PlaylistLocalization.GetString(resourceKey);
        }

        internal static bool ShouldPreferHltbValue(string hltbValue, string hltbResourceKey, string hltbEnglishBaseline)
        {
            if (!TryUseResolvedValue(hltbValue, hltbResourceKey))
            {
                return false;
            }

            if (IsEnglishUiCulture())
            {
                return true;
            }

            if (string.IsNullOrEmpty(hltbEnglishBaseline))
            {
                return true;
            }

            return !string.Equals(hltbValue, hltbEnglishBaseline, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryUseResolvedValue(string value, string resourceKey)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !IsUnresolvedResource(value, resourceKey);
        }

        private static bool IsUnresolvedResource(string value, string resourceKey)
        {
            if (string.IsNullOrEmpty(resourceKey))
            {
                return string.IsNullOrWhiteSpace(value);
            }

            return string.Equals(value, resourceKey, StringComparison.Ordinal)
                || string.Equals(value, "<!" + resourceKey + "!>", StringComparison.Ordinal);
        }

        private static bool IsEnglishUiCulture()
        {
            return string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "en",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
