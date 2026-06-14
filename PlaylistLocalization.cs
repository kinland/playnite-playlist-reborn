using Playnite.SDK;
using System;

namespace Playlist
{
    /// <summary>
    /// Playlist-owned localization keys (not provided by the HLTB plugin or Playnite core).
    /// </summary>
    internal static class PlaylistLocalization
    {
        internal const string PlaylistFeatureNameKey = "LOCPlaylist_Playlist";

        /// <summary>Test seam for substituting localized strings in unit tests.</summary>
        internal static Func<string, string> TestGetString { get; set; }

        /// <summary>Test seam for the Playnite ResourceProvider path when no supplemental override is active.</summary>
        internal static IResourceProvider TestResourceProvider { get; set; }

        internal static string GetPlaylistFeatureName() => GetString(PlaylistFeatureNameKey);

        /// <summary>Settings label and Extensions language submenu title ({0} = localized feature name).</summary>
        internal static string GetLanguageOverrideLabel() =>
            Format("LOCPlaylist_Settings_LanguageOverride", GetPlaylistFeatureName());

        /// <summary>Sync-search toggle label ({0} = localized feature name).</summary>
        internal static string GetSyncSearchWithMainPanelLabel() =>
            Format("LOCPlaylist_Settings_SyncSearchWithMainPanel", GetPlaylistFeatureName());

        internal static string GetString(string resourceKey)
        {
            if (TestGetString != null)
            {
                return TestGetString(resourceKey);
            }

            if (PlaylistLocalizationOverride.TryGetString(resourceKey, out string overrideValue))
            {
                return overrideValue;
            }

            if (TestResourceProvider != null)
            {
                return TestResourceProvider.GetString(resourceKey);
            }

            return ResourceProvider.GetString(resourceKey);
        }

        internal static string Format(string resourceKey, params object[] args)
        {
            return string.Format(GetString(resourceKey), args);
        }
    }
}
