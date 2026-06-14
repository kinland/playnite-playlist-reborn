using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(HltbSettingsTestCollection))]
public class CompletionStatusSyncTierTests : IDisposable
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
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void IsSyncableTier_uses_hltb_config_mapping_when_configured()
    {
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var otherId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        WriteHltbConfig($@"{{
  ""GameStatusPlaying"": ""{playingId}"",
  ""GameStatusCompleted"": ""{completedId}"",
  ""GameStatusCompletionist"": ""{completionistId}""
}}");

        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = playingId, Name = "Playing" },
            new CompletionStatus { Id = completedId, Name = "Beaten" },
            new CompletionStatus { Id = completionistId, Name = "Completed" },
            new CompletionStatus { Id = otherId, Name = "Plan to Play" },
        };

        Assert.True(CompletionStatusSyncTier.IsSyncableTier(playingId, statuses, null));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(completedId, statuses, null));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(completionistId, statuses, null));
        Assert.False(CompletionStatusSyncTier.IsSyncableTier(otherId, statuses, null));
        Assert.False(CompletionStatusSyncTier.IsSyncableTier(Guid.Empty, statuses, null));
    }

    [Fact]
    public void IsSyncableTier_includes_not_played_backlog_mapping()
    {
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var notPlayedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        WriteHltbConfig($@"{{
  ""GameStatusPlaying"": ""{playingId}"",
  ""GameStatusCompleted"": ""{completedId}"",
  ""GameStatusCompletionist"": ""{completionistId}""
}}");

        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = playingId, Name = "Playing" },
            new CompletionStatus { Id = completedId, Name = "Beaten" },
            new CompletionStatus { Id = completionistId, Name = "Completed" },
            new CompletionStatus { Id = notPlayedId, Name = "Not Played" },
        };

        Assert.True(CompletionStatusSyncTier.IsSyncableTier(notPlayedId, statuses, null));
    }

    [Fact]
    public void IsSyncableTier_uses_name_defaults_when_hltb_mapping_incomplete()
    {
        WriteHltbConfig("{}");

        var playingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var beatenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var completedId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var otherId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = playingId, Name = "Playing" },
            new CompletionStatus { Id = beatenId, Name = "Beaten" },
            new CompletionStatus { Id = completedId, Name = "Completed" },
            new CompletionStatus { Id = otherId, Name = "On Hold" },
        };

        Assert.True(CompletionStatusSyncTier.IsSyncableTier(playingId, statuses, null));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(beatenId, statuses, null));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(completedId, statuses, null));
        Assert.False(CompletionStatusSyncTier.IsSyncableTier(otherId, statuses, null));
    }

    [Fact]
    public void ResolveEffectiveMapping_prefers_hltb_config_over_defaults()
    {
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        WriteHltbConfig($@"{{
  ""GameStatusPlaying"": ""{playingId}"",
  ""GameStatusCompleted"": ""{completedId}"",
  ""GameStatusCompletionist"": ""{completionistId}""
}}");

        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Playing" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Beaten" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Completed" },
        };

        HltbCompletionStatusMapping mapping = CompletionStatusSyncTier.ResolveEffectiveMapping(statuses, null);

        Assert.Equal(playingId, mapping.GameStatusPlaying);
        Assert.Equal(completedId, mapping.GameStatusCompleted);
        Assert.Equal(completionistId, mapping.GameStatusCompletionist);
    }

    private void WriteHltbConfig(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), "playlist-hltb-config-" + Guid.NewGuid() + ".json");
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        tempPaths.Add(path);
        HltbCompletionStatusSyncConfig.TestConfigPathOverride = path;
    }
}
