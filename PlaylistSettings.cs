using Playnite.SDK;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Playlist
{
    public sealed class PlaylistColumnLayoutState
    {
        public string Key { get; set; } = string.Empty;
        public int DisplayIndex { get; set; }
        public double Width { get; set; }
    }

    public class PlaylistSettings : ObservableObject, ISettings, IEditableObject
    {
        private Playlist plugin;
        private bool showLastPlayedColumn = true;
        private bool showHowLongToBeatColumn = true;
        private bool backupShowLastPlayedColumn = true;
        private bool backupShowHowLongToBeatColumn = true;

        public bool ShowLastPlayedColumn
        {
            get => showLastPlayedColumn;
            set => SetValue(ref showLastPlayedColumn, value);
        }

        public bool ShowHowLongToBeatColumn
        {
            get => showHowLongToBeatColumn;
            set => SetValue(ref showHowLongToBeatColumn, value);
        }

        public string ActiveSortColumnKey { get; set; } = string.Empty;

        public ListSortDirection ActiveSortDirection { get; set; } = ListSortDirection.Ascending;

        public List<PlaylistColumnLayoutState> ColumnLayouts { get; set; } = new List<PlaylistColumnLayoutState>();

        public bool IsHowLongToBeatAvailable
        {
            get
            {
                if (Playlist.StaticPlayniteApi == null)
                {
                    return false;
                }

                return HowLongToBeatCache.IsAvailable(Playlist.StaticPlayniteApi);
            }
        }

        public PlaylistSettings()
        {
        }

        public PlaylistSettings(Playlist plugin)
        {
            this.plugin = plugin;
        }

        internal void AttachPlugin(Playlist plugin)
        {
            this.plugin = plugin;
        }

        internal void SaveRuntimeState(
            string activeSortColumnKey,
            ListSortDirection activeSortDirection,
            IEnumerable<PlaylistColumnLayoutState> columnLayouts)
        {
            ActiveSortColumnKey = activeSortColumnKey ?? string.Empty;
            ActiveSortDirection = activeSortDirection;
            ColumnLayouts = columnLayouts?
                .Where(layout => layout != null && !string.IsNullOrWhiteSpace(layout.Key))
                .Select(layout => new PlaylistColumnLayoutState
                {
                    Key = layout.Key,
                    DisplayIndex = layout.DisplayIndex,
                    Width = layout.Width,
                })
                .ToList()
                ?? new List<PlaylistColumnLayoutState>();

            plugin?.PersistSettings();
        }

        public void BeginEdit()
        {
            backupShowLastPlayedColumn = ShowLastPlayedColumn;
            backupShowHowLongToBeatColumn = ShowHowLongToBeatColumn;
        }

        public void CancelEdit()
        {
            ShowLastPlayedColumn = backupShowLastPlayedColumn;
            ShowHowLongToBeatColumn = backupShowHowLongToBeatColumn;
        }

        public void EndEdit()
        {
            plugin?.SaveSettings(this);
            plugin?.ApplySettingsToOpenView();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
