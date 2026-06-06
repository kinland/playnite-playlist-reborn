using Playnite.SDK;
using System.Collections.Generic;
using System.ComponentModel;

namespace Playlist
{
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
