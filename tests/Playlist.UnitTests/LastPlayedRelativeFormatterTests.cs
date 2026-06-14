using System;
using Xunit;

namespace Playlist.UnitTests;

public class LastPlayedRelativeFormatterTests
{
    private static readonly DateTime ReferenceNow = new DateTime(2026, 06, 06, 12, 0, 0, DateTimeKind.Utc);
    private const double DaysPerDisplayMonth = 30.44;
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 60 * SecondsPerMinute;
    private const int SecondsPerDay = 24 * SecondsPerHour;
    private const int SecondsPerWeek = 7 * SecondsPerDay;
    private const int SecondsPerMonth = (int)(DaysPerDisplayMonth * SecondsPerDay);

    [Fact]
    public void Format_Unplayed_IsBlank()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(null, ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Unplayed, value.Unit);
        Assert.Equal(string.Empty, value.Label);
    }

    [Fact]
    public void Format_UnderMinute_UsesMomentsAgo()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddSeconds(-59), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Moment, value.Unit);
        Assert.Equal("Moments ago", value.Label);
    }

    [Fact]
    public void Format_OneMinuteBoundary_UsesOneMinuteAgo()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddSeconds(-60), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Minute, value.Unit);
        Assert.Equal(" 1 minute ago", value.Label);
    }

    [Theory]
    [InlineData(119, " 1 minute ago")]
    [InlineData(120, " 2 minutes ago")]
    [InlineData(3599, "59 minutes ago")]
    public void Format_MinutePluralAndUpperBound_AreCorrect(int secondsAgo, string expected)
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo(secondsAgo);
        Assert.Equal(LastPlayedBucketUnit.Minute, value.Unit);
        Assert.Equal(expected, value.Label);
    }

    [Fact]
    public void Format_OneHourBoundary_UsesOneHourAgo()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddHours(-1), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Hour, value.Unit);
        Assert.Equal(" 1 hour ago", value.Label);
    }

    [Theory]
    [InlineData(7199, " 1 hour ago")]
    [InlineData(7200, " 2 hours ago")]
    [InlineData(86399, "23 hours ago")]
    public void Format_HourPluralAndUpperBound_AreCorrect(int secondsAgo, string expected)
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo(secondsAgo);
        Assert.Equal(LastPlayedBucketUnit.Hour, value.Unit);
        Assert.Equal(expected, value.Label);
    }

    [Fact]
    public void Format_OneDayBoundary_UsesOneDayAgo()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddDays(-1), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Day, value.Unit);
        Assert.Equal(" 1 day ago", value.Label);
    }

    [Theory]
    [InlineData(172800, " 2 days ago")]
    [InlineData(604799, " 6 days ago")]
    public void Format_DayPluralAndUpperBound_AreCorrect(int secondsAgo, string expected)
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo(secondsAgo);
        Assert.Equal(LastPlayedBucketUnit.Day, value.Unit);
        Assert.Equal(expected, value.Label);
    }

    [Fact]
    public void Format_OneWeekBoundary_UsesOneWeekAgo()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddDays(-7), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Week, value.Unit);
        Assert.Equal(" 1 week ago", value.Label);
    }

    [Theory]
    [InlineData(1209600, " 2 weeks ago")]
    [InlineData(2419199, " 3 weeks ago")]
    public void Format_WeekPluralAndUpperBound_AreCorrect(int secondsAgo, string expected)
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo(secondsAgo);
        Assert.Equal(LastPlayedBucketUnit.Week, value.Unit);
        Assert.Equal(expected, value.Label);
    }

    [Fact]
    public void Format_FourWeeksBoundary_RemainsWeeks()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddDays(-28), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Week, value.Unit);
        Assert.Equal(" 4 weeks ago", value.Label);
    }

    [Fact]
    public void Format_ThirtyPointFourFourDayBoundary_UsesOneMonthAgo()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddDays(-DaysPerDisplayMonth), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.Month, value.Unit);
        Assert.Equal(" 1 month ago", value.Label);
    }

    [Fact]
    public void Format_JustBeforeMonthBoundary_RemainsWeeks()
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo(SecondsPerMonth - 1);
        Assert.Equal(LastPlayedBucketUnit.Week, value.Unit);
        Assert.Equal(" 4 weeks ago", value.Label);
    }

    [Theory]
    [InlineData(2, " 2 months ago")]
    [InlineData(11, "11 months ago")]
    public void Format_MonthPlural_AreCorrect(int months, string expected)
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo(months * SecondsPerMonth);
        Assert.Equal(LastPlayedBucketUnit.Month, value.Unit);
        Assert.Equal(expected, value.Label);
    }

    [Fact]
    public void Format_MinuteValuesGetDistinctSortBuckets()
    {
        LastPlayedDisplayValue oneMinute = LastPlayedRelativeFormatter.Format(ReferenceNow.AddMinutes(-1), ReferenceNow);
        LastPlayedDisplayValue twoMinutes = LastPlayedRelativeFormatter.Format(ReferenceNow.AddMinutes(-2), ReferenceNow);
        LastPlayedDisplayValue threeMinutes = LastPlayedRelativeFormatter.Format(ReferenceNow.AddMinutes(-3), ReferenceNow);

        Assert.NotEqual(oneMinute.SortBucket, twoMinutes.SortBucket);
        Assert.NotEqual(twoMinutes.SortBucket, threeMinutes.SortBucket);
    }

    [Fact]
    public void Format_TwelveToTwentyThreeMonths_UsesOneYearAgo()
    {
        LastPlayedDisplayValue value12 = LastPlayedRelativeFormatter.Format(ReferenceNow.AddDays(-(DaysPerDisplayMonth * 12)), ReferenceNow);
        LastPlayedDisplayValue value23 = LastPlayedRelativeFormatter.Format(ReferenceNow.AddDays(-(DaysPerDisplayMonth * 23)), ReferenceNow);

        Assert.Equal(LastPlayedBucketUnit.Year, value12.Unit);
        Assert.Equal(" 1 year ago", value12.Label);
        Assert.Equal(LastPlayedBucketUnit.Year, value23.Unit);
        Assert.Equal(" 1 year ago", value23.Label);
    }

    [Fact]
    public void Format_JustBeforeTwelveMonths_RemainsMonths()
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo((12 * SecondsPerMonth) - 1);
        Assert.Equal(LastPlayedBucketUnit.Month, value.Unit);
        Assert.Equal("11 months ago", value.Label);
    }

    [Fact]
    public void Format_TwentyFourMonthsAndAbove_UsesLongAgo()
    {
        LastPlayedDisplayValue value = LastPlayedRelativeFormatter.Format(ReferenceNow.AddDays(-(DaysPerDisplayMonth * 24)), ReferenceNow);
        Assert.Equal(LastPlayedBucketUnit.LongAgo, value.Unit);
        Assert.Equal("Long ago", value.Label);
    }

    [Fact]
    public void Format_JustBeforeTwentyFourMonths_RemainsOneYearAgo()
    {
        LastPlayedDisplayValue value = FormatBySecondsAgo((24 * SecondsPerMonth) - 1);
        Assert.Equal(LastPlayedBucketUnit.Year, value.Unit);
        Assert.Equal(" 1 year ago", value.Label);
    }

    private static LastPlayedDisplayValue FormatBySecondsAgo(int secondsAgo)
    {
        return LastPlayedRelativeFormatter.Format(ReferenceNow.AddSeconds(-secondsAgo), ReferenceNow);
    }
}
