using System;
using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    /// <summary>
    /// Controls which persisted anchor is preferred when converting a visual drop operation
    /// into a move in the full playlist order.
    /// </summary>
    internal enum ReorderAnchorPreference
    {
        /// <summary>
        /// Use planner defaults (before anchor first, then after anchor).
        /// </summary>
        Auto = 0,
        /// <summary>
        /// Prefer inserting after the resolved "before" anchor.
        /// </summary>
        PreferBeforeAnchor = 1,
        /// <summary>
        /// Prefer inserting before the resolved "after" anchor.
        /// </summary>
        PreferAfterAnchor = 2,
    }

    /// <summary>
    /// Reorder planning helper that maps drag/drop from a filtered/sorted visual list
    /// back to the persisted full playlist order.
    /// </summary>
    internal static class PlaylistReorderPlanner
    {
        /// <summary>
        /// Produces the new persisted order after inserting dragged visible items at the supplied
        /// visual insert index. Hidden items are preserved and only shifted as needed.
        /// </summary>
        public static List<T> ReorderByVisibleInsertion<T>(
            IList<T> fullOrder,
            IList<T> visibleOrderVisual,
            IList<T> draggedItemsVisual,
            int originalInsertIndexVisual,
            bool reverseVisualToPersisted,
            ReorderAnchorPreference anchorPreference = ReorderAnchorPreference.Auto,
            bool invertAnchorSemantics = false) where T : class
        {
            if (fullOrder == null || visibleOrderVisual == null || draggedItemsVisual == null)
            {
                return fullOrder?.ToList() ?? new List<T>();
            }

            List<T> visible = visibleOrderVisual.ToList();
            List<T> draggedVisual = draggedItemsVisual.Where(item => item != null && visible.Contains(item)).Distinct().ToList();
            if (draggedVisual.Count == 0)
            {
                return fullOrder.ToList();
            }

            // Gong's insert index still counts dragged rows. Remove those rows from the index space first.
            List<int> draggedIndicesVisual = draggedVisual.Select(item => visible.IndexOf(item)).Where(index => index >= 0).ToList();
            int removedBeforeInsert = draggedIndicesVisual.Count(index => index < originalInsertIndexVisual);
            int adjustedInsertIndexVisual = originalInsertIndexVisual - removedBeforeInsert;

            List<T> visualWithoutDragged = visible.Where(item => !draggedVisual.Contains(item)).ToList();
            int insertIndexVisual = Math.Max(0, Math.Min(adjustedInsertIndexVisual, visualWithoutDragged.Count));
            List<T> visualAfter = visualWithoutDragged.ToList();
            visualAfter.InsertRange(insertIndexVisual, draggedVisual);

            // Visual order can be reversed relative to persisted order (rank descending view).
            List<T> persistedAfter = reverseVisualToPersisted
                ? visualAfter.AsEnumerable().Reverse().ToList()
                : visualAfter;
            List<T> draggedPersisted = reverseVisualToPersisted
                ? draggedVisual.AsEnumerable().Reverse().ToList()
                : draggedVisual;

            // Use the post-drop persisted projection to extract stable insertion anchors.
            int insertedStart = persistedAfter.IndexOf(draggedPersisted[0]);
            T beforeAnchor = insertedStart > 0 ? persistedAfter[insertedStart - 1] : null;
            T afterAnchor = insertedStart + draggedPersisted.Count < persistedAfter.Count
                ? persistedAfter[insertedStart + draggedPersisted.Count]
                : null;
            if (invertAnchorSemantics)
            {
                T swap = beforeAnchor;
                beforeAnchor = afterAnchor;
                afterAnchor = swap;
            }

            List<T> fullWorking = fullOrder.ToList();
            List<int> originalDraggedIndices = draggedPersisted.Select(item => fullWorking.IndexOf(item)).Where(index => index >= 0).ToList();
            foreach (T item in draggedPersisted)
            {
                fullWorking.Remove(item);
            }

            int insertIndex = ResolveInsertIndex(fullWorking, beforeAnchor, afterAnchor, originalDraggedIndices, anchorPreference);
            fullWorking.InsertRange(insertIndex, draggedPersisted);
            return fullWorking;
        }

        /// <summary>
        /// Checks whether a visual insert position keeps dragged items inside their current bucket.
        /// This is used by Last Played sorting where only within-bucket drag moves are valid.
        /// </summary>
        public static bool CanInsertWithinSameBucket<T>(
            IList<T> visibleOrderVisual,
            IList<T> draggedItemsVisual,
            int originalInsertIndexVisual,
            Func<T, int> bucketSelector) where T : class
        {
            if (visibleOrderVisual == null || draggedItemsVisual == null || bucketSelector == null)
            {
                return false;
            }

            List<T> visible = visibleOrderVisual.ToList();
            List<T> dragged = draggedItemsVisual.Where(item => item != null && visible.Contains(item)).Distinct().ToList();
            if (dragged.Count == 0)
            {
                return false;
            }

            int draggedBucket = bucketSelector(dragged[0]);
            if (dragged.Any(item => bucketSelector(item) != draggedBucket))
            {
                return false;
            }

            List<int> draggedIndicesVisual = dragged.Select(item => visible.IndexOf(item)).Where(index => index >= 0).ToList();
            int removedBeforeInsert = draggedIndicesVisual.Count(index => index < originalInsertIndexVisual);
            int adjustedInsertIndexVisual = originalInsertIndexVisual - removedBeforeInsert;

            List<T> withoutDragged = visible.Where(item => !dragged.Contains(item)).ToList();
            int insertIndex = Math.Max(0, Math.Min(adjustedInsertIndexVisual, withoutDragged.Count));
            T before = insertIndex > 0 ? withoutDragged[insertIndex - 1] : null;
            T after = insertIndex < withoutDragged.Count ? withoutDragged[insertIndex] : null;

            if (before == null && after == null)
            {
                return true;
            }

            bool beforeMatches = before != null && bucketSelector(before) == draggedBucket;
            bool afterMatches = after != null && bucketSelector(after) == draggedBucket;
            return beforeMatches || afterMatches;
        }

        /// <summary>
        /// Resolves the persisted insertion index using before/after anchors and optional preference hints.
        /// Falls back to original dragged position when anchors are unavailable.
        /// </summary>
        private static int ResolveInsertIndex<T>(
            List<T> fullWorking,
            T beforeAnchor,
            T afterAnchor,
            List<int> originalDraggedIndices,
            ReorderAnchorPreference anchorPreference) where T : class
        {
            bool preferAfter = anchorPreference == ReorderAnchorPreference.PreferAfterAnchor;
            bool preferBefore = anchorPreference == ReorderAnchorPreference.PreferBeforeAnchor;

            if (preferAfter && TryResolveAfterAnchor(fullWorking, afterAnchor, out int insertFromAfterPreferred))
            {
                return insertFromAfterPreferred;
            }

            if (preferBefore && TryResolveBeforeAnchor(fullWorking, beforeAnchor, out int insertFromBeforePreferred))
            {
                return insertFromBeforePreferred;
            }

            if (TryResolveBeforeAnchor(fullWorking, beforeAnchor, out int insertFromBefore))
            {
                return insertFromBefore;
            }

            if (TryResolveAfterAnchor(fullWorking, afterAnchor, out int insertFromAfter))
            {
                return insertFromAfter;
            }

            if (originalDraggedIndices.Count > 0)
            {
                int minOriginal = originalDraggedIndices.Min();
                return Math.Max(0, Math.Min(minOriginal, fullWorking.Count));
            }

            return fullWorking.Count;
        }

        /// <summary>
        /// Tries to resolve insertion index by placing after the before-anchor.
        /// </summary>
        private static bool TryResolveBeforeAnchor<T>(List<T> fullWorking, T beforeAnchor, out int insertIndex) where T : class
        {
            insertIndex = -1;
            if (beforeAnchor == null)
            {
                return false;
            }

            int beforeIndex = fullWorking.IndexOf(beforeAnchor);
            if (beforeIndex < 0)
            {
                return false;
            }

            insertIndex = beforeIndex + 1;
            return true;
        }

        /// <summary>
        /// Tries to resolve insertion index by placing before the after-anchor.
        /// </summary>
        private static bool TryResolveAfterAnchor<T>(List<T> fullWorking, T afterAnchor, out int insertIndex) where T : class
        {
            insertIndex = -1;
            if (afterAnchor == null)
            {
                return false;
            }

            int afterIndex = fullWorking.IndexOf(afterAnchor);
            if (afterIndex < 0)
            {
                return false;
            }

            insertIndex = afterIndex;
            return true;
        }
    }
}
