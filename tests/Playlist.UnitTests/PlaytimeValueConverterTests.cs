using System.Globalization;
using System.Windows;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class PlaytimeValueConverterTests
{
    private readonly PlaytimeValueConverter converter = new PlaytimeValueConverter();

    static PlaytimeValueConverterTests()
    {
        PlaylistTestLocalization.Install();
    }

    [Theory]
    [InlineData("46h 44m", "46h", "44m")]
    [InlineData("111h 6m", "111h", "6m")]
    [InlineData("1 h 5 m", "1h", "5m")]
    [InlineData("3h", "3h", "0m")]
    public void TryParseThemeHourMinuteUnits_ReadsHourPlusUnits(string formatted, string expectedHours, string expectedMinutes)
    {
        Assert.True(PlaytimeValueConverter.TryParseThemeHourMinuteUnits(formatted, out string hours, out string minutes));
        Assert.Equal(expectedHours, hours);
        Assert.Equal(expectedMinutes, minutes);
    }

    [Theory]
    [InlineData("13 minutes")]
    [InlineData("4minutes")]
    public void TryParseThemeHourMinuteUnits_RejectsMinuteOnlyStrings(string formatted)
    {
        Assert.False(PlaytimeValueConverter.TryParseThemeHourMinuteUnits(formatted, out _, out _));
    }

    [Theory]
    [InlineData("13 minutes", "13", " minutes")]
    [InlineData("4minutes", "4", "minutes")]
    [InlineData("57 min", "57", " min")]
    public void TryParseSubHourThemeDisplay_PreservesThemeSuffix(string formatted, string expectedDigits, string expectedSuffix)
    {
        Assert.True(PlaytimeValueConverter.TryParseSubHourThemeDisplay(formatted, out string digits, out string suffix));
        Assert.Equal(expectedDigits, digits);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Fact]
    public void Convert_HourPlusPlaytime_UsesAlignedUnits()
    {
        Assert.Equal("46h", converter.Convert(168240ul, typeof(string), "Hours", CultureInfo.InvariantCulture));
        Assert.Equal("44m", converter.Convert(168240ul, typeof(string), "Minutes", CultureInfo.InvariantCulture));
        Assert.Equal(" ", converter.Convert(168240ul, typeof(string), "UnitSeparator", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_SubHourPlaytime_PreservesMinutesLabel()
    {
        Assert.Equal("13", converter.Convert(780ul, typeof(string), "SubHourDigit", CultureInfo.InvariantCulture));
        Assert.Equal(" minutes", converter.Convert(780ul, typeof(string), "SubHourSuffix", CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert(780ul, typeof(string), "Hours", CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(780ul, "SubHour", Visibility.Visible)]
    [InlineData(780ul, "HourPlus", Visibility.Collapsed)]
    [InlineData(168240ul, "SubHour", Visibility.Collapsed)]
    [InlineData(168240ul, "HourPlus", Visibility.Visible)]
    [InlineData(0ul, "Visible", Visibility.Collapsed)]
    public void Convert_VisibilityParts(ulong seconds, string part, Visibility expected)
    {
        Assert.Equal(expected, converter.Convert(seconds, typeof(Visibility), part, CultureInfo.InvariantCulture));
    }
}
