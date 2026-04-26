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

            // InsertIndex is relative to the full visual list (still includes dragged rows).
            int originalInsert = GetInsertIndex(dropInfo);
            List<int> dragIndices = dragged.Select(g => visualOrder.IndexOf(g)).ToList();
            int removedBeforeInsert = dragIndices.Count(idx => idx < originalInsert);
            int insertIndex = originalInsert - removedBeforeInsert;

            foreach (Game g in dragged)
            {
                visualOrder.Remove(g);
            }

            insertIndex = Math.Max(0, Math.Min(insertIndex, visualOrder.Count));
            for (int i = 0; i < dragged.Count; i++)
            {
                visualOrder.Insert(insertIndex + i, dragged[i]);
            }

            List<Game> persistedOrder = viewModel.IsViewRankDescending
                ? Enumerable.Reverse(visualOrder).ToList()
                : visualOrder.ToList();

            ReorderCollectionToMatch(viewModel.PlaylistGames, persistedOrder);

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
            int insertIndex = dropInfo.UnfilteredInsertIndex;
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
