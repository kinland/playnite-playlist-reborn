using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbInstallStateTests
{
    [Fact]
    public void GetInstallState_without_api_returns_not_installed()
    {
        try
        {
            HowLongToBeatAddonNavigation.TestInstallStateResolver = null;
            Assert.Equal(HltbInstallState.NotInstalled, HowLongToBeatAddonNavigation.GetInstallState(null));
        }
        finally
        {
            HowLongToBeatAddonNavigation.TestInstallStateResolver = null;
        }
    }

    [Fact]
    public void IsPluginEnabledInPlaynite_matches_installed_enabled_state()
    {
        try
        {
            HowLongToBeatAddonNavigation.TestInstallStateResolver = _ => HltbInstallState.InstalledEnabled;
            Assert.True(HowLongToBeatAddonNavigation.IsPluginEnabledInPlaynite(null));

            HowLongToBeatAddonNavigation.TestInstallStateResolver = _ => HltbInstallState.InstalledDisabled;
            Assert.False(HowLongToBeatAddonNavigation.IsPluginEnabledInPlaynite(null));
        }
        finally
        {
            HowLongToBeatAddonNavigation.TestInstallStateResolver = null;
        }
    }
}
