using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Playlist.UnitTests;

public class PlaylistReorderPlannerTests
{
    [Fact]
    public void Reorder_FilteredAscending_DropAbove_ShiftsGlobalOrder()
    {
        List<string> full = L("A", "B", "C", "D", "E");
        List<string> visible = L("A", "C", "E");
        List<string> dragged = L("E");

        List<string> result = PlaylistReorderPlanner.ReorderByVisibleInsertion(
            fullOrder: full,
            visibleOrderVisual: visible,
            draggedItemsVisual: dragged,
            originalInsertIndexVisual: 1,
            reverseVisualToPersisted: false,
            anchorPreference: ReorderAnchorPreference.PreferAfterAnchor);

        Assert.Equal(L("A", "B", "E", "C", "D"), result);
    }

    [Fact]
    public void Reorder_FilteredAscending_DropBelow_ShiftsGlobalOrder()
    {
        List<string> full = L("A", "B", "C", "D", "E");
        List<string> visible = L("A", "C", "E");
        List<string> dragged = L("A");

        List<string> result = PlaylistReorderPlanner.ReorderByVisibleInsertion(
            fullOrder: full,
            visibleOrderVisual: visible,
            draggedItemsVisual: dragged,
            originalInsertIndexVisual: 2,
            reverseVisualToPersisted: false,
            anchorPreference: ReorderAnchorPreference.PreferBeforeAnchor);

        Assert.Equal(L("B", "C", "A", "D", "E"), result);
    }

    [Fact]
    public void Reorder_FilteredDescending_DropAbove_MapsToPersistedOrder()
    {
        List<string> full = L("A", "B", "C", "D", "E");
        List<string> visibleDescending = L("E", "C", "A");
        List<string> dragged = L("A");

        List<string> result = PlaylistReorderPlanner.ReorderByVisibleInsertion(
            fullOrder: full,
            visibleOrderVisual: visibleDescending,
            draggedItemsVisual: dragged,
            originalInsertIndexVisual: 1,
            reverseVisualToPersisted: true,
            anchorPreference: ReorderAnchorPreference.PreferBeforeAnchor);

        Assert.Equal(L("B", "C", "A", "D", "E"), result);
    }

    [Fact]
    public void Reorder_FilteredDescending_DropBelow_MapsToPersistedOrder()
    {
        List<string> full = L("A", "B", "C", "D", "E");
        List<string> visibleDescending = L("E", "C", "A");
        List<string> dragged = L("E");

        List<string> result = PlaylistReorderPlanner.ReorderByVisibleInsertion(
            fullOrder: full,
            visibleOrderVisual: visibleDescending,
            draggedItemsVisual: dragged,
            originalInsertIndexVisual: 2,
            reverseVisualToPersisted: true,
            anchorPreference: ReorderAnchorPreference.PreferAfterAnchor);

        Assert.Equal(L("A", "B", "E", "C", "D"), result);
    }

    [Fact]
    public void Reorder_PreservesRelativeOrderOfHiddenItems()
    {
        List<string> full = L("A", "B", "C", "D", "E", "F", "G");
        List<string> visible = L("A", "D", "G");
        List<string> dragged = L("G");

        List<string> result = PlaylistReorderPlanner.ReorderByVisibleInsertion(
            fullOrder: full,
            visibleOrderVisual: visible,
            draggedItemsVisual: dragged,
            originalInsertIndexVisual: 1,
            reverseVisualToPersisted: false,
            anchorPreference: ReorderAnchorPreference.PreferAfterAnchor);

        Assert.Equal(L("A", "B", "C", "G", "D", "E", "F"), result);
        Assert.Equal(L("B", "C", "E", "F"), result.Where(item => item is "B" or "C" or "E" or "F").ToList());
    }

    [Fact]
    public void Reorder_FilteredAscending_MultiDrag_PreservesDraggedRelativeOrder()
    {
        List<string> full = L("A", "B", "C", "D", "E", "F");
        List<string> visible = L("A", "C", "D", "F");
        List<string> dragged = L("C", "D");

        List<string> result = PlaylistReorderPlanner.ReorderByVisibleInsertion(
            fullOrder: full,
            visibleOrderVisual: visible,
            draggedItemsVisual: dragged,
            originalInsertIndexVisual: 0,
            reverseVisualToPersisted: false,
            anchorPreference: ReorderAnchorPreference.PreferAfterAnchor);

        Assert.Equal(L("C", "D", "A", "B", "E", "F"), result);
    }

    [Fact]
    public void Reorder_FilteredAscending_DragPathfinderAboveVoid_PreservesHiddenOrder()
    {
        List<string> full = L("Alan Wake 2", "Moonbreaker", "VOID/BREAKER", "Hidden-10", "Hidden-11", "Pathfinder", "Gatekeeper");
        List<string> visible = L("Alan Wake 2", "Moonbreaker", "VOID/BREAKER", "Pathfinder", "Gatekeeper");
        List<string> dragged = L("Pathfinder");

        List<string> result = PlaylistReorderPlanner.ReorderByVisibleInsertion(
            fullOrder: full,
            visibleOrderVisual: visible,
            draggedItemsVisual: dragged,
            originalInsertIndexVisual: 2,
            reverseVisualToPersisted: false,
            anchorPreference: ReorderAnchorPreference.PreferAfterAnchor);

        Assert.Equal(
            L("Alan Wake 2", "Moonbreaker", "Pathfinder", "VOID/BREAKER", "Hidden-10", "Hidden-11", "Gatekeeper"),
            result);
    }

    private static List<string> L(params string[] items) => items.ToList();
}
