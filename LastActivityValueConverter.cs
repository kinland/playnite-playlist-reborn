using System;
using System.Globalization;
using System.Windows.Data;
using Playnite.SDK.Models;

namespace Playlist
{
    /// <summary>
    /// WPF converter that renders <see cref="Game.Modified"/> as a relative Last Activity label.
    /// Modified advances on play sessions, installs/uninstalls, and other record changes, so this
    /// behaves like Last Played but also reflects activity such as installing a game.
    /// </summary>
    public class LastActivityValueConverter : IValueConverter
    {
        /// <summary>
        /// Converts a DateTime/DateTimeOffset source value into formatted Last Activity text.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime? lastActivity = ConvertToUtcNullable(value);
            LastPlayedDisplayValue formatted = LastPlayedRelativeFormatter.Format(lastActivity, DateTime.UtcNow);
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
        /// Normalizes a game's Modified timestamp to nullable UTC for formatting and sorting.
        /// </summary>
        internal static DateTime? ExtractModifiedUtc(Game game)
        {
            return ConvertToUtcNullable(game?.Modified);
        }

        /// <summary>
        /// Converts supported date types to nullable UTC DateTime, treating defaults as no activity.
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
