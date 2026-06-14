using System.ComponentModel;

namespace Playlist
{
    /// <summary>
    /// Minimal surface used by <see cref="MainSearchSync"/> for playlist search text.
    /// </summary>
    internal interface IPlaylistSearchSyncTarget : INotifyPropertyChanged
    {
        string SearchQuery { get; set; }
    }
}
