using System;

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
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Moment, MomentBucketOrder, "Moments ago");
            }

            if (totalSeconds < SecondsPerHour)
            {
                int minutes = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerMinute));
                int sortBucket = FirstMinuteBucketOrder + (minutes - 1);
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Minute, sortBucket, FormatUnit(minutes, "minute"));
            }

            if (totalSeconds < SecondsPerDay)
            {
                int hours = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerHour));
                int sortBucket = FirstHourBucketOrder + (hours - 1);
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Hour, sortBucket, FormatUnit(hours, "hour"));
            }

            if (totalSeconds < SecondsPerWeek)
            {
                int days = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerDay));
                int sortBucket = FirstDayBucketOrder + (days - 1);
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Day, sortBucket, FormatUnit(days, "day"));
            }

            if (totalSeconds < SecondsPerMonth)
            {
                int weeks = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerWeek));
                int sortBucket = FirstWeekBucketOrder + (weeks - 1);
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Week, sortBucket, FormatUnit(weeks, "week"));
            }

            int months = Math.Max(1, (int)Math.Floor(totalSeconds / SecondsPerMonth));
            if (months < 12)
            {
                int sortBucket = FirstMonthBucketOrder + (months - 1);
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Month, sortBucket, FormatUnit(months, "month"));
            }

            if (months < 24)
            {
                return new LastPlayedDisplayValue(LastPlayedBucketUnit.Year, YearBucketOrder, "1 year ago");
            }

            return new LastPlayedDisplayValue(LastPlayedBucketUnit.LongAgo, LongAgoBucketOrder, "Long ago");
        }

        /// <summary>
        /// Builds singular/plural relative label text for a single unit.
        /// </summary>
        private static string FormatUnit(int value, string unit)
        {
            return value == 1
                ? $"1 {unit} ago"
                : $"{value} {unit}s ago";
        }
    }
}
