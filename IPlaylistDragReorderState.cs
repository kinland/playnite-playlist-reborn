namespace Playlist
{
    /// <summary>
    /// Implemented by <see cref="PlaylistViewModel"/> so column reorder UI can ignore row drags
    /// without referencing the full view model from indicator helpers.
    /// </summary>
    internal interface IPlaylistDragReorderState
    {
        bool IsPlaylistDragReorderActive { get; }
    }
}
