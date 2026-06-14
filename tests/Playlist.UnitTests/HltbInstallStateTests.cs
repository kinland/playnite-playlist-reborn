using Xunit;

namespace Playlist.UnitTests;

public class HltbInstallStateTests
{
    [Fact]
    public void GetInstallState_without_api_returns_not_installed()
    {
        HowLongToBeatAddonNavigation.TestInstallStateResolver = null;
        Assert.Equal(HltbInstallState.NotInstalled, HowLongToBeatAddonNavigation.GetInstallState(null));
    }

    [Fact]
    public void IsPluginEnabledInPlaynite_matches_installed_enabled_state()
    {
        HowLongToBeatAddonNavigation.TestInstallStateResolver = _ => HltbInstallState.InstalledEnabled;
        Assert.True(HowLongToBeatAddonNavigation.IsPluginEnabledInPlaynite(null));

        HowLongToBeatAddonNavigation.TestInstallStateResolver = _ => HltbInstallState.InstalledDisabled;
        Assert.False(HowLongToBeatAddonNavigation.IsPluginEnabledInPlaynite(null));
    }
}
