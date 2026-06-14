using System;
using System.Globalization;
using System.Windows.Data;
using Playnite.SDK.Models;

namespace Playlist
{
    /// <summary>
    /// WPF converter that renders Game.LastActivity as a relative Last Played label.
    /// </summary>
    public class LastPlayedValueConverter : IValueConverter
    {
        /// <summary>
        /// Converts DateTime/DateTimeOffset source values into formatted Last Played text.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime? lastPlayed = ConvertToUtcNullable(value);
            LastPlayedDisplayValue formatted = LastPlayedRelativeFormatter.Format(lastPlayed, DateTime.UtcNow);
            return formatted.Label;
        }

        /// <summary>
        /// Reverse conversion is not supported for display-only labels.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Normalizes a game's LastActivity timestamp to nullable UTC for formatting and sorting.
        /// </summary>
        internal static DateTime? ExtractLastActivityUtc(Game game)
        {
            return ConvertToUtcNullable(game?.LastActivity);
        }

        /// <summary>
        /// Converts supported date types to nullable UTC DateTime, treating defaults as unplayed.
        /// </summary>
        private static DateTime? ConvertToUtcNullable(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is DateTime dt)
            {
                if (dt == default)
                {
                    return null;
                }

                if (dt.Kind == DateTimeKind.Utc)
                {
                    return dt;
                }

                if (dt.Kind == DateTimeKind.Local)
                {
                    return dt.ToUniversalTime();
                }

                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }

            if (value is DateTimeOffset dto)
            {
                if (dto == default)
                {
                    return null;
                }

                return dto.UtcDateTime;
            }

            return null;
        }
    }
}
