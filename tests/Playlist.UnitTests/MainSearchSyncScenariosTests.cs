using Playnite.SDK.Models;
using Playlist.UnitTests.TestStubs;
using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

/// <summary>
/// End-to-end style tests for Playlist ↔ main filter sync permutations.
/// </summary>
public class MainSearchSyncScenariosTests
{
    private static readonly Guid TenTonsDeveloperId = Guid.Parse("9911f200-458f-4cd7-9025-9e03b241f867");
    private static readonly Guid SeventeenBitDeveloperId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid RemedyDeveloperId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly DictionaryScopedFilterNameLookup Lookup = new DictionaryScopedFilterNameLookup(
        idNames: new Dictionary<(ScopedFilterKind, Guid), string>
        {
            [(ScopedFilterKind.Developer, TenTonsDeveloperId)] = "10tons",
            [(ScopedFilterKind.Developer, SeventeenBitDeveloperId)] = "17-BIT",
            [(ScopedFilterKind.Developer, RemedyDeveloperId)] = "remedy",
        },
        nameIds: new Dictionary<(ScopedFilterKind, string), Guid>
        {
            [(ScopedFilterKind.Developer, "10tons")] = TenTonsDeveloperId,
            [(ScopedFilterKind.Developer, "17-BIT")] = SeventeenBitDeveloperId,
            [(ScopedFilterKind.Developer, "remedy")] = RemedyDeveloperId,
        });

    private static (FilterPresetSettings pushed, FilterPresetSettings snapshot) SimulateLeavePlaylist(
        FilterPresetSettings currentMain,
        string playlistQuery)
    {
        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(currentMain, playlistQuery, Lookup);
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(currentMain, playlistQuery, Lookup);
        return (pushed, snapshot);
    }

    private static string SimulateReturnToPlaylist(
        string preservedPlaylistQuery,
        FilterPresetSettings snapshot,
        FilterPresetSettings currentMain)
    {
        return MainSearchFilterMapper.ResolveReturnQuery(
            preservedPlaylistQuery,
            snapshot,
            currentMain,
            Lookup);
    }

    [Fact]
    public void RoundTrip_FullySyncable_RestoresPreservedWhenMainUnchanged()
    {
        const string playlistQuery = "Alan dev:remedy";
        (FilterPresetSettings pushed, FilterPresetSettings snapshot) = SimulateLeavePlaylist(
            new FilterPresetSettings(),
            playlistQuery);

        Assert.Equal("Alan", pushed.Name);
        Assert.NotNull(pushed.Developer);
        Assert.Equal(RemedyDeveloperId, pushed.Developer.Ids[0]);

        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, pushed);
        Assert.Equal(playlistQuery, restored);
    }

    [Fact]
    public void RoundTrip_FullySyncable_RebuildsWhenMainChangedOnReturn()
    {
        const string playlistQuery = "Alan dev:remedy";
        (_, FilterPresetSettings snapshot) = SimulateLeavePlaylist(new FilterPresetSettings(), playlistQuery);

        FilterPresetSettings changedMain = new FilterPresetSettings
        {
            Name = "Alan Wake",
            Developer = new IdItemFilterItemProperties("remedy"),
        };

        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, changedMain);
        Assert.Equal("Alan Wake dev:remedy", restored);
    }

    [Fact]
    public void RoundTrip_PlaylistOnlyNegation_RestoresPreservedWhenMainMatchesSnapshot()
    {
        const string playlistQuery = "Alan !dev:remedy";
        (FilterPresetSettings pushed, FilterPresetSettings snapshot) = SimulateLeavePlaylist(
            new FilterPresetSettings(),
            playlistQuery);

        Assert.Equal("Alan", pushed.Name);
        Assert.Null(pushed.Developer);

        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, pushed);
        Assert.Equal(playlistQuery, restored);
    }

    [Fact]
    public void RoundTrip_PlaylistOnlyNegation_MergesMainNameChangeOnReturn()
    {
        const string playlistQuery = "!genre:shooter";
        (_, FilterPresetSettings snapshot) = SimulateLeavePlaylist(new FilterPresetSettings(), playlistQuery);

        FilterPresetSettings changedMain = new FilterPresetSettings { Name = "Alan" };
        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, changedMain);
        Assert.Equal("Alan !genre:shooter", restored);
    }

    [Fact]
    public void RoundTrip_PlaylistOnlyNegation_MergesMainNameAndSyncableScopeOnReturn()
    {
        const string playlistQuery = "Alan !dev:remedy";
        (_, FilterPresetSettings snapshot) = SimulateLeavePlaylist(new FilterPresetSettings(), playlistQuery);

        FilterPresetSettings changedMain = new FilterPresetSettings
        {
            Name = "Alan Wake",
            Tag = new IdItemFilterItemProperties("backlog"),
        };

        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, changedMain);
        Assert.Equal("Alan Wake !dev:remedy tag:backlog", restored);
    }

    [Fact]
    public void RoundTrip_PlaylistOnlyNegation_UsesMainWhenSyncedFieldsCleared()
    {
        const string playlistQuery = "Alan !dev:remedy";
        FilterPresetSettings startingMain = new FilterPresetSettings
        {
            Name = "Alan",
            Developer = new IdItemFilterItemProperties("remedy"),
        };
        (_, FilterPresetSettings snapshot) = SimulateLeavePlaylist(startingMain, playlistQuery);

        FilterPresetSettings clearedMain = new FilterPresetSettings
        {
            Tag = new IdItemFilterItemProperties("backlog"),
        };

        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, clearedMain);
        Assert.Equal("tag:backlog", restored);
    }

    [Fact]
    public void RoundTrip_FirstOpen_PullsFromMainWhenNothingPreserved()
    {
        FilterPresetSettings currentMain = new FilterPresetSettings
        {
            Name = "Alan",
            Developer = new IdItemFilterItemProperties(RemedyDeveloperId),
        };

        string pulled = SimulateReturnToPlaylist(null, null, currentMain);
        Assert.Equal("Alan dev:remedy", pulled);
    }

    [Fact]
    public void ApplySyncPush_MixedSyncableAndNegated_PushesSyncableAndClearsNegatedScope()
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Genre = new IdItemFilterItemProperties("shooter"),
            Tag = new IdItemFilterItemProperties("backlog"),
        };

        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            current,
            "Alan dev:10tons !genre:shooter",
            Lookup);

        Assert.Equal("Alan", pushed.Name);
        Assert.Equal(TenTonsDeveloperId, pushed.Developer.Ids[0]);
        Assert.Null(pushed.Genre);
        Assert.Equal("backlog", pushed.Tag.Text);
    }

    [Fact]
    public void ApplySyncPush_PlaylistOnlyQuery_DoesNotClearUnrelatedMainScopes()
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Tag = new IdItemFilterItemProperties("backlog"),
            Genre = new IdItemFilterItemProperties("shooter"),
        };

        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            current,
            "Alan !dev:remedy",
            Lookup);

        Assert.Equal("Alan", pushed.Name);
        Assert.Null(pushed.Developer);
        Assert.Equal("backlog", pushed.Tag.Text);
        Assert.Equal("shooter", pushed.Genre.Text);
    }

    [Fact]
    public void ApplySyncPush_TransitionFromPositiveToNegatedDeveloper_ClearsDeveloperOnMain()
    {
        FilterPresetSettings afterPositive = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "dev:remedy",
            Lookup);
        Assert.Equal(RemedyDeveloperId, afterPositive.Developer.Ids[0]);

        FilterPresetSettings afterNegated = MainSearchFilterMapper.ApplySyncPush(
            afterPositive,
            "!dev:remedy",
            Lookup);

        Assert.Null(afterNegated.Developer);
    }

    [Fact]
    public void ApplySyncPush_MatchAllConflict_SkipsAndListButPushesOrList()
    {
        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "tag:fps tag:roguelike dev:10tons,17-BIT",
            Lookup);

        Assert.Null(pushed.Tag);
        Assert.NotNull(pushed.Developer);
        Assert.Equal(2, pushed.Developer.Ids.Count);
        Assert.True(pushed.UseAndFilteringStyle);
    }

    [Fact]
    public void BuildSyncSnapshot_MatchAllConflict_OmitsAndListFromSnapshot()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "tag:fps tag:roguelike dev:10tons,17-BIT",
            Lookup);

        Assert.Null(snapshot.Tag);
        Assert.NotNull(snapshot.Developer);
        Assert.Equal(2, snapshot.Developer.Ids.Count);
    }

    [Fact]
    public void ApplySyncPush_NotFullySyncable_DoesNotClearScopesRemovedFromQuery()
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Name = "Alan",
            Developer = new IdItemFilterItemProperties("remedy"),
            Tag = new IdItemFilterItemProperties("backlog"),
        };

        FilterPresetSettings pushed = MainSearchFilterMapper.ApplySyncPush(
            current,
            "Alan !dev:remedy",
            Lookup);

        Assert.Equal("Alan", pushed.Name);
        Assert.Null(pushed.Developer);
        Assert.Equal("backlog", pushed.Tag.Text);
    }

    [Fact]
    public void ToPlaylistQuery_AndFilteringStyle_EmitsRepeatedScopes()
    {
        string query = MainSearchFilterMapper.ToPlaylistQuery(new FilterPresetSettings
        {
            UseAndFilteringStyle = true,
            Tag = new IdItemFilterItemProperties("fps, roguelike"),
        }, Lookup);

        Assert.Equal("tag:fps tag:roguelike", query);
    }

    [Fact]
    public void ToPlaylistQuery_OrFilteringStyle_EmitsCommaSeparatedScope()
    {
        string query = MainSearchFilterMapper.ToPlaylistQuery(new FilterPresetSettings
        {
            UseAndFilteringStyle = false,
            Developer = new IdItemFilterItemProperties(new List<Guid> { TenTonsDeveloperId, SeventeenBitDeveloperId }),
        }, Lookup);

        Assert.Equal("dev:10tons,17-BIT", query);
    }

    [Fact]
    public void SyncedFieldsCleared_TrueWhenSnapshotNameClearedOnMain()
    {
        FilterPresetSettings snapshot = new FilterPresetSettings { Name = "Alan" };
        FilterPresetSettings main = new FilterPresetSettings();

        Assert.True(MainSearchFilterMapper.SyncedFieldsCleared(main, snapshot));
    }

    [Fact]
    public void SyncedFieldsCleared_FalseWhenMainStillHasSnapshotName()
    {
        FilterPresetSettings snapshot = new FilterPresetSettings { Name = "Alan" };
        FilterPresetSettings main = new FilterPresetSettings { Name = "Alan Wake" };

        Assert.False(MainSearchFilterMapper.SyncedFieldsCleared(main, snapshot));
    }

    [Fact]
    public void SyncedFieldsCleared_FalseWhenSnapshotHadNoSyncableContent()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "!dev:remedy",
            Lookup);
        FilterPresetSettings main = new FilterPresetSettings();

        Assert.False(MainSearchFilterMapper.SyncedFieldsCleared(main, snapshot));
    }

    [Fact]
    public void MatchesSyncedState_FalseWhenUseAndFilteringStyleDiffers()
    {
        FilterPresetSettings left = new FilterPresetSettings
        {
            Tag = new IdItemFilterItemProperties("fps"),
            UseAndFilteringStyle = true,
        };
        FilterPresetSettings right = new FilterPresetSettings
        {
            Tag = new IdItemFilterItemProperties("fps"),
            UseAndFilteringStyle = false,
        };

        Assert.False(MainSearchFilterMapper.MatchesSyncedState(left, right, Lookup));
    }

    [Fact]
    public void AnalyzePlaylistQuery_FlagsPlaylistOnlySyntaxForNegationAndConflict()
    {
        MainSearchSyncAnalysis negated = MainSearchFilterMapper.AnalyzePlaylistQuery("!dev:remedy");
        Assert.True(negated.HasPlaylistOnlySyntax);
        Assert.Equal(ScopePushMode.ClearNegated, negated.Developer.Mode);

        MainSearchSyncAnalysis conflict = MainSearchFilterMapper.AnalyzePlaylistQuery("tag:fps tag:roguelike dev:10tons,17-BIT");
        Assert.True(conflict.HasMatchAllConflict);
        Assert.True(conflict.IsFullySyncable);
        Assert.Equal(ScopePushMode.PushAndList, conflict.Tag.Mode);
        Assert.Equal(ScopePushMode.PushOrList, conflict.Developer.Mode);
    }

    [Fact]
    public void RoundTrip_OrDeveloperList_RestoresAfterMainUnchanged()
    {
        const string playlistQuery = "dev:10tons,17-BIT";
        (FilterPresetSettings pushed, FilterPresetSettings snapshot) = SimulateLeavePlaylist(
            new FilterPresetSettings(),
            playlistQuery);

        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, pushed);
        Assert.Equal(playlistQuery, restored);
    }

    [Fact]
    public void RoundTrip_AndTagList_RestoresAfterMainUnchanged()
    {
        const string playlistQuery = "tag:fps tag:roguelike";
        (FilterPresetSettings pushed, FilterPresetSettings snapshot) = SimulateLeavePlaylist(
            new FilterPresetSettings(),
            playlistQuery);

        Assert.True(pushed.UseAndFilteringStyle);
        string restored = SimulateReturnToPlaylist(playlistQuery, snapshot, pushed);
        Assert.Equal(playlistQuery, restored);
    }
}
