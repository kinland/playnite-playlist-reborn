using Playlist;
using Playlist.UnitTests.TestStubs;
using Xunit;

namespace Playlist.UnitTests;

public class MainSearchSyncLifecycleTests
{
    [Fact]
    public void OnViewOpened_pulls_main_filter_name_into_playlist_search()
    {
        var bridge = new FakeMainFilterPanelBridge
        {
            Current = new Playnite.SDK.Models.FilterPresetSettings { Name = "Alan Wake" },
        };
        var sync = CreateSync(bridge, enabled: true);
        var target = new PlaylistSearchSyncTarget();
        sync.Attach(target);

        sync.OnViewOpened();

        Assert.Equal("Alan Wake", target.SearchQuery);
    }

    [Fact]
    public void OnViewClosed_pushes_playlist_search_to_main_filter()
    {
        var bridge = new FakeMainFilterPanelBridge
        {
            Current = new Playnite.SDK.Models.FilterPresetSettings(),
        };
        var sync = CreateSync(bridge, enabled: true);
        var target = new PlaylistSearchSyncTarget { SearchQuery = "tag:fps" };
        sync.Attach(target);

        sync.OnViewClosed();

        Assert.Equal(1, bridge.ApplyCount);
        Assert.NotNull(bridge.LastApplied.Tag);
        Assert.Equal("fps", bridge.LastApplied.Tag.Text);
    }

    [Fact]
    public void ApplySettingsChange_when_disabled_does_not_pull_on_view_open()
    {
        var bridge = new FakeMainFilterPanelBridge
        {
            Current = new Playnite.SDK.Models.FilterPresetSettings { Name = "ShouldNotApply" },
        };
        var sync = CreateSync(bridge, enabled: false);
        var target = new PlaylistSearchSyncTarget();
        sync.Attach(target);

        sync.ApplySettingsChange(playlistViewActive: true);

        Assert.Equal(string.Empty, target.SearchQuery);
    }

    [Fact]
    public void Main_filter_change_updates_playlist_search_while_view_is_open()
    {
        var bridge = new FakeMainFilterPanelBridge
        {
            Current = new Playnite.SDK.Models.FilterPresetSettings { Name = "Before" },
        };
        var sync = CreateSync(bridge, enabled: true);
        var target = new PlaylistSearchSyncTarget();
        sync.Attach(target);
        sync.OnViewOpened();

        bridge.RaiseChanged(new Playnite.SDK.Models.FilterPresetSettings { Name = "After" });

        Assert.Equal("After", target.SearchQuery);
    }

    private static MainSearchSync CreateSync(FakeMainFilterPanelBridge bridge, bool enabled)
    {
        var lookup = new DictionaryScopedFilterNameLookup();
        return new MainSearchSync(
            () => enabled,
            bridge,
            new MainSearchFilterNameResolver(lookup));
    }
}
