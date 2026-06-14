using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Playlist
{
    internal static class PlaylistLocalizationOverride
    {
        private static ResourceDictionary loadedDictionary;
        private static string activeLocaleId;

        internal static string ActiveLocaleId => activeLocaleId;

        internal static void SetActiveLocale(string localeId)
        {
            localeId = NormalizeLocaleId(localeId);
            if (string.IsNullOrEmpty(localeId))
            {
                activeLocaleId = null;
                loadedDictionary = null;
                return;
            }

            if (string.Equals(localeId, activeLocaleId, StringComparison.OrdinalIgnoreCase)
                && loadedDictionary != null)
            {
                return;
            }

            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string localePath = Path.Combine(pluginDirectory ?? string.Empty, "Localization", localeId + ".xaml");
            if (!File.Exists(localePath))
            {
                throw new FileNotFoundException($"Playlist localization file not found for override locale {localeId}.", localePath);
            }

            loadedDictionary = new ResourceDictionary
            {
                Source = new Uri(localePath, UriKind.Absolute),
            };
            activeLocaleId = localeId;
        }

        internal static bool TryGetString(string resourceKey, out string value)
        {
            if (loadedDictionary != null
                && loadedDictionary.Contains(resourceKey)
                && loadedDictionary[resourceKey] is string resolved
                && !string.IsNullOrWhiteSpace(resolved))
            {
                value = resolved;
                return true;
            }

            value = null;
            return false;
        }

        internal static void MergeInto(FrameworkElement element)
        {
            if (element == null || loadedDictionary == null)
            {
                return;
            }

            if (element.Resources == null)
            {
                element.Resources = new ResourceDictionary();
            }

            if (!element.Resources.MergedDictionaries.Contains(loadedDictionary))
            {
                element.Resources.MergedDictionaries.Add(loadedDictionary);
            }
        }

        internal static void ApplyFromSettings(PlaylistSettings settings)
        {
            SetActiveLocale(settings?.LanguageOverrideLocaleId);
        }

        private static string NormalizeLocaleId(string localeId)
        {
            if (string.IsNullOrWhiteSpace(localeId))
            {
                return null;
            }

            return localeId.Trim().Replace('-', '_');
        }
    }
}
