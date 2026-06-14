using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Playlist
{
    internal static class PlaylistLocalizationOverride
    {
        private static ResourceDictionary loadedDictionary;
        private static string activeLocaleId;
        private static readonly ConditionalWeakTable<FrameworkElement, ResourceDictionary> MergedOverrides =
            new ConditionalWeakTable<FrameworkElement, ResourceDictionary>();

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
            if (element == null)
            {
                return;
            }

            if (element.Resources == null)
            {
                element.Resources = new ResourceDictionary();
            }

            if (MergedOverrides.TryGetValue(element, out ResourceDictionary previous))
            {
                element.Resources.MergedDictionaries.Remove(previous);
                MergedOverrides.Remove(element);
            }

            if (loadedDictionary == null)
            {
                return;
            }

            element.Resources.MergedDictionaries.Insert(0, loadedDictionary);
            MergedOverrides.Add(element, loadedDictionary);
        }

        internal static void ApplyFromSettings(PlaylistSettings settings)
        {
            SetActiveLocale(settings?.LanguageOverrideLocaleId);
        }

        /// <summary>Test seam: load a locale dictionary from an embedded resource stream.</summary>
        internal static void SetActiveLocaleFromStream(string localeId, System.IO.Stream localeXamlStream)
        {
            localeId = NormalizeLocaleId(localeId);
            if (string.IsNullOrEmpty(localeId))
            {
                activeLocaleId = null;
                loadedDictionary = null;
                return;
            }

            if (localeXamlStream == null)
            {
                throw new ArgumentNullException(nameof(localeXamlStream));
            }

            loadedDictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Load(localeXamlStream);
            activeLocaleId = localeId;
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
