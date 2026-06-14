using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class HltbPlaytimeFormatTests
{
    static HltbPlaytimeFormatTests()
    {
        PlaylistTestLocalization.Install();
    }

    [Fact]
    public void FormatSeconds_ReturnsUnknownForZero()
    {
        string formatted = HltbPlaytimeFormat.FormatSeconds(0, integrationViewItemOnlyHour: false, themeScope: null);
        Assert.Equal("--", formatted);
    }

    [Fact]
    public void FormatSeconds_OnlyHourOmitsMinutes()
    {
        string formatted = HltbPlaytimeFormat.FormatSeconds(5400, integrationViewItemOnlyHour: true, themeScope: null);
        Assert.Equal("2h", formatted);
    }

    [Fact]
    public void FormatSeconds_DefaultFallbackIncludesMinutes()
    {
        string formatted = HltbPlaytimeFormat.FormatSeconds(5400, integrationViewItemOnlyHour: false, themeScope: null);
        Assert.Equal("1h 30m", formatted);
    }
}
