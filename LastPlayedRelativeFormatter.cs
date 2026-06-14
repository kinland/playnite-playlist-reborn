using System;
using System.Globalization;

namespace Playlist
{
    /// <summary>
    /// Display unit for Last Played labels.
    /// </summary>
    internal enum LastPlayedBucketUnit
    {
        Moment = 0,
        Minute = 1,
        Hour = 2,
        Day = 3,
        Week = 4,
        Month = 5,
        Year = 6,
        LongAgo = 7,
        Unplayed = 8,
    }

    /// <summary>
    /// Display payload for Last Played formatting and sort bucketing.
    /// </summary>
    internal readonly struct LastPlayedDisplayValue
    {
        public LastPlayedBucketUnit Unit { get; }
        public int SortBucket { get; }
        public string Label { get; }

        public LastPlayedDisplayValue(LastPlayedBucketUnit unit, int sortBucket, string label)
        {
            Unit = unit;
            SortBucket = sortBucket;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>
    /// Formats LastActivity values into single-unit relative labels and stable sort buckets.
    /// </summary>
    internal static class LastPlayedRelativeFormatter
    {
        private const int SecondsPerMinute  = 60;
        private const int SecondsPerHour    = 60 * SecondsPerMinute;
        private const int SecondsPerDay     = 24 * SecondsPerHour;
        private const int SecondsPerWeek    = 7 * SecondsPerDay;
        private const int SecondsPerMonth   = (int)(30.44 * SecondsPerDay); // Deliberate truncated 30.44-day display month (2,630,016 seconds)

        private const int MomentBucketOrder      = 0;   // Moments ago
        private const int FirstMinuteBucketOrder = 1;   // x minute(s) ago
        private const int FirstHourBucketOrder   = 60;  // x hour(s) ago
        private const int FirstDayBucketOrder    = 83;  // x day(s) ago
        private const int FirstWeekBucketOrder   = 89;  // x week(s) ago
        private const int FirstMonthBucketOrder  = 92;  // x month(s) ago
        private const int YearBucketOrder        = 103; // 1 year ago
        private const int LongAgoBucketOrder     = 104; // Long ago
        private const int UnplayedBucketOrder    = 105; // Unplayed

        /// <summary>
        /// Bucket id used for "Moments ago" rows, which sort by exact recency.
        /// </summary>
        public static int MomentsBucketOrder => MomentBucketOrder;

        /// <summary>
        /// Bucket id used for games with no Last Played timestamp.
        /// </summary>
        public static int UnplayedSortBucketOrder => UnplayedBucketOrder;

        /// <summary>
        /// Converts a Last Played timestamp into display label + sortable bucket metadata.
        /// </summary>
        public static LastPlayedDisplayValue Format(DateTime? lastPlayed, DateTime now)
        {
            if (!lastPlayed.HasValue)
            {
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Unplayed, UnplayedBucketOrder, string.Empty);
            }

            DateTime played = lastPlayed.Value;
            TimeSpan elapsed = now - played;
            double totalSeconds = Math.Max(0, elapsed.TotalSeconds);

            if (totalSeconds < SecondsPerMinute)
            {
                return new LastPlayedDisplayValue(
                    LastPlayedBucketUnit.Moment,
                    MomentBucketOrder,
                    PlaylistLocalization.GetString("LOCPlaylist_LastPlayed_MomentsAgo"));
            }

            if (totalSeconds < SecondsPerHour)
            {
                int minutes = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerMinute));
                int sortBucket = FirstMinuteBucketOrder + (minutes - 1);
                return new LastPlayedDisplayValue(
                    LastPlayedBucketUnit.Minute,
                    sortBucket,
                    FormatRelativeUnit(
                        minutes,
                        "LOCPlaylist_LastPlayed_OneMinuteAgo",
                        "LOCPlaylist_LastPlayed_MinutesAgo"));
            }

            if (totalSeconds < SecondsPerDay)
            {
                int hours = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerHour));
                int sortBucket = FirstHourBucketOrder + (hours - 1);
                return new LastPlayedDisplayValue(
                    LastPlayedBucketUnit.Hour,
                    sortBucket,
                    FormatRelativeUnit(
                        hours,
                        "LOCPlaylist_LastPlayed_OneHourAgo",
                        "LOCPlaylist_LastPlayed_HoursAgo"));
            }

            if (totalSeconds < SecondsPerWeek)
            {
                int days = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerDay));
                int sortBucket = FirstDayBucketOrder + (days - 1);
                return new LastPlayedDisplayValue(
                    LastPlayedBucketUnit.Day,
                    sortBucket,
                    FormatRelativeUnit(
                        days,
                        "LOCPlaylist_LastPlayed_OneDayAgo",
                        "LOCPlaylist_LastPlayed_DaysAgo"));
            }

            if (totalSeconds < SecondsPerMonth)
            {
                int weeks = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerWeek));
                int sortBucket = FirstWeekBucketOrder + (weeks - 1);
                return new LastPlayedDisplayValue(
                    LastPlayedBucketUnit.Week,
                    sortBucket,
                    FormatRelativeUnit(
                        weeks,
                        "LOCPlaylist_LastPlayed_OneWeekAgo",
                        "LOCPlaylist_LastPlayed_WeeksAgo"));
            }

            int months = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerMonth));
            if (months < 12)
            {
                int sortBucket = FirstMonthBucketOrder + (months - 1);
                return new LastPlayedDisplayValue(
                    LastPlayedBucketUnit.Month,
                    sortBucket,
                    FormatRelativeUnit(
                        months,
                        "LOCPlaylist_LastPlayed_OneMonthAgo",
                        "LOCPlaylist_LastPlayed_MonthsAgo"));
            }

            if (months < 24)
            {
                return new LastPlayedDisplayValue(
                    LastPlayedBucketUnit.Year,
                    YearBucketOrder,
                    PlaylistLocalization.Format("LOCPlaylist_LastPlayed_OneYearAgo", FormatCount(1)));
            }

            return new LastPlayedDisplayValue(
                LastPlayedBucketUnit.LongAgo,
                LongAgoBucketOrder,
                PlaylistLocalization.GetString("LOCPlaylist_LastPlayed_LongAgo"));
        }

        private static string FormatRelativeUnit(int value, string singularKey, string pluralKey)
        {
            string formatKey = value == 1 ? singularKey : pluralKey;
            return PlaylistLocalization.Format(formatKey, FormatCount(value));
        }

        private static string FormatCount(int value)
        {
            return value.ToString(CultureInfo.CurrentCulture);
        }
    }
}
