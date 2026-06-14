using System;
using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    /// <summary>
    /// Pure helpers for persisting GridView column order and widths (no WPF types).
    /// </summary>
    internal static class PlaylistColumnLayoutPersistence
    {
        internal const double CollapsedColumnWidthThreshold = 0.5;

        /// <summary>
        /// Hidden columns keep their prior <see cref="PlaylistColumnLayoutState.DisplayIndex"/> slots
        /// so re-showing restores position instead of appending at the end.
        /// </summary>
        internal static List<string> MergeVisibleAndHiddenColumnOrder(
            IReadOnlyList<string> visibleKeysInOrder,
            IReadOnlyList<string> hiddenKeys,
            IReadOnlyDictionary<string, PlaylistColumnLayoutState> previousByKey,
            int totalColumns)
        {
            var hiddenSlotAssignments = new SortedDictionary<int, string>();
            var unassignedHidden = new List<string>();

            foreach (string key in hiddenKeys)
            {
                int slot = previousByKey.TryGetValue(key, out PlaylistColumnLayoutState layout)
                    ? layout.DisplayIndex
                    : int.MaxValue;
                if (slot >= 0 && slot < totalColumns && !hiddenSlotAssignments.ContainsKey(slot))
                {
                    hiddenSlotAssignments[slot] = key;
                }
                else
                {
                    unassignedHidden.Add(key);
                }
            }

            var result = new List<string>(totalColumns);
            int visibleIndex = 0;
            int hiddenFallbackIndex = 0;

            for (int slot = 0; slot < totalColumns; slot++)
            {
                if (hiddenSlotAssignments.TryGetValue(slot, out string hiddenKey))
                {
                    result.Add(hiddenKey);
                    continue;
                }

                if (visibleIndex < visibleKeysInOrder.Count)
                {
                    result.Add(visibleKeysInOrder[visibleIndex++]);
                    continue;
                }

                while (hiddenFallbackIndex < unassignedHidden.Count)
                {
                    string key = unassignedHidden[hiddenFallbackIndex++];
                    if (!result.Contains(key))
                    {
                        result.Add(key);
                        break;
                    }
                }
            }

            while (visibleIndex < visibleKeysInOrder.Count)
            {
                result.Add(visibleKeysInOrder[visibleIndex++]);
            }

            foreach (string key in hiddenKeys)
            {
                if (!result.Contains(key))
                {
                    result.Add(key);
                }
            }

            return result;
        }

        internal static double? TryGetPersistedColumnWidth(
            IEnumerable<PlaylistColumnLayoutState> columnLayouts,
            string columnKey)
        {
            if (columnLayouts == null || string.IsNullOrEmpty(columnKey))
            {
                return null;
            }

            PlaylistColumnLayoutState layout = columnLayouts
                .FirstOrDefault(item => item != null && item.Key == columnKey);
            if (layout == null
                || double.IsNaN(layout.Width)
                || layout.Width <= CollapsedColumnWidthThreshold)
            {
                return null;
            }

            return layout.Width;
        }

        /// <summary>
        /// Maps a live column width to the value stored in settings (ignores collapsed on-screen widths).
        /// </summary>
        internal static double GetWidthForLayoutPersistence(
            string columnKey,
            double currentWidth,
            IEnumerable<PlaylistColumnLayoutState> columnLayouts)
        {
            if (columnKey == PlaylistColumnWidthLayout.IconColumnKey)
            {
                return PlaylistGridViewLayout.IconColumnWidth;
            }

            if (!double.IsNaN(currentWidth) && currentWidth > CollapsedColumnWidthThreshold)
            {
                return currentWidth;
            }

            return TryGetPersistedColumnWidth(columnLayouts, columnKey) ?? currentWidth;
        }

        /// <summary>
        /// Resolves the width written for one column when persisting layout state.
        /// </summary>
        /// <param name="persistColumnWidths">
        /// True only after a user column gripper drag; otherwise prior saved widths are reused.
        /// </param>
        internal static double ResolvePersistedWidthForColumnKey(
            string columnKey,
            double? visibleColumnWidth,
            bool isVisibleInGrid,
            bool persistColumnWidths,
            IReadOnlyDictionary<string, PlaylistColumnLayoutState> previousByKey,
            IEnumerable<PlaylistColumnLayoutState> columnLayouts)
        {
            if (columnKey == PlaylistColumnWidthLayout.IconColumnKey)
            {
                return PlaylistGridViewLayout.IconColumnWidth;
            }

            if (!persistColumnWidths)
            {
                if (previousByKey.TryGetValue(columnKey, out PlaylistColumnLayoutState previousLayout)
                    && !double.IsNaN(previousLayout.Width)
                    && previousLayout.Width > CollapsedColumnWidthThreshold)
                {
                    return previousLayout.Width;
                }

                double defaultWidth = PlaylistColumnWidthLayout.GetMinimumWidth(columnKey);
                return double.IsNaN(defaultWidth) ? 0 : defaultWidth;
            }

            if (isVisibleInGrid && visibleColumnWidth.HasValue)
            {
                return GetWidthForLayoutPersistence(columnKey, visibleColumnWidth.Value, columnLayouts);
            }

            if (previousByKey.TryGetValue(columnKey, out PlaylistColumnLayoutState hiddenLayout)
                && !double.IsNaN(hiddenLayout.Width)
                && hiddenLayout.Width > CollapsedColumnWidthThreshold)
            {
                return hiddenLayout.Width;
            }

            if (visibleColumnWidth.HasValue
                && !double.IsNaN(visibleColumnWidth.Value)
                && visibleColumnWidth.Value > CollapsedColumnWidthThreshold)
            {
                return visibleColumnWidth.Value;
            }

            double restoredWidth = TryGetPersistedColumnWidth(columnLayouts, columnKey)
                ?? PlaylistColumnWidthLayout.GetMinimumWidth(columnKey);
            return double.IsNaN(restoredWidth) ? 0 : restoredWidth;
        }
    }
}
