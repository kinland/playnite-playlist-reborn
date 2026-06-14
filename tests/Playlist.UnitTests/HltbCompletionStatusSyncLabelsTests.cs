using Moq;
using Playnite.SDK;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class HltbCompletionStatusSyncLabelsTests : IDisposable
{
    public HltbCompletionStatusSyncLabelsTests()
    {
        var resourceProvider = new Mock<IResourceProvider>();
        resourceProvider.Setup(provider => provider.GetString("LOCHltbUserListBacklog")).Returns("Backlog");
        HltbLocalizedStringResolver.TestResourceProvider = resourceProvider.Object;
    }

    public void Dispose()
    {
        HltbLocalizedStringResolver.TestResourceProvider = null;
    }

    [Fact]
    public void FormatSyncTarget_uses_hltb_plugin_label()
    {
        Assert.Equal("HLTB: Backlog", HltbCompletionStatusSyncLabels.FormatSyncTarget("LOCHltbUserListBacklog", "Backlog"));
    }
}
