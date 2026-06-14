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
        private bool showLastActivityColumn = false;
        private bool showHowLongToBeatColumn = true;
        private bool enableHowLongToBeatIntegration = true;
        private bool backupEnableHowLongToBeatIntegration = true;
        private bool syncSearchWithMainPanel = true;
        private bool backupSyncSearchWithMainPanel = true;
        private bool pendingEnableHowLongToBeatIntegrationFromPlaylistPrompt;
        private bool pendingShowHowLongToBeatColumnFromHeaderMenu;
        private bool isHowLongToBeatAvailable;
        private HltbInstallState howLongToBeatInstallState = HltbInstallState.NotInstalled;

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
        /// Last Activity uses <see cref="Playnite.SDK.Models.Game.Modified"/>, which also reflects installs.
        /// Hidden by default so it does not widen the default layout.
        /// </summary>
        public bool ShowLastActivityColumn
        {
            get => showLastActivityColumn;
            set => SetValue(ref showLastActivityColumn, value);
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
            set
            {
                SetValue(ref enableHowLongToBeatIntegration, value);
                OnPropertyChanged(nameof(HowLongToBeatIntegrationCheckboxDisplay));
            }
        }

        /// <summary>
        /// When enabled, the Playlist search box stays in sync with Playnite's main search / filter fields.
        /// </summary>
        public bool SyncSearchWithMainPanel
        {
            get => syncSearchWithMainPanel;
            set => SetValue(ref syncSearchWithMainPanel, value);
        }

        /// <summary>
        /// Bumped when persisted settings need a one-time in-memory migration after deserialization.
        /// </summary>
        public int SettingsSchemaVersion { get; set; }

        public string ActiveSortColumnKey { get; set; } = string.Empty;

        public ListSortDirection ActiveSortDirection { get; set; } = ListSortDirection.Ascending;

        public List<PlaylistColumnLayoutState> ColumnLayouts { get; set; } = new List<PlaylistColumnLayoutState>();

        /// <summary>
        /// Persisted only so a post-enable Playnite restart can still apply integration.
        /// Cleared when Add-ons or settings close without HLTB becoming available, after apply,
        /// or on startup when HLTB is still unavailable (crash / stale flag).
        /// </summary>
        public bool PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt
        {
            get => pendingEnableHowLongToBeatIntegrationFromPlaylistPrompt;
            set => SetValue(ref pendingEnableHowLongToBeatIntegrationFromPlaylistPrompt, value);
        }

        public bool IsHowLongToBeatAvailable => isHowLongToBeatAvailable;

        public HltbInstallState HowLongToBeatInstallState => howLongToBeatInstallState;

        public bool IsHowLongToBeatInstalledDisabled =>
            howLongToBeatInstallState == HltbInstallState.InstalledDisabled;

        public bool IsHowLongToBeatNotInstalled =>
            howLongToBeatInstallState == HltbInstallState.NotInstalled;

        /// <summary>
        /// Checkbox display state: unchecked while the HLTB add-on is unavailable; otherwise mirrors
        /// <see cref="EnableHowLongToBeatIntegration"/>. Avoids style-trigger overrides that break WPF binding.
        /// </summary>
        public bool HowLongToBeatIntegrationCheckboxDisplay
        {
            get => isHowLongToBeatAvailable && enableHowLongToBeatIntegration;
            set
            {
                if (!isHowLongToBeatAvailable)
                {
                    return;
                }

                EnableHowLongToBeatIntegration = value;
            }
        }

        /// <summary>
        /// Re-evaluates HLTB add-on install state for bindings and applies a pending integration enable
        /// when the user previously opened Add-ons from Playlist's disabled HLTB UI.
        /// </summary>
        internal void RefreshHowLongToBeatInstallState()
        {
            howLongToBeatInstallState = HowLongToBeatAddonNavigation.GetInstallState(Playlist.StaticPlayniteApi);
            isHowLongToBeatAvailable = howLongToBeatInstallState == HltbInstallState.InstalledEnabled;
            TryApplyPendingIntegrationEnableFromPlaylistPrompt();
            OnPropertyChanged(nameof(IsHowLongToBeatAvailable));
            OnPropertyChanged(nameof(HowLongToBeatInstallState));
            OnPropertyChanged(nameof(IsHowLongToBeatInstalledDisabled));
            OnPropertyChanged(nameof(IsHowLongToBeatNotInstalled));
            OnPropertyChanged(nameof(HowLongToBeatIntegrationCheckboxDisplay));
        }

        internal void MarkPendingIntegrationEnableFromPlaylistPrompt()
        {
            PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt = true;
            plugin?.PersistSettings();
        }

        internal void ClearPendingIntegrationEnableFromPlaylistPrompt()
        {
            if (!pendingEnableHowLongToBeatIntegrationFromPlaylistPrompt)
            {
                return;
            }

            PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt = false;
            plugin?.PersistSettings();
        }

        /// <returns>True when pending integration was applied.</returns>
        internal bool TryApplyPendingIntegrationEnableFromPlaylistPrompt()
        {
            if (!pendingEnableHowLongToBeatIntegrationFromPlaylistPrompt
                || howLongToBeatInstallState != HltbInstallState.InstalledEnabled)
            {
                return false;
            }

            pendingEnableHowLongToBeatIntegrationFromPlaylistPrompt = false;
            EnableHowLongToBeatIntegration = true;
            ShowHowLongToBeatColumn = true;
            plugin?.PersistSettings();
            return true;
        }

        internal void MarkPendingShowHowLongToBeatColumnFromHeaderMenu()
        {
            pendingShowHowLongToBeatColumnFromHeaderMenu = true;
        }

        internal void ClearPendingShowHowLongToBeatColumnFromHeaderMenu()
        {
            pendingShowHowLongToBeatColumnFromHeaderMenu = false;
        }

        /// <summary>
        /// Session-only pending from the column header menu; cleared when settings dialogs close
        /// or on startup (no restart in this flow).
        /// </summary>
        internal void ExpireSessionOnlyHltbPendingFlags()
        {
            pendingShowHowLongToBeatColumnFromHeaderMenu = false;
        }

        /// <summary>
        /// Clears persisted add-on pending intent when HLTB is still not enabled after a prompt flow.
        /// Skips when a HowLongToBeat extension install is queued for restart (user chose restart later).
        /// </summary>
        internal void ExpireAddonPendingIfHltbStillUnavailable()
        {
            if (!pendingEnableHowLongToBeatIntegrationFromPlaylistPrompt)
            {
                return;
            }

            if (HowLongToBeatAddonNavigation.GetInstallState(Playlist.StaticPlayniteApi)
                == HltbInstallState.InstalledEnabled)
            {
                return;
            }

            if (HowLongToBeatAddonNavigation.IsExtensionInstallQueuedForRestart())
            {
                return;
            }

            ClearPendingIntegrationEnableFromPlaylistPrompt();
        }

        internal void TryApplyPendingShowHowLongToBeatColumnFromHeaderMenu()
        {
            if (!pendingShowHowLongToBeatColumnFromHeaderMenu)
            {
                return;
            }

            pendingShowHowLongToBeatColumnFromHeaderMenu = false;
            if (!enableHowLongToBeatIntegration || !isHowLongToBeatAvailable)
            {
                return;
            }

            ShowHowLongToBeatColumn = true;
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

        /// <summary>Persists active sort and column layout snapshot to plugin settings.</summary>
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
            RefreshHowLongToBeatInstallState();
            backupEnableHowLongToBeatIntegration = EnableHowLongToBeatIntegration;
            backupSyncSearchWithMainPanel = SyncSearchWithMainPanel;
        }

        public void CancelEdit()
        {
            ClearPendingShowHowLongToBeatColumnFromHeaderMenu();
            EnableHowLongToBeatIntegration = backupEnableHowLongToBeatIntegration;
            SyncSearchWithMainPanel = backupSyncSearchWithMainPanel;
            ExpireAddonPendingIfHltbStillUnavailable();
        }

        public void EndEdit()
        {
            RefreshHowLongToBeatInstallState();
            TryApplyPendingShowHowLongToBeatColumnFromHeaderMenu();
            ExpireAddonPendingIfHltbStillUnavailable();
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
