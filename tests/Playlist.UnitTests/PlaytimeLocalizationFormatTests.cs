using System.Globalization;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class PlaytimeLocalizationFormatTests : IDisposable
{
    private readonly Func<string, string> previousGetter;

    public PlaytimeLocalizationFormatTests()
    {
        previousGetter = PlaylistLocalization.TestGetString;
    }

    public void Dispose()
    {
        PlaylistLocalization.TestGetString = previousGetter;
    }

    [Theory]
    [InlineData("{0}h {1}m", "{0}h", "{0}m", " ")]
    [InlineData("{0}時間{1}分", "{0}時間", "{0}分", "")]
    [InlineData("{0} Std. {1} Min.", "{0} Std.", "{0} Min.", " ")]
    [InlineData("{0}ч {1}м", "{0}ч", "{0}м", " ")]
    [InlineData("{0}g {1}p", "{0}g", "{0}p", " ")]
    public void ResolveHourPlusUnitSeparator_DerivesFromTemplates(
        string hoursMinutes,
        string hoursOnly,
        string minuteUnit,
        string expectedSeparator)
    {
        InstallFormats(hoursMinutes, hoursOnly, minuteUnit);

        Assert.Equal(expectedSeparator, PlaytimeValueConverter.ResolveHourPlusUnitSeparator());
    }

    [Theory]
    [InlineData("{0}h {1}m", "{0}h", "{0}m", 46L, 44L)]
    [InlineData("{0}時間{1}分", "{0}時間", "{0}分", 46L, 44L)]
    [InlineData("{0} Std. {1} Min.", "{0} Std.", "{0} Min.", 168L, 59L)]
    public void HourPlusStructuredUnits_ReconstructHoursMinutes(
        string hoursMinutes,
        string hoursOnly,
        string minuteUnit,
        long hours,
        long minutes)
    {
        InstallFormats(hoursMinutes, hoursOnly, minuteUnit);
        var converter = new PlaytimeValueConverter();
        ulong seconds = (ulong)((hours * 60 + minutes) * 60);

        string hourPart = converter.Convert(seconds, typeof(string), "Hours", CultureInfo.InvariantCulture) as string;
        string separator = converter.Convert(seconds, typeof(string), "UnitSeparator", CultureInfo.InvariantCulture) as string;
        string minutePart = converter.Convert(seconds, typeof(string), "Minutes", CultureInfo.InvariantCulture) as string;

        string expected = string.Format(hoursMinutes, hours, minutes);
        Assert.Equal(expected, hourPart + separator + minutePart);
    }

    [Fact]
    public void HourPlusEnglishAbbreviations_MatchCommonGamingConvention()
    {
        InstallFormats("{0}h {1}m", "{0}h", "{0}m");
        var converter = new PlaytimeValueConverter();
        ulong seconds = 168240;

        Assert.Equal("46h", converter.Convert(seconds, typeof(string), "Hours", CultureInfo.InvariantCulture));
        Assert.Equal("44m", converter.Convert(seconds, typeof(string), "Minutes", CultureInfo.InvariantCulture));
    }

    private static void InstallFormats(string hoursMinutes, string hoursOnly, string minuteUnit)
    {
        PlaylistLocalization.TestGetString = key => key switch
        {
            "LOCPlaylist_Playtime_HoursMinutes" => hoursMinutes,
            "LOCPlaylist_Playtime_HoursOnly" => hoursOnly,
            "LOCPlaylist_Playtime_MinuteUnit" => minuteUnit,
            "LOCPlaylist_Playtime_Minutes" => "{0} minutes",
            _ => key,
        };
    }
}
