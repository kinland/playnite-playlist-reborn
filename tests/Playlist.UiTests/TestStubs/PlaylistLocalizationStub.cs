using System;

namespace Playlist
{
    /// <summary>
    /// UiTests stub for production <see cref="PlaylistLocalization"/>.
    /// </summary>
    internal static class PlaylistLocalization
    {
        internal static Func<string, string> TestGetString { get; set; }

        internal static string GetString(string resourceKey)
        {
            if (TestGetString != null)
            {
                return TestGetString(resourceKey);
            }

            return resourceKey;
        }

        internal static string Format(string resourceKey, params object[] args)
        {
            return string.Format(GetString(resourceKey), args);
        }
    }
}
