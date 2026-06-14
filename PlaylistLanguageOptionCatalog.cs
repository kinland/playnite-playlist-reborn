using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Playlist
{
    internal static class PlaylistLanguageOptionCatalog
    {
        internal static IReadOnlyList<PlaylistLanguageOption> BuildOptions(
            string playniteLanguage,
            CultureInfo osUiCulture)
        {
            playniteLanguage = NormalizeLocaleId(playniteLanguage) ?? "en_US";
            osUiCulture = osUiCulture ?? CultureInfo.CurrentUICulture;

            var options = new List<PlaylistLanguageOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string playniteDisplayName = GetLocaleDisplayName(playniteLanguage);
            options.Add(new PlaylistLanguageOption(
                string.Empty,
                playniteDisplayName,
                PlaylistLanguageOptionKind.Playnite));
            seen.Add(playniteLanguage);

            if (TryGetOsLocaleOption(playniteLanguage, osUiCulture, seen, out PlaylistLanguageOption osOption))
            {
                options.Add(osOption);
            }

            foreach (string supplementalLocaleId in PlaylistLocaleCultureMap.GetSupplementalLocaleIds()
                .OrderBy(GetLocaleDisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!seen.Add(supplementalLocaleId))
                {
                    continue;
                }

                options.Add(new PlaylistLanguageOption(
                    supplementalLocaleId,
                    GetLocaleDisplayName(supplementalLocaleId),
                    PlaylistLanguageOptionKind.Supplemental));
            }

            return options;
        }

        internal static bool ShouldOfferOsLocaleMismatchPrompt(
            bool hasPrompted,
            string playniteLanguage,
            CultureInfo osUiCulture,
            out string osLocaleId)
        {
            osLocaleId = null;
            if (hasPrompted)
            {
                return false;
            }

            playniteLanguage = NormalizeLocaleId(playniteLanguage) ?? "en_US";
            osUiCulture = osUiCulture ?? CultureInfo.CurrentUICulture;
            if (!PlaylistLocaleCultureMap.TryResolveLocaleFromCulture(osUiCulture, out osLocaleId))
            {
                return false;
            }

            return !string.Equals(osLocaleId, playniteLanguage, StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetLocaleDisplayName(string localeId)
        {
            localeId = NormalizeLocaleId(localeId);
            if (string.IsNullOrEmpty(localeId))
            {
                return string.Empty;
            }

            PlaylistLocaleCultureMap.LocaleEntry entry = PlaylistLocaleCultureMap.TryGetLocaleEntry(localeId);
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Autonym))
            {
                return entry.Autonym;
            }

            try
            {
                return CultureInfo.GetCultureInfo(ToCultureName(localeId)).NativeName;
            }
            catch (CultureNotFoundException)
            {
                return localeId;
            }
        }

        internal static string FormatOsLocaleMismatchPrompt(
            string playniteLanguage,
            string osLocaleId,
            CultureInfo osUiCulture)
        {
            PlaylistLocaleCultureMap.OsLocaleMismatchPrompt prompt = PlaylistLocaleCultureMap.GetOsLocaleMismatchPrompt();
            string template = PlaylistLocalization.GetString(prompt.MessageKey);
            if (string.IsNullOrWhiteSpace(template)
                || string.Equals(template, prompt.MessageKey, StringComparison.Ordinal)
                || template.StartsWith("<!", StringComparison.Ordinal))
            {
                template = prompt.FallbackMessage;
            }

            string osLanguageLabel = GetOsLanguageLabel(osUiCulture, osLocaleId);
            string playlistLanguageLabel = GetLocaleDisplayName(osLocaleId);
            return string.Format(template, osLanguageLabel, playlistLanguageLabel);
        }

        private static bool TryGetOsLocaleOption(
            string playniteLanguage,
            CultureInfo osUiCulture,
            ISet<string> seen,
            out PlaylistLanguageOption option)
        {
            option = null;
            if (!PlaylistLocaleCultureMap.TryResolveLocaleFromCulture(osUiCulture, out string osLocaleId))
            {
                return false;
            }

            if (string.Equals(osLocaleId, playniteLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!seen.Add(osLocaleId))
            {
                return false;
            }

            option = new PlaylistLanguageOption(
                osLocaleId,
                GetLocaleDisplayName(osLocaleId),
                PlaylistLanguageOptionKind.Os);
            return true;
        }

        private static string GetOsLanguageLabel(CultureInfo osUiCulture, string osLocaleId)
        {
            if (osUiCulture != null && !string.IsNullOrWhiteSpace(osUiCulture.DisplayName))
            {
                return osUiCulture.DisplayName;
            }

            return GetLocaleDisplayName(osLocaleId);
        }

        private static string NormalizeLocaleId(string localeId)
        {
            if (string.IsNullOrWhiteSpace(localeId))
            {
                return null;
            }

            return localeId.Trim().Replace('-', '_');
        }

        private static string ToCultureName(string localeId)
        {
            return localeId.Replace('_', '-');
        }
    }
}
