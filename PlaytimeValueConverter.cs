using Playnite.SDK;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Playlist
{
    /// <summary>
    /// Playtime formatting via Playnite's theme, plus grid cell parts for the Time Played column.
    /// ConverterParameter: <c>SubHourDigit</c>, <c>SubHourSuffix</c>, <c>Hours</c>, <c>Minutes</c>,
    /// <c>UnitSeparator</c>, or <c>Visible</c>, <c>SubHour</c>, <c>HourPlus</c> for layout visibility.
    /// </summary>
    public class PlaytimeValueConverter : IValueConverter
    {
        internal const ulong SecondsPerHour = 3600;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string part = parameter as string;
            if (!TryParseSeconds(value, out ulong seconds))
            {
                return IsVisibilityPart(part) ? (object)Visibility.Collapsed : string.Empty;
            }

            if (IsVisibilityPart(part))
            {
                return ResolveVisibility(part, seconds);
            }

            if (seconds == 0)
            {
                return string.Empty;
            }

            return ResolveTextPart(part, seconds, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        internal static bool TryParseSeconds(object value, out ulong seconds)
        {
            seconds = 0;
            if (value is ulong playtime)
            {
                seconds = playtime;
                return true;
            }

            if (value is long longPlaytime && longPlaytime > 0)
            {
                seconds = (ulong)longPlaytime;
                return true;
            }

            return false;
        }

        internal static bool TryParseSubHourThemeDisplay(string formatted, out string digits, out string suffix)
        {
            digits = null;
            suffix = null;
            if (string.IsNullOrWhiteSpace(formatted))
            {
                return false;
            }

            int index = 0;
            if (!TryReadDigitRun(formatted, ref index, out digits))
            {
                return false;
            }

            int trailingDigitCheck = index;
            if (TryReadDigitRun(formatted, ref trailingDigitCheck, out _))
            {
                return false;
            }

            suffix = formatted.Substring(index);
            if (string.IsNullOrEmpty(suffix))
            {
                return false;
            }

            return IsMinuteSuffix(suffix.TrimStart());
        }

        /// <summary>
        /// Splits English-style theme playtime strings (e.g. <c>46h 44m</c>) into hour and minute display units.
        /// Non-English theme output falls through to localized <see cref="TryGetHourPlusUnits"/>.
        /// </summary>
        internal static bool TryParseThemeHourMinuteUnits(string formatted, out string hourUnit, out string minuteUnit)
        {
            hourUnit = null;
            minuteUnit = null;
            if (string.IsNullOrWhiteSpace(formatted))
            {
                return false;
            }

            int index = 0;
            if (!TryReadPlaytimeUnit(formatted, ref index, out string firstUnit))
            {
                return false;
            }

            if (TryReadPlaytimeUnit(formatted, ref index, out string secondUnit))
            {
                hourUnit = firstUnit;
                minuteUnit = secondUnit;
                return true;
            }

            if (TryGetUnitParts(firstUnit, out _, out string suffix) && IsHourSuffix(suffix))
            {
                hourUnit = firstUnit;
                minuteUnit = PlaylistLocalization.Format("LOCPlaylist_Playtime_MinuteUnit", "0");
                return true;
            }

            return false;
        }

        private static object ResolveVisibility(string part, ulong seconds)
        {
            if (string.Equals(part, "Visible", StringComparison.OrdinalIgnoreCase))
            {
                return seconds > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (string.Equals(part, "SubHour", StringComparison.OrdinalIgnoreCase))
            {
                return seconds > 0 && seconds < SecondsPerHour ? Visibility.Visible : Visibility.Collapsed;
            }

            if (string.Equals(part, "HourPlus", StringComparison.OrdinalIgnoreCase))
            {
                return seconds >= SecondsPerHour ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        private static string ResolveTextPart(string part, ulong seconds, CultureInfo culture)
        {
            if (seconds < SecondsPerHour)
            {
                if (!TryGetSubHourDisplay(seconds, culture, out string digits, out string suffix))
                {
                    return string.Empty;
                }

                if (string.Equals(part, "SubHourSuffix", StringComparison.OrdinalIgnoreCase))
                {
                    return suffix;
                }

                if (string.Equals(part, "SubHourDigit", StringComparison.OrdinalIgnoreCase))
                {
                    return digits;
                }

                return string.Empty;
            }

            if (string.Equals(part, "UnitSeparator", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveHourPlusUnitSeparator();
            }

            if (!TryGetHourPlusUnits(seconds, culture, out string hourUnit, out string minuteUnit))
            {
                return string.Empty;
            }

            if (string.Equals(part, "Minutes", StringComparison.OrdinalIgnoreCase))
            {
                return minuteUnit;
            }

            if (string.Equals(part, "Hours", StringComparison.OrdinalIgnoreCase))
            {
                return hourUnit;
            }

            return string.Empty;
        }

        private static bool IsVisibilityPart(string part)
        {
            return string.Equals(part, "Visible", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "SubHour", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "HourPlus", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSubHourDisplay(ulong seconds, CultureInfo culture, out string digits, out string suffix)
        {
            digits = null;
            suffix = null;
            if (seconds == 0 || seconds >= SecondsPerHour)
            {
                return false;
            }

            string formatted = FormatSeconds(seconds, culture);
            if (TryParseSubHourThemeDisplay(formatted, out digits, out suffix))
            {
                return true;
            }

            long minutes = Math.Max(1, (long)(seconds / 60));
            string full = PlaylistLocalization.Format("LOCPlaylist_Playtime_Minutes", minutes);
            if (TryParseSubHourThemeDisplay(full, out digits, out suffix))
            {
                return true;
            }

            digits = minutes.ToString(culture);
            suffix = full.Length > digits.Length ? full.Substring(digits.Length) : string.Empty;
            return !string.IsNullOrEmpty(suffix);
        }

        /// <summary>
        /// Derives the gap between hour and minute units from the locale's playtime format templates
        /// so spacing matches <see cref="LOCPlaylist_Playtime_HoursMinutes"/> without a separate key.
        /// </summary>
        internal static string ResolveHourPlusUnitSeparator()
        {
            const string hourPlaceholder = "8";
            const string minutePlaceholder = "9";
            string combined = PlaylistLocalization.Format(
                "LOCPlaylist_Playtime_HoursMinutes",
                hourPlaceholder,
                minutePlaceholder);
            string hourOnly = PlaylistLocalization.Format(
                "LOCPlaylist_Playtime_HoursOnly",
                hourPlaceholder);
            string minuteOnly = PlaylistLocalization.Format(
                "LOCPlaylist_Playtime_MinuteUnit",
                minutePlaceholder);

            if (string.IsNullOrEmpty(combined)
                || !combined.StartsWith(hourOnly, StringComparison.Ordinal)
                || !combined.EndsWith(minuteOnly, StringComparison.Ordinal))
            {
                return " ";
            }

            int separatorLength = combined.Length - hourOnly.Length - minuteOnly.Length;
            return separatorLength <= 0
                ? string.Empty
                : combined.Substring(hourOnly.Length, separatorLength);
        }

        private static bool TryGetHourPlusUnits(ulong seconds, CultureInfo culture, out string hourUnit, out string minuteUnit)
        {
            hourUnit = null;
            minuteUnit = null;

            long totalMinutes = (long)(seconds / 60);
            long hoursValue = totalMinutes / 60;
            long minutesValue = totalMinutes % 60;
            string hoursText = hoursValue.ToString(culture);
            string minutesText = minutesValue.ToString(culture);
            string structuredHourUnit = PlaylistLocalization.Format(
                "LOCPlaylist_Playtime_HoursOnly",
                hoursText);
            string structuredMinuteUnit = PlaylistLocalization.Format(
                "LOCPlaylist_Playtime_MinuteUnit",
                minutesText);
            string structuredCombined = PlaylistLocalization.Format(
                "LOCPlaylist_Playtime_HoursMinutes",
                hoursText,
                minutesText);

            string formatted = FormatSeconds(seconds, culture);
            if (string.Equals(formatted, structuredCombined, StringComparison.Ordinal))
            {
                hourUnit = structuredHourUnit;
                minuteUnit = structuredMinuteUnit;
                return true;
            }

            if (TryParseThemeHourMinuteUnits(formatted, out hourUnit, out minuteUnit))
            {
                return true;
            }

            hourUnit = structuredHourUnit;
            minuteUnit = structuredMinuteUnit;
            return true;
        }

        private static string FormatSeconds(ulong seconds, CultureInfo culture)
        {
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

        private static bool TryGetUnitParts(string unit, out string digits, out string suffix)
        {
            digits = null;
            suffix = null;
            if (string.IsNullOrEmpty(unit))
            {
                return false;
            }

            int index = 0;
            if (!TryReadDigitRun(unit, ref index, out digits))
            {
                return false;
            }

            suffix = unit.Substring(index);
            return true;
        }

        private static bool IsHourSuffix(string suffix)
        {
            return !string.IsNullOrEmpty(suffix)
                && (suffix.Equals("h", StringComparison.OrdinalIgnoreCase)
                    || suffix.StartsWith("hr", StringComparison.OrdinalIgnoreCase)
                    || suffix.StartsWith("hour", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsMinuteSuffix(string suffix)
        {
            return !string.IsNullOrEmpty(suffix)
                && (suffix.Equals("m", StringComparison.OrdinalIgnoreCase)
                    || suffix.StartsWith("min", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryReadPlaytimeUnit(string text, ref int index, out string unit)
        {
            unit = null;
            if (!TryReadDigitRun(text, ref index, out string digits))
            {
                return false;
            }

            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            int suffixStart = index;
            while (index < text.Length && char.IsLetter(text[index]))
            {
                index++;
            }

            string suffix = suffixStart < index ? text.Substring(suffixStart, index - suffixStart) : string.Empty;
            unit = string.IsNullOrEmpty(suffix) ? digits : digits + suffix;
            return true;
        }

        private static bool TryReadDigitRun(string text, ref int index, out string digits)
        {
            digits = null;
            while (index < text.Length && !char.IsDigit(text[index]))
            {
                index++;
            }

            if (index >= text.Length)
            {
                return false;
            }

            int start = index;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            digits = text.Substring(start, index - start);
            return digits.Length > 0;
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
