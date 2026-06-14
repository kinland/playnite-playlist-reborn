using Xunit;

namespace Playlist.UnitTests;

public class HltbColumnHeaderLabelTests
{
    [Theory]
    [InlineData("Main Story", " ({0})", " (Main Story)")]
    [InlineData("Completionist", " ({0})", " (Completionist)")]
    public void FormatActiveSortSuffix_wraps_type_label(string typeLabel, string format, string expected)
    {
        Assert.Equal(expected, string.Format(format, typeLabel));
    }

    [Theory]
    [InlineData("Main Story", " (sort by {0})", " (sort by Main Story)")]
    [InlineData("Main + Extra", " (sort by {0})", " (sort by Main + Extra)")]
    public void FormatHoverSortSuffix_includes_sort_by_prefix(string typeLabel, string format, string expected)
    {
        Assert.Equal(expected, string.Format(format, typeLabel));
    }

    [Theory]
    [InlineData(0, "LOCPlaylist_Hltb_TimeType_MainStory", "LOCHowLongToBeatMainStory", "Main story")]
    [InlineData(1, "LOCPlaylist_Hltb_TimeType_MainExtra", "LOCHowLongToBeatMainExtra", "Main + extra")]
    [InlineData(2, "LOCPlaylist_Hltb_TimeType_Completionist", "LOCHowLongToBeatCompletionist", "Completionist")]
    [InlineData(3, "LOCPlaylist_Hltb_TimeType_Solo", "LOCHowLongToBeatSolo", "Solo")]
    [InlineData(4, "LOCPlaylist_Hltb_TimeType_CoOp", "LOCHowLongToBeatCoOp", "Co-Op")]
    [InlineData(5, "LOCPlaylist_Hltb_TimeType_Versus", "LOCHowLongToBeatVs", "Vs")]
    public void GetPreferredTimeTypeResourceKey_maps_each_preferred_time_type(
        int rawType,
        string expectedPlaylistKey,
        string expectedHltbKey,
        string expectedEnglishBaseline)
    {
        var type = (HltbPreferredTimeType)rawType;
        Assert.Equal(expectedPlaylistKey, HltbColumnHeaderLabels.GetPreferredTimeTypeResourceKey(type));
        Assert.Equal(expectedHltbKey, HltbColumnHeaderLabels.GetHltbTimeTypeResourceKey(type));
        Assert.Equal(expectedEnglishBaseline, HltbColumnHeaderLabels.GetHltbTimeTypeEnglishBaseline(type));
    }

    [Fact]
    public void ColumnHeaderLocKey_matches_column_toggle_menu_key()
    {
        Assert.Equal("LOCPlaylist_Column_HowLongToBeat", HltbColumnHeaderLabels.ColumnHeaderLocKey);
    }
}
