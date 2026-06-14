using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

public class CompletionStatusSyncTierTests
{
    [Fact]
    public void IsSyncableTier_uses_saved_mapping_when_configured()
    {
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var otherId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        var settings = new PlaylistSettings
        {
            HltbSyncStatusPlayingId = playingId,
            HltbSyncStatusCompletedId = completedId,
            HltbSyncStatusCompletionistId = completionistId,
        };

        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = playingId, Name = "Playing" },
            new CompletionStatus { Id = completedId, Name = "Beaten" },
            new CompletionStatus { Id = completionistId, Name = "Completed" },
            new CompletionStatus { Id = otherId, Name = "Plan to Play" },
        };

        Assert.True(CompletionStatusSyncTier.IsSyncableTier(playingId, statuses, settings));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(completedId, statuses, settings));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(completionistId, statuses, settings));
        Assert.False(CompletionStatusSyncTier.IsSyncableTier(otherId, statuses, settings));
        Assert.False(CompletionStatusSyncTier.IsSyncableTier(Guid.Empty, statuses, settings));
    }

    [Fact]
    public void IsSyncableTier_includes_not_played_backlog_mapping()
    {
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var notPlayedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        var settings = new PlaylistSettings
        {
            HltbSyncStatusPlayingId = playingId,
            HltbSyncStatusCompletedId = completedId,
            HltbSyncStatusCompletionistId = completionistId,
        };

        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = playingId, Name = "Playing" },
            new CompletionStatus { Id = completedId, Name = "Beaten" },
            new CompletionStatus { Id = completionistId, Name = "Completed" },
            new CompletionStatus { Id = notPlayedId, Name = "Not Played" },
        };

        Assert.True(CompletionStatusSyncTier.IsSyncableTier(notPlayedId, statuses, settings));
    }

    [Fact]
    public void IsSyncableTier_uses_name_defaults_when_mapping_incomplete()
    {
        var playingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var beatenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var completedId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var otherId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var settings = new PlaylistSettings();
        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = playingId, Name = "Playing" },
            new CompletionStatus { Id = beatenId, Name = "Beaten" },
            new CompletionStatus { Id = completedId, Name = "Completed" },
            new CompletionStatus { Id = otherId, Name = "On Hold" },
        };

        Assert.True(CompletionStatusSyncTier.IsSyncableTier(playingId, statuses, settings));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(beatenId, statuses, settings));
        Assert.True(CompletionStatusSyncTier.IsSyncableTier(completedId, statuses, settings));
        Assert.False(CompletionStatusSyncTier.IsSyncableTier(otherId, statuses, settings));
    }

    [Fact]
    public void ResolveEffectiveMapping_prefers_saved_ids_over_defaults()
    {
        var playingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var completionistId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var settings = new PlaylistSettings
        {
            HltbSyncStatusPlayingId = playingId,
            HltbSyncStatusCompletedId = completedId,
            HltbSyncStatusCompletionistId = completionistId,
        };

        var statuses = new List<CompletionStatus>
        {
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Playing" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Beaten" },
            new CompletionStatus { Id = Guid.NewGuid(), Name = "Completed" },
        };

        HltbCompletionStatusMapping mapping = CompletionStatusSyncTier.ResolveEffectiveMapping(statuses, settings);

        Assert.Equal(playingId, mapping.GameStatusPlaying);
        Assert.Equal(completedId, mapping.GameStatusCompleted);
        Assert.Equal(completionistId, mapping.GameStatusCompletionist);
    }
}
