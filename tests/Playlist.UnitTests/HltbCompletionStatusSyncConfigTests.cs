using Moq;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(HltbSettingsTestCollection))]
public class HltbCompletionStatusSyncConfigTests : IDisposable
{
    private readonly List<string> tempPaths = new List<string>();

    public void Dispose()
    {
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = null;
        foreach (string path in tempPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                string temp = path + ".playlist.tmp";
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ParseMapping_reads_status_ids_and_auto_sync_flag()
    {
        const string json = @"{
  ""AutoSetGameStatusToHltb"": true,
  ""GameStatusPlaying"": ""42c2dda9-deb1-4364-8fb1-ee24e0d23c87"",
  ""GameStatusCompleted"": ""1ba7b620-73bf-463b-ac2e-4fdaf86358c0"",
  ""GameStatusCompletionist"": ""edcdd344-cedd-440c-924e-76ce8f65afce""
}";

        HltbCompletionStatusMapping mapping = HltbCompletionStatusSyncConfig.ParseMapping(json);

        Assert.True(mapping.AutoSetGameStatusToHltb);
        Assert.Equal(Guid.Parse("42c2dda9-deb1-4364-8fb1-ee24e0d23c87"), mapping.GameStatusPlaying);
        Assert.Equal(Guid.Parse("1ba7b620-73bf-463b-ac2e-4fdaf86358c0"), mapping.GameStatusCompleted);
        Assert.Equal(Guid.Parse("edcdd344-cedd-440c-924e-76ce8f65afce"), mapping.GameStatusCompletionist);
    }

    [Fact]
    public void TryWriteMapping_persists_mapping_without_dropping_existing_keys()
    {
        string path = CreateTempConfigPath();
        File.WriteAllText(
            path,
            "{\"ExistingKey\":123,\"AutoSetGameStatusToHltb\":false}",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;

        var mapping = new HltbCompletionStatusMapping
        {
            AutoSetGameStatusToHltb = true,
            GameStatusPlaying = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            GameStatusCompleted = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            GameStatusCompletionist = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        };

        Assert.True(HltbCompletionStatusSyncConfig.TryWriteMapping(mapping));

        HltbCompletionStatusMapping roundTrip = HltbCompletionStatusSyncConfig.ReadMapping();
        Assert.True(roundTrip.AutoSetGameStatusToHltb);
        Assert.Equal(mapping.GameStatusPlaying, roundTrip.GameStatusPlaying);
        Assert.Equal(mapping.GameStatusCompleted, roundTrip.GameStatusCompleted);
        Assert.Equal(mapping.GameStatusCompletionist, roundTrip.GameStatusCompletionist);

        string payload = File.ReadAllText(path);
        Assert.Contains("\"ExistingKey\": 123", payload);
    }

    [Fact]
    public void ResolveDefaults_maps_common_playnite_status_names()
    {
        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Playing" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Beaten" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Completed" },
        };

        HltbCompletionStatusMapping mapping = HltbCompletionStatusMapping.ResolveDefaults(statuses);

        Assert.Equal(statuses[0].Id, mapping.GameStatusPlaying);
        Assert.Equal(statuses[1].Id, mapping.GameStatusCompleted);
        Assert.Equal(statuses[2].Id, mapping.GameStatusCompletionist);
    }

    [Fact]
    public void VerifySettings_rejects_duplicate_status_mappings()
    {
        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();
        harness.Settings.SyncCompletionStatusWithHltb = true;
        harness.Settings.HltbSyncStatusPlayingId = Guid.NewGuid();
        harness.Settings.HltbSyncStatusCompletedId = harness.Settings.HltbSyncStatusPlayingId;
        harness.Settings.HltbSyncStatusCompletionistId = Guid.NewGuid();

        Assert.False(harness.Settings.VerifySettings(out List<string> errors));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void VerifySettings_rejects_missing_status_mappings()
    {
        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();
        harness.Settings.SyncCompletionStatusWithHltb = true;
        harness.Settings.HltbSyncStatusPlayingId = Guid.NewGuid();
        harness.Settings.HltbSyncStatusCompletedId = Guid.Empty;
        harness.Settings.HltbSyncStatusCompletionistId = Guid.NewGuid();

        Assert.False(harness.Settings.VerifySettings(out List<string> errors));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ApplyPlaylistSettings_writes_mapping_and_enables_auto_sync()
    {
        string path = CreateTempConfigPath();
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;

        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();
        harness.Settings.SyncCompletionStatusWithHltb = true;
        harness.Settings.HltbSyncStatusPlayingId = playingId;
        harness.Settings.HltbSyncStatusCompletedId = completedId;
        harness.Settings.HltbSyncStatusCompletionistId = completionistId;

        HltbCompletionStatusSyncConfig.ApplyPlaylistSettings(CreateApiWithCompletionStatuses(), harness.Settings);

        HltbCompletionStatusMapping mapping = HltbCompletionStatusSyncConfig.ReadMapping();
        Assert.True(mapping.AutoSetGameStatusToHltb);
        Assert.Equal(playingId, mapping.GameStatusPlaying);
        Assert.Equal(completedId, mapping.GameStatusCompleted);
        Assert.Equal(completionistId, mapping.GameStatusCompletionist);
    }

    [Fact]
    public void ApplyPlaylistSettings_disables_auto_sync_when_sync_turned_off()
    {
        string path = CreateTempConfigPath();
        File.WriteAllText(
            path,
            "{\"AutoSetGameStatusToHltb\":true,\"GameStatusPlaying\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;

        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();
        harness.Settings.SyncCompletionStatusWithHltb = false;

        HltbCompletionStatusSyncConfig.ApplyPlaylistSettings(CreateApiWithCompletionStatuses(), harness.Settings);

        HltbCompletionStatusMapping mapping = HltbCompletionStatusSyncConfig.ReadMapping();
        Assert.False(mapping.AutoSetGameStatusToHltb);
    }

    [Fact]
    public void ImportIntoPlaylistSettings_imports_from_hltb_config_when_playlist_ids_empty()
    {
        string path = CreateTempConfigPath();
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        File.WriteAllText(
            path,
            $@"{{
  ""AutoSetGameStatusToHltb"": true,
  ""GameStatusPlaying"": ""{playingId}"",
  ""GameStatusCompleted"": ""{completedId}"",
  ""GameStatusCompletionist"": ""{completionistId}""
}}",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;

        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();

        HltbCompletionStatusSyncConfig.ImportIntoPlaylistSettings(CreateApiWithCompletionStatuses(), harness.Settings);

        Assert.True(harness.Settings.SyncCompletionStatusWithHltb);
        Assert.Equal(playingId, harness.Settings.HltbSyncStatusPlayingId);
        Assert.Equal(completedId, harness.Settings.HltbSyncStatusCompletedId);
        Assert.Equal(completionistId, harness.Settings.HltbSyncStatusCompletionistId);
    }

    [Fact]
    public void ImportIntoPlaylistSettings_does_not_modify_hltb_config_file()
    {
        string path = CreateTempConfigPath();
        const string originalJson = "{\"ExistingKey\":123,\"AutoSetGameStatusToHltb\":true,\"GameStatusPlaying\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}";
        File.WriteAllText(path, originalJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;

        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();

        HltbCompletionStatusSyncConfig.ImportIntoPlaylistSettings(CreateApiWithCompletionStatuses(), harness.Settings);

        Assert.Equal(originalJson, File.ReadAllText(path));
    }

    [Fact]
    public void ApplyPlaylistSettings_when_sync_disabled_preserves_existing_mapping_guids()
    {
        string path = CreateTempConfigPath();
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        File.WriteAllText(
            path,
            $@"{{
  ""ExistingKey"": 123,
  ""AutoSetGameStatusToHltb"": true,
  ""GameStatusPlaying"": ""{playingId}"",
  ""GameStatusCompleted"": ""{completedId}"",
  ""GameStatusCompletionist"": ""{completionistId}""
}}",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;

        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();
        harness.Settings.SyncCompletionStatusWithHltb = false;

        HltbCompletionStatusSyncConfig.ApplyPlaylistSettings(CreateApiWithCompletionStatuses(), harness.Settings);

        HltbCompletionStatusMapping mapping = HltbCompletionStatusSyncConfig.ReadMapping();
        Assert.False(mapping.AutoSetGameStatusToHltb);
        Assert.Equal(playingId, mapping.GameStatusPlaying);
        Assert.Equal(completedId, mapping.GameStatusCompleted);
        Assert.Equal(completionistId, mapping.GameStatusCompletionist);

        string payload = File.ReadAllText(path);
        Assert.Contains("\"ExistingKey\": 123", payload);
    }

    [Fact]
    public void ApplyPlaylistSettings_when_sync_disabled_and_auto_set_already_false_does_not_write()
    {
        string path = CreateTempConfigPath();
        const string originalJson = "{\"ExistingKey\":123,\"AutoSetGameStatusToHltb\":false}";
        File.WriteAllText(path, originalJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;
        DateTime originalWriteUtc = File.GetLastWriteTimeUtc(path);

        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = true;
        harness.SimulateStartup();
        harness.Settings.SyncCompletionStatusWithHltb = false;

        HltbCompletionStatusSyncConfig.ApplyPlaylistSettings(CreateApiWithCompletionStatuses(), harness.Settings);

        Assert.Equal(originalJson, File.ReadAllText(path));
        Assert.Equal(originalWriteUtc, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void ApplyPlaylistSettings_when_hltb_integration_disabled_does_not_write_config()
    {
        string path = CreateTempConfigPath();
        const string originalJson = "{\"AutoSetGameStatusToHltb\":true}";
        File.WriteAllText(path, originalJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;

        using var harness = new PlaylistSettingsTestHarness();
        harness.Settings.EnableHowLongToBeatIntegration = false;
        harness.SimulateStartup();
        harness.Settings.SyncCompletionStatusWithHltb = true;

        HltbCompletionStatusSyncConfig.ApplyPlaylistSettings(CreateApiWithCompletionStatuses(), harness.Settings);

        Assert.Equal(originalJson, File.ReadAllText(path));
    }

    private static IPlayniteAPI CreateApiWithCompletionStatuses()
    {
        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Playing" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Beaten" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Completed" },
        };

        var completionStatuses = new Mock<IItemCollection<CompletionStatus>>();
        completionStatuses.As<IEnumerable<CompletionStatus>>()
            .Setup(collection => collection.GetEnumerator())
            .Returns(statuses.GetEnumerator());
        var database = new Mock<IGameDatabaseAPI>();
        database.Setup(db => db.CompletionStatuses).Returns(completionStatuses.Object);
        var api = new Mock<IPlayniteAPI>();
        api.Setup(playniteApi => playniteApi.Database).Returns(database.Object);
        return api.Object;
    }

    private string CreateTempConfigPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "playlist-hltb-config-" + Guid.NewGuid() + ".json");
        tempPaths.Add(path);
        return path;
    }
}
