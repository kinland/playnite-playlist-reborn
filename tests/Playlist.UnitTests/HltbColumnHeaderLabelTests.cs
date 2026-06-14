using Xunit;

namespace Playlist.UnitTests;

public class HltbColumnHeaderLabelTests
{
    [Theory]
    [InlineData("Main Story", " (Main Story)")]
    [InlineData("Completionist", " (Completionist)")]
    public void FormatActiveSortSuffix_wraps_type_label(string typeLabel, string expected)
    {
        Assert.Equal(expected, HltbColumnHeaderLabels.FormatActiveSortSuffix(typeLabel));
    }

    [Theory]
    [InlineData("Main Story", " (sort by Main Story)")]
    [InlineData("Main + Extra", " (sort by Main + Extra)")]
    public void FormatHoverSortSuffix_includes_sort_by_prefix(string typeLabel, string expected)
    {
        Assert.Equal(expected, HltbColumnHeaderLabels.FormatHoverSortSuffix(typeLabel));
    }

    [Fact]
    public void BaseText_is_column_name_without_suffix()
    {
        Assert.Equal("HowLongToBeat", HltbColumnHeaderLabels.BaseText);
    }
}
