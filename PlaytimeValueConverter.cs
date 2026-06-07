using Playnite.SDK;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Playlist
{
    /// <summary>
    /// WPF converter that renders Game.Playtime using Playnite's theme formatter,
    /// leaving unplayed games blank in the Time Played column.
    /// </summary>
    public class PlaytimeValueConverter : IValueConverter
    {
        /// <summary>
        /// Converts playtime seconds into formatted text, or blank when unplayed.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ulong seconds = 0;
            if (value is ulong playtime)
            {
                seconds = playtime;
            }
            else if (value is long longPlaytime && longPlaytime > 0)
            {
                seconds = (ulong)longPlaytime;
            }

            if (seconds == 0)
            {
                return string.Empty;
            }

            IValueConverter themeConverter = ResolvePlayTimeConverter();
            if (themeConverter == null)
            {
                return HltbPlaytimeFormat.FormatSeconds((long)seconds, integrationViewItemOnlyHour: false, themeScope: null);
            }

            try
            {
                object formatted = themeConverter.Convert(seconds, typeof(string), false, culture);
                return formatted as string ?? string.Empty;
            }
            catch
            {
                return HltbPlaytimeFormat.FormatSeconds((long)seconds, integrationViewItemOnlyHour: false, themeScope: null);
            }
        }

        /// <summary>
        /// Reverse conversion is not supported for display-only labels.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static IValueConverter ResolvePlayTimeConverter()
        {
            object fromApp = Application.Current?.TryFindResource("PlayTimeToStringConverter");
            if (fromApp is IValueConverter converter)
            {
                return converter;
            }

            object fromProvider = ResourceProvider.GetResource("PlayTimeToStringConverter");
            return fromProvider as IValueConverter;
        }
    }
}
