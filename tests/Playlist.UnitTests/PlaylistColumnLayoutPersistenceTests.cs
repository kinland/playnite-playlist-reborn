using Xunit;

namespace Playlist.UnitTests;

public class PlaylistColumnLayoutPersistenceTests
{
    [Fact]
    public void MergeVisibleAndHiddenColumnOrder_restores_hidden_column_slot()
    {
        var previous = new Dictionary<string, PlaylistColumnLayoutState>
        {
            ["Rank"] = new PlaylistColumnLayoutState { Key = "Rank", DisplayIndex = 0, Width = 45 },
            ["Name"] = new PlaylistColumnLayoutState { Key = "Name", DisplayIndex = 1, Width = 200 },
            ["Playtime"] = new PlaylistColumnLayoutState { Key = "Playtime", DisplayIndex = 2, Width = 111 },
        };

        List<string> visible = new List<string> { "Rank", "Playtime" };
        List<string> hidden = new List<string> { "Name" };

        List<string> merged = PlaylistColumnLayoutPersistence.MergeVisibleAndHiddenColumnOrder(
            visible,
            hidden,
            previous,
            totalColumns: 3);

        Assert.Equal(new[] { "Rank", "Name", "Playtime" }, merged);
    }

    [Fact]
    public void ResolvePersistedWidthForColumnKey_without_gripper_drag_reuses_saved_width()
    {
        var previous = new Dictionary<string, PlaylistColumnLayoutState>
        {
            ["Name"] = new PlaylistColumnLayoutState { Key = "Name", DisplayIndex = 1, Width = 320 },
        };

        double width = PlaylistColumnLayoutPersistence.ResolvePersistedWidthForColumnKey(
            "Name",
            visibleColumnWidth: 180,
            isVisibleInGrid: true,
            persistColumnWidths: false,
            previous,
            columnLayouts: previous.Values);

        Assert.Equal(320, width);
    }

    [Fact]
    public void ResolvePersistedWidthForColumnKey_with_gripper_drag_uses_visible_width()
    {
        var previous = new Dictionary<string, PlaylistColumnLayoutState>
        {
            ["Name"] = new PlaylistColumnLayoutState { Key = "Name", DisplayIndex = 1, Width = 320 },
        };

        double width = PlaylistColumnLayoutPersistence.ResolvePersistedWidthForColumnKey(
            "Name",
            visibleColumnWidth: 240,
            isVisibleInGrid: true,
            persistColumnWidths: true,
            previous,
            columnLayouts: previous.Values);

        Assert.Equal(240, width);
    }

    [Fact]
    public void GetWidthForLayoutPersistence_ignores_collapsed_on_screen_width()
    {
        var layouts = new[]
        {
            new PlaylistColumnLayoutState { Key = "Name", DisplayIndex = 1, Width = 280 },
        };

        double width = PlaylistColumnLayoutPersistence.GetWidthForLayoutPersistence(
            "Name",
            currentWidth: 0,
            layouts);

        Assert.Equal(280, width);
    }

    [Fact]
    public void ResolvePersistedWidthForColumnKey_icon_column_uses_fixed_width()
    {
        double width = PlaylistColumnLayoutPersistence.ResolvePersistedWidthForColumnKey(
            PlaylistColumnWidthLayout.IconColumnKey,
            visibleColumnWidth: 99,
            isVisibleInGrid: true,
            persistColumnWidths: true,
            previousByKey: new Dictionary<string, PlaylistColumnLayoutState>(),
            columnLayouts: Array.Empty<PlaylistColumnLayoutState>());

        Assert.Equal(PlaylistGridViewLayout.IconColumnWidth, width);
    }
}
