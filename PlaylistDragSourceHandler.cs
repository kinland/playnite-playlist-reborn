using GongSolutions.Wpf.DragDrop;
using System.Windows;

namespace Playlist
{
    /// <summary>
    /// Wraps Gong's default drag source so we can pause HowLongToBeat plugin UI updates for the duration
    /// of a reorder drag (see PR #4 discussion — heavy per-row plugin controls otherwise slow dragging).
    /// </summary>
    public sealed class PlaylistDragSourceHandler : IDragSource
    {
        private readonly PlaylistViewModel viewModel;
        private readonly DefaultDragHandler inner = new DefaultDragHandler();

        public PlaylistDragSourceHandler(PlaylistViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public void StartDrag(IDragInfo dragInfo)
        {
            inner.StartDrag(dragInfo);
            if (viewModel.IsDragReorderEnabled)
            {
                viewModel.SetPlaylistDragReorderActive(true);
            }
        }

        public bool CanStartDrag(IDragInfo dragInfo) => inner.CanStartDrag(dragInfo);

        public void Dropped(IDropInfo dropInfo)
        {
            try
            {
                inner.Dropped(dropInfo);
            }
            finally
            {
                viewModel.SetPlaylistDragReorderActive(false);
            }
        }

        public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo)
        {
            try
            {
                inner.DragDropOperationFinished(operationResult, dragInfo);
            }
            finally
            {
                viewModel.SetPlaylistDragReorderActive(false);
            }
        }

        public void DragCancelled()
        {
            try
            {
                inner.DragCancelled();
            }
            finally
            {
                viewModel.SetPlaylistDragReorderActive(false);
            }
        }

        public bool TryCatchOccurredException(System.Exception exception)
        {
            try
            {
                return inner.TryCatchOccurredException(exception);
            }
            finally
            {
                viewModel.SetPlaylistDragReorderActive(false);
            }
        }
    }
}
