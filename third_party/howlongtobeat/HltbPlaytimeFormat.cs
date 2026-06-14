using Playnite.SDK;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Playlist
{
    /// <summary>
    /// Matches HowLongToBeat's use of Playnite playtime strings: default format uses theme
    /// <c>PlayTimeToStringConverter</c>; "only hour" follows the plugin's IntegrationViewItemOnlyHour path
    /// (rounded to whole hours, then formatted like other Playnite times).
    /// </summary>
    internal static class HltbPlaytimeFormat
    {
        public static string FormatSeconds(long seconds, bool integrationViewItemOnlyHour, FrameworkElement themeScope)
        {
            if (seconds <= 0)
            {
                return PlaylistLocalization.GetString("LOCPlaylist_HLTB_EmptyTime");
            }

            var conv = ResolvePlayTimeConverter(themeScope);
            if (conv == null)
            {
                return FallbackFormat(seconds, integrationViewItemOnlyHour);
            }

            try
            {
                if (integrationViewItemOnlyHour)
                {
                    double hours = seconds / 3600.0;
                    long rounded = (long)Math.Round(hours, MidpointRounding.AwayFromZero);
                    if (rounded <= 0)
                    {
                        rounded = 1;
                    }

                    // Match HLTB "only hour" behavior: never show minutes (e.g. 20h, not 20h 0m).
                    return PlaylistLocalization.Format(
                        "LOCPlaylist_Playtime_HoursOnly",
                        rounded.ToString(CultureInfo.CurrentCulture));
                }

                object full = conv.Convert((ulong)seconds, typeof(string), false, CultureInfo.CurrentCulture);
                return full as string ?? FallbackFormat(seconds, false);
            }
            catch
            {
                return FallbackFormat(seconds, integrationViewItemOnlyHour);
            }
        }

        private static IValueConverter ResolvePlayTimeConverter(FrameworkElement themeScope)
        {
            object fromScope = themeScope?.TryFindResource("PlayTimeToStringConverter");
            if (fromScope is IValueConverter c1)
            {
                return c1;
            }

            object fromApp = Application.Current?.TryFindResource("PlayTimeToStringConverter");
            if (fromApp is IValueConverter c2)
            {
                return c2;
            }

            object fromProvider = ResourceProvider.GetResource("PlayTimeToStringConverter");
            return fromProvider as IValueConverter;
        }

        private static string FallbackFormat(long seconds, bool onlyHour)
        {
            if (onlyHour)
            {
                long rounded = (long)Math.Round(seconds / 3600.0, MidpointRounding.AwayFromZero);
                if (rounded <= 0)
                {
                    rounded = 1;
                }

                return PlaylistLocalization.Format(
                    "LOCPlaylist_Playtime_HoursOnly",
                    rounded.ToString(CultureInfo.CurrentCulture));
            }

            long totalMinutes = seconds / 60;
            long h = totalMinutes / 60;
            long m = totalMinutes % 60;
            return PlaylistLocalization.Format(
                "LOCPlaylist_Playtime_HoursMinutes",
                h.ToString(CultureInfo.CurrentCulture),
                m.ToString(CultureInfo.CurrentCulture));
        }
    }
}
