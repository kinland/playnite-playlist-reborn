using Xunit;

namespace Playlist.UnitTests;

/// <summary>
/// Playnite keeps plugin settings in an <see cref="System.ComponentModel.IEditableObject"/> session while a
/// settings view is open (<c>BeginEdit</c> on open, <c>CancelEdit</c> on dismiss without save). See
/// <c>Playnite.DesktopApp/ViewModels/PluginSettingsViewModel.cs</c> and
/// <c>Playnite.DesktopApp/PluginSettingsHelper.cs</c>.
/// </summary>
[Collection(nameof(HltbSettingsTestCollection))]
public class QuickAccessSettingsEditSessionTests
{
    [Fact]
    public void BeginEdit_snapshot_stays_stale_after_in_memory_change_until_plugin_persist()
    {
        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.SyncSearchWithMainPanel = true;
        harness.SimulatePlayniteSettingsDialogOpened();
        harness.Settings.SyncSearchWithMainPanel = false;

        Assert.True(
            harness.Settings.EditSessionBackupSyncSearchWithMainPanel,
            "Playnite CancelEdit restores backup fields, not live values.");

        harness.Host.PersistSettings();

        Assert.False(harness.Settings.EditSessionBackupSyncSearchWithMainPanel);
    }

    /// <summary>
    /// Row 1 is the fixed behavior (notify on). Row 2 simulates missing notify and must keep passing.
    /// If <see cref="PlaylistSettings.NotifyPersistedToStorage"/> stops refreshing backups, row 1 fails.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Playnite_settings_flow_cancel_edit_respects_persist_notify(
        bool suppressPersistNotify,
        bool expectedSyncSearchAfterCancel)
    {
        using var harness = new PlaylistSettingsTestHarness();
        try
        {
            PlaylistSettings.TestSuppressPersistedStorageNotify = suppressPersistNotify;
            harness.Settings.SyncSearchWithMainPanel = true;
            harness.SimulatePlayniteSettingsDialogOpened();
            harness.Settings.SyncSearchWithMainPanel = false;

            harness.Host.PersistSettings();
            harness.SimulatePlayniteSettingsDialogClosedWithoutSaving();

            Assert.Equal(expectedSyncSearchAfterCancel, harness.Settings.SyncSearchWithMainPanel);
        }
        finally
        {
            PlaylistSettings.TestSuppressPersistedStorageNotify = false;
        }
    }

    [Fact]
    public void PersistAndApplyOpenView_refreshes_edit_backup_during_edit_session()
    {
        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.SyncSearchWithMainPanel = true;
        harness.SimulatePlayniteSettingsDialogOpened();
        harness.Settings.SyncSearchWithMainPanel = false;

        PlaylistColumnVisibilitySettings.PersistAndApplyOpenView();

        Assert.False(harness.Settings.EditSessionBackupSyncSearchWithMainPanel);
    }

    [Fact]
    public void CancelEdit_without_plugin_persist_restores_begin_edit_snapshot()
    {
        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.SyncSearchWithMainPanel = true;
        harness.SimulatePlayniteSettingsDialogOpened();
        harness.Settings.SyncSearchWithMainPanel = false;

        harness.SimulatePlayniteSettingsDialogClosedWithoutSaving();

        Assert.True(harness.Settings.SyncSearchWithMainPanel);
    }
}
