using Xunit;

namespace Playlist.UnitTests;

public class PlaylistColumnReorderDragRulesTests
{
    [Fact]
    public void ShouldShowDropGuide_requires_floating_header_and_header_row_hit()
    {
        Assert.True(PlaylistColumnReorderDragRules.ShouldShowDropGuide(
            leftButtonPressed: true,
            isThumbCaptured: false,
            isPlaylistRowDragActive: false,
            isMouseOverHeaderRow: true,
            hasFloatingColumnHeader: true));

        Assert.False(PlaylistColumnReorderDragRules.ShouldShowDropGuide(
            leftButtonPressed: true,
            isThumbCaptured: false,
            isPlaylistRowDragActive: false,
            isMouseOverHeaderRow: false,
            hasFloatingColumnHeader: true));
    }

    [Fact]
    public void ShouldShowDropGuide_is_false_during_playlist_row_drag()
    {
        Assert.False(PlaylistColumnReorderDragRules.ShouldShowDropGuide(
            leftButtonPressed: true,
            isThumbCaptured: false,
            isPlaylistRowDragActive: true,
            isMouseOverHeaderRow: true,
            hasFloatingColumnHeader: true));
    }

    [Fact]
    public void ShouldShowDropGuide_is_false_while_resizing_columns()
    {
        Assert.False(PlaylistColumnReorderDragRules.ShouldShowDropGuide(
            leftButtonPressed: true,
            isThumbCaptured: true,
            isPlaylistRowDragActive: false,
            isMouseOverHeaderRow: true,
            hasFloatingColumnHeader: true));
    }
}
