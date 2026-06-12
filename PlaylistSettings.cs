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
        private bool showRankColumn = true;
        private bool showPlaytimeColumn = true;
        private bool showCompletionStatusColumn = true;
        private bool showLastPlayedColumn = true;
        private bool showHowLongToBeatColumn = true;
        private bool enableHowLongToBeatIntegration = true;
        private bool backupEnableHowLongToBeatIntegration = true;

        public bool ShowRankColumn
        {
            get => showRankColumn;
            set => SetValue(ref showRankColumn, value);
        }

        public bool ShowPlaytimeColumn
        {
            get => showPlaytimeColumn;
            set => SetValue(ref showPlaytimeColumn, value);
        }

        public bool ShowCompletionStatusColumn
        {
            get => showCompletionStatusColumn;
            set => SetValue(ref showCompletionStatusColumn, value);
        }

        public bool ShowLastPlayedColumn
        {
            get => showLastPlayedColumn;
            set => SetValue(ref showLastPlayedColumn, value);
        }

        /// <summary>
        /// Column visibility toggle (right-click header menu), like the other <c>Show*Column</c> flags.
        /// </summary>
        public bool ShowHowLongToBeatColumn
        {
            get => showHowLongToBeatColumn;
            set => SetValue(ref showHowLongToBeatColumn, value);
        }

        /// <summary>
        /// Master switch on the settings page: when off, HowLongToBeat data is not read and the column
        /// cannot be shown regardless of <see cref="ShowHowLongToBeatColumn"/>.
        /// </summary>
        public bool EnableHowLongToBeatIntegration
        {
            get => enableHowLongToBeatIntegration;
            set => SetValue(ref enableHowLongToBeatIntegration, value);
        }

        /// <summary>
        /// Bumped when persisted settings need a one-time in-memory migration after deserialization.
        /// </summary>
        public int SettingsSchemaVersion { get; set; }

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
            MigrateSettingsIfNeeded();
        }

        internal void AttachPlugin(Playlist plugin)
        {
            this.plugin = plugin;
            MigrateSettingsIfNeeded();
        }

        private const int CurrentSettingsSchemaVersion = 2;

        /// <summary>
        /// v2: split legacy <c>ShowHowLongToBeatColumn</c> (integration + visibility) into
        /// <see cref="EnableHowLongToBeatIntegration"/> and a visibility-only <see cref="ShowHowLongToBeatColumn"/>.
        /// </summary>
        private void MigrateSettingsIfNeeded()
        {
            if (SettingsSchemaVersion >= CurrentSettingsSchemaVersion)
            {
                return;
            }

            if (SettingsSchemaVersion < 2)
            {
                // Pre-v2, ShowHowLongToBeatColumn gated integration and visibility together.
                enableHowLongToBeatIntegration = showHowLongToBeatColumn;
                if (enableHowLongToBeatIntegration)
                {
                    showHowLongToBeatColumn = true;
                }
            }

            SettingsSchemaVersion = CurrentSettingsSchemaVersion;
            plugin?.PersistSettings();
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
            // Only the HowLongToBeat integration toggle lives on the settings page now; the rest of the
            // column visibility flags are edited via the column header right-click menu (persisted immediately).
            backupEnableHowLongToBeatIntegration = EnableHowLongToBeatIntegration;
        }

        public void CancelEdit()
        {
            EnableHowLongToBeatIntegration = backupEnableHowLongToBeatIntegration;
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
