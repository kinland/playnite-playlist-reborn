using GongSolutions.Wpf.DragDrop;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Playlist
{
    /// <summary>
    /// Reorders using the on-screen item sequence. Gong's default handler mixes visual insert indices with
    /// <see cref="ObservableCollection{T}.Move(int,int)"/> (source indices), which breaks for rank-descending
    /// and mis-handles removal before insert without adjusting the index.
    /// </summary>
    public sealed class PlaylistListDropHandler : IDropTarget
    {
        private readonly PlaylistViewModel viewModel;
        private readonly DefaultDropHandler defaultHandler = new DefaultDropHandler();

        public PlaylistListDropHandler(PlaylistViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            defaultHandler.DragOver(dropInfo);

            if (dropInfo?.DragInfo == null || !viewModel.IsLastPlayedSortActive)
            {
                return;
            }

            if (!DefaultDropHandler.CanAcceptData(dropInfo))
            {
                return;
            }

            ListCollectionView listView = viewModel.PlaylistGamesView as ListCollectionView;
            if (listView == null)
            {
                return;
            }

            List<Game> visualOrder = listView.Cast<Game>().ToList();
            List<Game> dragged = GetVisibleDraggedItems(dropInfo, visualOrder);
            if (dragged.Count == 0)
            {
                return;
            }

            int originalInsert = GetInsertIndex(dropInfo);
            if (!CanInsertWithinLastPlayedBucket(visualOrder, dragged, originalInsert))
            {
                dropInfo.DropTargetAdorner = null;
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        public void DropHint(IDropHintInfo dropHintInfo)
        {
            defaultHandler.DropHint(dropHintInfo);
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo?.DragInfo == null)
            {
                return;
            }

            if (DefaultDropHandler.ShouldCopyData(dropInfo))
            {
                defaultHandler.Drop(dropInfo);
                return;
            }

            if (!viewModel.IsDragReorderEnabled)
            {
                defaultHandler.Drop(dropInfo);
                return;
            }

            if (!DefaultDropHandler.CanAcceptData(dropInfo))
            {
                return;
            }

            ListCollectionView listView = viewModel.PlaylistGamesView as ListCollectionView;
            if (listView == null)
            {
                defaultHandler.Drop(dropInfo);
                return;
            }

            List<Game> visualOrder = listView.Cast<Game>().ToList();
            List<Game> dragged = GetVisibleDraggedItems(dropInfo, visualOrder);
            if (dragged.Count == 0)
            {
                return;
            }

            int originalInsert = GetInsertIndex(dropInfo);
            if (viewModel.IsLastPlayedSortActive
                && !CanInsertWithinLastPlayedBucket(visualOrder, dragged, originalInsert))
            {
                return;
            }

            ReorderAnchorPreference anchorPreference = ResolveAnchorPreference(dropInfo, viewModel.IsViewRankDescending);
            List<Game> plannedOrder = PlaylistReorderPlanner.ReorderByVisibleInsertion(
                fullOrder: viewModel.PlaylistGames,
                visibleOrderVisual: visualOrder,
                draggedItemsVisual: dragged,
                originalInsertIndexVisual: originalInsert,
                reverseVisualToPersisted: viewModel.IsViewRankDescending,
                anchorPreference: anchorPreference,
                invertAnchorSemantics: viewModel.IsViewLastPlayedDescending);

            ReorderCollectionToMatch(viewModel.PlaylistGames, plannedOrder);

            // Moves on the source list do not always invalidate a sorted ICollectionView; force re-sort / UI sync.
            listView.Refresh();

            DefaultDropHandler.SelectDroppedItems(dropInfo, dragged);

            // Gong can finish the drop before layout/bindings catch up; one deferred refresh fixes a stuck visual order.
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => listView.Refresh()));
        }

        /// <summary>
        /// Keeps Last Played drag moves inside a single display bucket.
        /// </summary>
        private static bool CanInsertWithinLastPlayedBucket(
            IList<Game> visualOrder,
            IList<Game> dragged,
            int originalInsertIndexVisual)
        {
            DateTime nowUtc = DateTime.UtcNow;
            return PlaylistReorderPlanner.CanInsertWithinSameBucket(
                visibleOrderVisual: visualOrder,
                draggedItemsVisual: dragged,
                originalInsertIndexVisual: originalInsertIndexVisual,
                bucketSelector: game => LastPlayedRelativeFormatter.Format(ExtractLastPlayedUtc(game), nowUtc).SortBucket);
        }

        private static List<Game> GetVisibleDraggedItems(IDropInfo dropInfo, IList<Game> visualOrder)
        {
            return DefaultDropHandler.ExtractData(dropInfo.Data)
                .OfType<Game>()
                .Where(game => visualOrder.Contains(game))
                .Distinct()
                .OrderBy(game => visualOrder.IndexOf(game))
                .ToList();
        }

        private static DateTime? ExtractLastPlayedUtc(Game game)
        {
            DateTime? dt = game?.LastActivity;
            if (!dt.HasValue || dt.Value == default)
            {
                return null;
            }

            DateTime value = dt.Value;
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static int GetInsertIndex(IDropInfo dropInfo)
        {
            // Use visual insert index so filtered views map to the same index space
            // as listView.Cast<Game>() order used by the reorder planner.
            int insertIndex = dropInfo.InsertIndex;
            if (dropInfo.VisualTarget is ItemsControl itemsControl)
            {
                if (itemsControl.Items is IEditableCollectionView editableItems)
                {
                    NewItemPlaceholderPosition newItemPlaceholderPosition = editableItems.NewItemPlaceholderPosition;
                    if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning && insertIndex == 0)
                    {
                        ++insertIndex;
                    }
                    else if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtEnd && insertIndex == itemsControl.Items.Count)
                    {
                        --insertIndex;
                    }
                }
            }

            return insertIndex;
        }

        /// <summary>
        /// Maps Gong's before/after target marker to reorder anchor preference, accounting for
        /// whether visual order is reversed relative to persisted playlist order.
        /// </summary>
        private static ReorderAnchorPreference ResolveAnchorPreference(IDropInfo dropInfo, bool reverseVisualToPersisted)
        {
            if (dropInfo == null)
            {
                return ReorderAnchorPreference.Auto;
            }

            // In reversed visual order (rank descending), before/after semantics invert when projected
            // back into persisted ascending playlist order.
            if ((dropInfo.InsertPosition & RelativeInsertPosition.BeforeTargetItem) == RelativeInsertPosition.BeforeTargetItem)
            {
                return reverseVisualToPersisted
                    ? ReorderAnchorPreference.PreferBeforeAnchor
                    : ReorderAnchorPreference.PreferAfterAnchor;
            }

            if ((dropInfo.InsertPosition & RelativeInsertPosition.AfterTargetItem) == RelativeInsertPosition.AfterTargetItem)
            {
                return reverseVisualToPersisted
                    ? ReorderAnchorPreference.PreferAfterAnchor
                    : ReorderAnchorPreference.PreferBeforeAnchor;
            }

            return ReorderAnchorPreference.Auto;
        }

        private static void ReorderCollectionToMatch(ObservableCollection<Game> list, IList<Game> targetOrder)
        {
            for (int i = 0; i < targetOrder.Count; i++)
            {
                Game g = targetOrder[i];
                int cur = list.IndexOf(g);
                if (cur < 0)
                {
                    return;
                }

                if (cur != i)
                {
                    list.Move(cur, i);
                }
            }
        }

#if !NETCOREAPP3_1_OR_GREATER
        public void DragEnter(IDropInfo dropInfo)
        {
        }

        public void DragLeave(IDropInfo dropInfo)
        {
        }
#endif
    }
}
