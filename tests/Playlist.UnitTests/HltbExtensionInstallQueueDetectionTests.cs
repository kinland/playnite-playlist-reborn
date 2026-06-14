using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbExtensionInstallQueueDetectionTests : IDisposable
{
    private readonly string tempRoot;

    public HltbExtensionInstallQueueDetectionTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "playlist-hltb-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        HowLongToBeatAddonNavigation.TestExtensionInstallQueuePendingResolver = null;
        HowLongToBeatAddonNavigation.TestExtensionQueueFilePathOverride = null;
    }

    [Theory]
    [InlineData("Id: playnite-howlongtobeat-plugin\nName: HowLongToBeat", "playnite-howlongtobeat-plugin")]
    [InlineData("Name: Other\nId: playnite-howlongtobeat-plugin", "playnite-howlongtobeat-plugin")]
    [InlineData("id: PLAYNITE-HOWLONGTOBEAT-PLUGIN", "PLAYNITE-HOWLONGTOBEAT-PLUGIN")]
    public void ParseExtensionIdFromYaml_reads_id_line(string yaml, string expectedId)
    {
        Assert.Equal(expectedId, HowLongToBeatAddonNavigation.ParseExtensionIdFromYaml(yaml));
    }

    [Fact]
    public void ParseExtensionIdFromYaml_returns_null_when_id_missing()
    {
        Assert.Null(HowLongToBeatAddonNavigation.ParseExtensionIdFromYaml("Name: HowLongToBeat\nType: GenericPlugin"));
    }

    [Fact]
    public void IsExtensionInstallQueuedForRestart_detects_hltb_pext_from_queue_file()
    {
        string packagePath = CreatePackedExtension("playnite-howlongtobeat-plugin");
        WriteQueueFile(new[]
        {
            new QueueItemDto { InstallType = 0, Path = packagePath },
        });

        Assert.True(HowLongToBeatAddonNavigation.IsExtensionInstallQueuedForRestart());
    }

    [Fact]
    public void IsExtensionInstallQueuedForRestart_ignores_non_hltb_pext_in_queue_file()
    {
        string packagePath = CreatePackedExtension("some-other-plugin");
        WriteQueueFile(new[]
        {
            new QueueItemDto { InstallType = 0, Path = packagePath },
        });

        Assert.False(HowLongToBeatAddonNavigation.IsExtensionInstallQueuedForRestart());
    }

    [Fact]
    public void IsExtensionInstallQueuedForRestart_ignores_theme_install_paths()
    {
        WriteQueueFile(new[]
        {
            new QueueItemDto { InstallType = 0, Path = Path.Combine(tempRoot, "theme.pthm") },
        });

        Assert.False(HowLongToBeatAddonNavigation.IsExtensionInstallQueuedForRestart());
    }

    [Fact]
    public void ExpireAddonPending_keeps_pending_when_hltb_pext_queued_via_queue_file()
    {
        using var harness = new PlaylistSettingsTestHarness(HltbInstallState.NotInstalled);
        harness.Settings.MarkPendingIntegrationEnableFromPlaylistPrompt();

        string packagePath = CreatePackedExtension("playnite-howlongtobeat-plugin");
        WriteQueueFile(new[]
        {
            new QueueItemDto { InstallType = 0, Path = packagePath },
        });

        harness.Settings.ExpireAddonPendingIfHltbStillUnavailable();

        Assert.True(harness.Settings.PendingEnableHowLongToBeatIntegrationFromPlaylistPrompt);
    }

    public void Dispose()
    {
        HowLongToBeatAddonNavigation.TestExtensionInstallQueuePendingResolver = null;
        HowLongToBeatAddonNavigation.TestExtensionQueueFilePathOverride = null;

        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    private string CreatePackedExtension(string extensionId)
    {
        string packagePath = Path.Combine(tempRoot, extensionId + ".pext");
        using (FileStream stream = File.Create(packagePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("extension.yaml");
            using (StreamWriter writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.Write("Id: " + extensionId + "\nName: Test\n");
            }
        }

        return packagePath;
    }

    private void WriteQueueFile(QueueItemDto[] items)
    {
        string queuePath = Path.Combine(tempRoot, "extinstalls.json");
        string json = JsonSerializer.Serialize(items);
        File.WriteAllText(queuePath, json);
        HowLongToBeatAddonNavigation.TestExtensionQueueFilePathOverride = queuePath;
    }

    private sealed class QueueItemDto
    {
        public int InstallType { get; set; }

        public string Path { get; set; }
    }
}
