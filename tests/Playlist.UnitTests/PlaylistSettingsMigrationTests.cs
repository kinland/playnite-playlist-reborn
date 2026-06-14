using Xunit;

namespace Playlist.UnitTests;

public class PlaylistSettingsMigrationTests
{
    [Fact]
    public void AttachPlugin_migrates_legacy_show_column_on_to_integration_and_visibility()
    {
        var host = new Playlist();
        var settings = new PlaylistSettings
        {
            SettingsSchemaVersion = 0,
            ShowHowLongToBeatColumn = true,
        };

        settings.AttachPlugin(host);

        Assert.Equal(2, settings.SettingsSchemaVersion);
        Assert.True(settings.EnableHowLongToBeatIntegration);
        Assert.True(settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void AttachPlugin_migrates_legacy_show_column_off_to_both_flags_false()
    {
        var host = new Playlist();
        var settings = new PlaylistSettings
        {
            SettingsSchemaVersion = 0,
            ShowHowLongToBeatColumn = false,
        };

        settings.AttachPlugin(host);

        Assert.Equal(2, settings.SettingsSchemaVersion);
        Assert.False(settings.EnableHowLongToBeatIntegration);
        Assert.False(settings.ShowHowLongToBeatColumn);
    }

    [Fact]
    public void AttachPlugin_skips_migration_when_schema_already_current()
    {
        var host = new Playlist();
        var settings = new PlaylistSettings
        {
            SettingsSchemaVersion = 2,
            ShowHowLongToBeatColumn = false,
            EnableHowLongToBeatIntegration = true,
        };

        settings.AttachPlugin(host);

        Assert.Equal(2, settings.SettingsSchemaVersion);
        Assert.True(settings.EnableHowLongToBeatIntegration);
        Assert.False(settings.ShowHowLongToBeatColumn);
    }
}
