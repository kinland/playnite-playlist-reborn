using GongSolutions.Wpf.DragDrop;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

            List<Game> dragged = DefaultDropHandler.ExtractData(dropInfo.Data).OfType<Game>().ToList();
            if (dragged.Count == 0)
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
            dragged = dragged.Where(g => visualOrder.Contains(g)).Distinct().ToList();
            if (dragged.Count == 0)
            {
                return;
            }

            dragged = dragged.OrderBy(g => visualOrder.IndexOf(g)).ToList();

            int originalInsert = GetInsertIndex(dropInfo);
            ReorderAnchorPreference anchorPreference = ResolveAnchorPreference(dropInfo, viewModel.IsViewRankDescending);
            List<Game> plannedOrder = PlaylistReorderPlanner.ReorderByVisibleInsertion(
                fullOrder: viewModel.PlaylistGames,
                visibleOrderVisual: visualOrder,
                draggedItemsVisual: dragged,
                originalInsertIndexVisual: originalInsert,
                reverseVisualToPersisted: viewModel.IsViewRankDescending,
                anchorPreference: anchorPreference);

            ReorderCollectionToMatch(viewModel.PlaylistGames, plannedOrder);

            // Moves on the source list do not always invalidate a sorted ICollectionView; force re-sort / UI sync.
            listView.Refresh();

            DefaultDropHandler.SelectDroppedItems(dropInfo, dragged);

            // Gong can finish the drop before layout/bindings catch up; one deferred refresh fixes a stuck visual order.
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => listView.Refresh()));
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
