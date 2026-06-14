namespace Playlist
{
    /// <summary>
    /// Shared rules for when the playlist column reorder drop guide should be active.
    /// </summary>
    internal static class PlaylistColumnReorderDragRules
    {
        internal static bool ShouldShowDropGuide(
            bool leftButtonPressed,
            bool isThumbCaptured,
            bool isPlaylistRowDragActive,
            bool isMouseOverHeaderRow,
            bool hasFloatingColumnHeader)
        {
            if (!leftButtonPressed || isThumbCaptured || isPlaylistRowDragActive)
            {
                return false;
            }

            return hasFloatingColumnHeader && isMouseOverHeaderRow;
        }
    }
}
