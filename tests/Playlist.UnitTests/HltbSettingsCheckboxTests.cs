using Xunit;

namespace Playlist.UnitTests;

[CollectionDefinition(nameof(HltbSettingsTestCollection))]
public class HltbSettingsTestCollection
{
}

/// <summary>
/// Mirrors <c>PlaylistView.BuildHowLongToBeatColumnMenuItem</c> routing for permutation tests.
/// </summary>
internal enum HltbColumnMenuAction
{
    ToggleColumnVisibility,
    OpenBrowseAddonsToInstall,
    OpenInstalledAddonsToEnable,
    OpenPlaylistSettingsToEnableIntegration,
}

internal static class HltbColumnMenuActionPolicy
{
    internal static HltbColumnMenuAction Resolve(HltbInstallState installState, bool integrationEnabled)
    {
        if (installState == HltbInstallState.NotInstalled)
        {
            return HltbColumnMenuAction.OpenBrowseAddonsToInstall;
        }

        if (installState == HltbInstallState.InstalledDisabled)
        {
            return HltbColumnMenuAction.OpenInstalledAddonsToEnable;
        }

        if (!integrationEnabled)
        {
            return HltbColumnMenuAction.OpenPlaylistSettingsToEnableIntegration;
        }

        return HltbColumnMenuAction.ToggleColumnVisibility;
    }

    internal static bool IsColumnVisibleOnScreen(
        HltbInstallState installState,
        bool integrationEnabled,
        bool showHowLongToBeatColumn)
    {
        if (installState != HltbInstallState.InstalledEnabled || !integrationEnabled)
        {
            return false;
        }

        return showHowLongToBeatColumn;
    }

    internal static bool IsColumnMenuChecked(
        HltbInstallState installState,
        bool integrationEnabled,
        bool showHowLongToBeatColumn)
    {
        return Resolve(installState, integrationEnabled) == HltbColumnMenuAction.ToggleColumnVisibility
            && IsColumnVisibleOnScreen(installState, integrationEnabled, showHowLongToBeatColumn);
    }
}

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbIntegrationCheckboxDisplayTests
{
    public static IEnumerable<object[]> InstallStateIntegrationPermutations()
    {
        foreach (HltbInstallState installState in Enum.GetValues(typeof(HltbInstallState)))
        {
            foreach (bool integrationEnabled in new[] { false, true })
            {
                yield return new object[] { installState, integrationEnabled };
            }
        }
    }

    [Theory]
    [MemberData(nameof(InstallStateIntegrationPermutations))]
    public void IntegrationCheckboxDisplay_reflects_availability_and_integration(
        HltbInstallState installState,
        bool integrationEnabled)
    {
        using var harness = new PlaylistSettingsTestHarness(installState);
        harness.Settings.EnableHowLongToBeatIntegration = integrationEnabled;
        harness.Settings.RefreshHowLongToBeatInstallState();

        bool expectedDisplay = installState == HltbInstallState.InstalledEnabled && integrationEnabled;
        Assert.Equal(expectedDisplay, harness.Settings.HowLongToBeatIntegrationCheckboxDisplay);
    }

    [Theory]
    [InlineData(HltbInstallState.NotInstalled)]
    [InlineData(HltbInstallState.InstalledDisabled)]
    public void IntegrationCheckboxDisplay_ignores_set_when_addon_unavailable(HltbInstallState installState)
    {
        using var harness = new PlaylistSettingsTestHarness(installState);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.RefreshHowLongToBeatInstallState();

        harness.Settings.HowLongToBeatIntegrationCheckboxDisplay = true;

        Assert.False(harness.Settings.EnableHowLongToBeatIntegration);
        Assert.False(harness.Settings.HowLongToBeatIntegrationCheckboxDisplay);
    }

    [Fact]
    public void IntegrationCheckboxDisplay_set_updates_integration_when_addon_enabled()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.RefreshHowLongToBeatInstallState();

        harness.Settings.HowLongToBeatIntegrationCheckboxDisplay = false;
        Assert.False(harness.Settings.EnableHowLongToBeatIntegration);

        harness.Settings.HowLongToBeatIntegrationCheckboxDisplay = true;
        Assert.True(harness.Settings.EnableHowLongToBeatIntegration);
    }

    [Theory]
    [MemberData(nameof(InstallStateIntegrationPermutations))]
    public void IsHowLongToBeatAvailable_matches_installed_enabled_state(
        HltbInstallState installState,
        bool integrationEnabled)
    {
        using var harness = new PlaylistSettingsTestHarness(installState);
        harness.Settings.EnableHowLongToBeatIntegration = integrationEnabled;
        harness.Settings.RefreshHowLongToBeatInstallState();

        Assert.Equal(installState == HltbInstallState.InstalledEnabled, harness.Settings.IsHowLongToBeatAvailable);
    }
}

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbColumnMenuCheckboxPermutationTests
{
    public static IEnumerable<object[]> FullColumnMenuPermutations()
    {
        foreach (HltbInstallState installState in Enum.GetValues(typeof(HltbInstallState)))
        {
            foreach (bool integrationEnabled in new[] { false, true })
            {
                foreach (bool showColumn in new[] { false, true })
                {
                    yield return new object[] { installState, integrationEnabled, showColumn };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(FullColumnMenuPermutations))]
    public void ColumnMenuAction_matches_install_and_integration_state(
        HltbInstallState installState,
        bool integrationEnabled,
        bool showColumn)
    {
        HltbColumnMenuAction action = HltbColumnMenuActionPolicy.Resolve(installState, integrationEnabled);

        switch (installState)
        {
            case HltbInstallState.NotInstalled:
                Assert.Equal(HltbColumnMenuAction.OpenBrowseAddonsToInstall, action);
                break;
            case HltbInstallState.InstalledDisabled:
                Assert.Equal(HltbColumnMenuAction.OpenInstalledAddonsToEnable, action);
                break;
            case HltbInstallState.InstalledEnabled when !integrationEnabled:
                Assert.Equal(HltbColumnMenuAction.OpenPlaylistSettingsToEnableIntegration, action);
                break;
            case HltbInstallState.InstalledEnabled:
                Assert.Equal(HltbColumnMenuAction.ToggleColumnVisibility, action);
                break;
        }

        _ = showColumn;
    }

    [Theory]
    [MemberData(nameof(FullColumnMenuPermutations))]
    public void ColumnMenuChecked_only_when_toggle_mode_and_column_visible(
        HltbInstallState installState,
        bool integrationEnabled,
        bool showColumn)
    {
        bool expectedChecked = HltbColumnMenuActionPolicy.IsColumnMenuChecked(
            installState,
            integrationEnabled,
            showColumn);

        if (installState != HltbInstallState.InstalledEnabled || !integrationEnabled)
        {
            Assert.False(expectedChecked);
            return;
        }

        Assert.Equal(showColumn, expectedChecked);
    }

    [Theory]
    [MemberData(nameof(FullColumnMenuPermutations))]
    public void ColumnVisibleOnScreen_requires_enabled_addon_integration_and_show_flag(
        HltbInstallState installState,
        bool integrationEnabled,
        bool showColumn)
    {
        bool visible = HltbColumnMenuActionPolicy.IsColumnVisibleOnScreen(
            installState,
            integrationEnabled,
            showColumn);

        bool expected = installState == HltbInstallState.InstalledEnabled
            && integrationEnabled
            && showColumn;
        Assert.Equal(expected, visible);
    }
}

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbAddonPendingIntegrationTests
{
    [Theory]
    [InlineData(HltbInstallState.InstalledEnabled, true, true)]
    [InlineData(HltbInstallState.InstalledDisabled, false, false)]
    [InlineData(HltbInstallState.NotInstalled, false, false)]
    public void PendingFromPlaylistPrompt_applies_integration_only_when_addon_becomes_enabled(
        HltbInstallState installState,
        bool expectApplied,
        bool expectIntegrationOn)
    {
        using var harness = new PlaylistSettingsTestHarness(installState);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.RefreshHowLongToBeatInstallState();

        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();
        Assert.True(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);

        bool applied = harness.Settings.TryApplyPendingIntegrationEnableFromPlaylistPrompt();

        Assert.Equal(expectApplied, applied);
        Assert.Equal(expectIntegrationOn, harness.Settings.EnableHowLongToBeatIntegration);
        Assert.Equal(expectIntegrationOn, harness.Settings.ShowHowLongToBeatColumn);
        Assert.Equal(!expectApplied, harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Theory]
    [InlineData(HltbInstallState.InstalledDisabled)]
    [InlineData(HltbInstallState.NotInstalled)]
    public void PendingFromPlaylistPrompt_expires_when_addon_still_unavailable(HltbInstallState installState)
    {
        using var harness = new PlaylistSettingsTestHarness(installState);
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();

        harness.Settings.ExpireAddonPendingIfHltbStillUnavailable();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Fact]
    public void PendingFromPlaylistPrompt_survives_expire_when_addon_enabled()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();

        harness.Settings.ExpireAddonPendingIfHltbStillUnavailable();

        Assert.True(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Theory]
    [InlineData(HltbInstallState.InstalledDisabled)]
    [InlineData(HltbInstallState.NotInstalled)]
    public void SettingsEndEdit_clears_addon_pending_when_hltb_still_unavailable(HltbInstallState installState)
    {
        using var harness = new PlaylistSettingsTestHarness(installState);
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();

        harness.Settings.EndEdit();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Fact]
    public void SettingsCancelEdit_clears_addon_pending_when_hltb_still_unavailable()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();

        harness.Settings.CancelEdit();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Fact]
    public void Startup_clears_stale_addon_pending_when_hltb_not_enabled()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt = true;

        harness.SimulateStartup();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Fact]
    public void Startup_applies_addon_pending_when_hltb_enabled_after_restart()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt = true;

        harness.SimulateStartup();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
        Assert.True(harness.Settings.EnableHowLongToBeatIntegration);
        Assert.True(harness.Settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void RefreshAfterEnablingAddon_applies_pending_without_user_enabling_integration_manually()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledDisabled);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();

        harness.SetInstallState(HltbInstallState.InstalledEnabled);
        harness.Settings.RefreshHowLongToBeatInstallState();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
        Assert.True(harness.Settings.EnableHowLongToBeatIntegration);
        Assert.True(harness.Settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void EnablingAddonElsewhere_without_playlist_prompt_does_not_auto_enable_integration()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledDisabled);
        harness.Settings.EnableHowLongToBeatIntegration = false;

        harness.SetInstallState(HltbInstallState.InstalledEnabled);
        harness.Settings.RefreshHowLongToBeatInstallState();

        Assert.False(harness.Settings.EnableHowLongToBeatIntegration);
    }

    /// <summary>
    /// User disabled integration, uninstalled HLTB, toggled the column (browse install prompt),
    /// installed from Add-ons, then restarted Playnite. Pending must survive the pre-restart dialog close.
    /// </summary>
    [Fact]
    public void InstallViaBrowsePrompt_then_restart_enables_integration_and_column()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.RefreshHowLongToBeatInstallState();

        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();
        harness.SimulateCloseBrowseAddonsAfterInstallPrompt(restartRequired: true);

        harness.SetExtensionInstallQueued(queued: false);
        harness.SetInstallState(HltbInstallState.InstalledEnabled);
        harness.SimulateStartup();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
        Assert.True(harness.Settings.EnableHowLongToBeatIntegration);
        Assert.True(harness.Settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void InstallViaBrowsePrompt_cancelled_without_restart_clears_pending()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();

        harness.SimulateCloseBrowseAddonsAfterInstallPrompt(restartRequired: false);

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Fact]
    public void PendingFromPlaylistPrompt_survives_expire_when_extension_install_queued()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();
        harness.SetExtensionInstallQueued(queued: true);

        harness.Settings.ExpireAddonPendingIfHltbStillUnavailable();

        Assert.True(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Fact]
    public void SettingsEndEdit_keeps_addon_pending_when_install_queued_for_restart()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();
        harness.SetExtensionInstallQueued(queued: true);

        harness.Settings.EndEdit();

        Assert.True(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    [Fact]
    public void SettingsCancelEdit_keeps_addon_pending_when_install_queued_for_restart()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();
        harness.SetExtensionInstallQueued(queued: true);

        harness.Settings.CancelEdit();

        Assert.True(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    /// <summary>
    /// Installed HLTB from browse prompt, chose restart later, saved unrelated Playlist settings,
    /// then restarted Playnite — integration and column should still auto-enable.
    /// </summary>
    [Fact]
    public void InstallViaBrowsePrompt_deferred_restart_settings_save_then_restart_enables_integration()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.RefreshHowLongToBeatInstallState();

        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();
        harness.SimulateCloseBrowseAddonsAfterInstallPrompt(restartRequired: true);

        harness.Settings.BeginEdit();
        harness.Settings.EndEdit();

        Assert.True(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);

        harness.SetExtensionInstallQueued(queued: false);
        harness.SetInstallState(HltbInstallState.InstalledEnabled);
        harness.SimulateStartup();

        Assert.False(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
        Assert.True(harness.Settings.EnableHowLongToBeatIntegration);
        Assert.True(harness.Settings.ShowHowLongToBeatColumn);
    }
}

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbHeaderMenuPendingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void PendingFromHeaderMenu_shows_column_on_save_when_integration_enabled(
        bool enableIntegrationOnSave,
        bool expectColumnVisible)
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.RefreshHowLongToBeatInstallState();

        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingShowHowLongToBeatColumnFromHeaderMenu();
        harness.Settings.EnableHowLongToBeatIntegration = enableIntegrationOnSave;

        harness.Settings.EndEdit();

        Assert.Equal(expectColumnVisible, harness.Settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void PendingFromHeaderMenu_does_not_show_column_when_integration_stays_off()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingShowHowLongToBeatColumnFromHeaderMenu();

        harness.Settings.EndEdit();

        Assert.False(harness.Settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void PendingFromHeaderMenu_cleared_on_settings_cancel()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingShowHowLongToBeatColumnFromHeaderMenu();
        harness.Settings.EnableHowLongToBeatIntegration = true;

        harness.Settings.CancelEdit();

        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.Settings.RefreshHowLongToBeatInstallState();
        harness.Settings.TryApplyPendingShowHowLongToBeatColumnFromHeaderMenu();

        Assert.False(harness.Settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void SessionPending_expires_when_settings_dialog_closes()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingShowHowLongToBeatColumnFromHeaderMenu();
        harness.Settings.EnableHowLongToBeatIntegration = true;

        harness.SimulateSettingsDialogClosed();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.Settings.RefreshHowLongToBeatInstallState();
        harness.Settings.TryApplyPendingShowHowLongToBeatColumnFromHeaderMenu();

        Assert.False(harness.Settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void SessionPending_expires_on_startup()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.ShowHowLongToBeatColumn = false;
        harness.Settings.MarkPendingShowHowLongToBeatColumnFromHeaderMenu();

        harness.SimulateStartup();

        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.Settings.TryApplyPendingShowHowLongToBeatColumnFromHeaderMenu();
        Assert.False(harness.Settings.ShowHowLongToBeatColumn);
    }
}

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbSettingsDialogFlowTests
{
    [Theory]
    [InlineData(HltbInstallState.NotInstalled)]
    [InlineData(HltbInstallState.InstalledDisabled)]
    [InlineData(HltbInstallState.InstalledEnabled)]
    public void BeginEdit_refreshes_install_state_flags(HltbInstallState installState)
    {
        using var harness = new PlaylistSettingsTestHarness(installState);
        harness.Settings.BeginEdit();

        Assert.Equal(installState, harness.Settings.HowLongToBeatInstallState);
        Assert.Equal(installState == HltbInstallState.InstalledEnabled, harness.Settings.IsHowLongToBeatAvailable);
    }

    [Fact]
    public void CancelEdit_restores_integration_backup_and_clears_header_pending()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.InstalledEnabled);
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.Settings.BeginEdit();
        harness.Settings.MarkPendingShowHowLongToBeatColumnFromHeaderMenu();
        harness.Settings.EnableHowLongToBeatIntegration = false;

        harness.Settings.CancelEdit();

        Assert.True(harness.Settings.EnableHowLongToBeatIntegration);
    }
}
