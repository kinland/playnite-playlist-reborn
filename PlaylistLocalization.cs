using Playnite.SDK;
using System;

namespace Playlist
{
    /// <summary>
    /// Playlist-owned localization keys (not provided by the HLTB plugin or Playnite core).
    /// </summary>
    internal static class PlaylistLocalization
    {
        /// <summary>Test seam for substituting localized strings in unit tests.</summary>
        internal static Func<string, string> TestGetString { get; set; }

        internal static string GetString(string resourceKey)
        {
            if (TestGetString != null)
            {
                return TestGetString(resourceKey);
            }

            return ResourceProvider.GetString(resourceKey);
        }

        internal static string Format(string resourceKey, params object[] args)
        {
            return string.Format(GetString(resourceKey), args);
        }
    }
}
