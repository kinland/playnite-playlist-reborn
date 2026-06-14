namespace Playlist;

internal sealed class PlaylistSettingsTestHarness : IDisposable
{
    private HltbInstallState installState;

    public PlaylistSettingsTestHarness(HltbInstallState initialInstallState = HltbInstallState.InstalledEnabled)
    {
        installState = initialInstallState;
        Host = new Playlist();
        Settings = new PlaylistSettings(Host);
        Playlist.StaticPlayniteApi = null;
        Playlist.StaticSettings = Settings;
        Playlist.StaticPluginInstance = Host;
        HowLongToBeatAddonNavigation.TestInstallStateResolver = _ => installState;
    }

    public Playlist Host { get; }

    public PlaylistSettings Settings { get; }

    public void SetInstallState(HltbInstallState state)
    {
        installState = state;
    }

    public void SetExtensionInstallQueued(bool queued)
    {
        HowLongToBeatAddonNavigation.TestExtensionInstallQueuePendingResolver = () => queued;
    }

    public void SimulateStartup()
    {
        Settings.ExpireSessionOnlyHltbPendingFlags();
        Settings.ExpireAddonPendingIfHltbStillUnavailable();
        Settings.RefreshHowLongToBeatInstallState();
    }

    /// <summary>
    /// Mirrors <see cref="HowLongToBeatAddonNavigation.OpenAddonsPageCore"/> after the Add-ons dialog closes.
    /// </summary>
    public void SimulateCloseBrowseAddonsAfterInstallPrompt(bool restartRequired = false)
    {
        SetExtensionInstallQueued(restartRequired);
        Settings.RefreshHowLongToBeatInstallState();
        Settings.ExpireAddonPendingIfHltbStillUnavailable();
    }

    public void SimulateSettingsDialogClosed()
    {
        Settings.ExpireSessionOnlyHltbPendingFlags();
    }

    public void Dispose()
    {
        HowLongToBeatAddonNavigation.TestInstallStateResolver = null;
        HowLongToBeatAddonNavigation.TestExtensionInstallQueuePendingResolver = null;
        Playlist.StaticPlayniteApi = null;
        Playlist.StaticSettings = null;
        Playlist.StaticPluginInstance = null;
    }
}
