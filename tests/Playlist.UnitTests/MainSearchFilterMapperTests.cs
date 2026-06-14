using Playnite.SDK.Models;
using Playlist.UnitTests.TestStubs;
using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

public class MainSearchFilterMapperTests
{
    private static readonly Guid TenTonsDeveloperId = Guid.Parse("9911f200-458f-4cd7-9025-9e03b241f867");

    private static readonly Guid SeventeenBitDeveloperId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly DictionaryScopedFilterNameLookup DefaultLookup = new DictionaryScopedFilterNameLookup(
        idNames: new Dictionary<(ScopedFilterKind, Guid), string>
        {
            [(ScopedFilterKind.Developer, TenTonsDeveloperId)] = "10tons",
            [(ScopedFilterKind.Developer, SeventeenBitDeveloperId)] = "17-BIT",
        },
        nameIds: new Dictionary<(ScopedFilterKind, string), Guid>
        {
            [(ScopedFilterKind.Developer, "10tons")] = TenTonsDeveloperId,
            [(ScopedFilterKind.Developer, "17-BIT")] = SeventeenBitDeveloperId,
        });

    [Fact]
    public void ApplySyncPush_MapsNameAndDeveloperSeparately()
    {
        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "Alan dev:remedy",
            DefaultLookup);

        Assert.Equal("Alan", mapped.Name);
        Assert.NotNull(mapped.Developer);
        Assert.Equal("remedy", mapped.Developer.Text);
    }

    [Fact]
    public void ApplySyncPush_ResolvesKnownDeveloperNameToId()
    {
        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "dev:10tons",
            DefaultLookup);

        Assert.NotNull(mapped.Developer);
        Assert.Single(mapped.Developer.Ids);
        Assert.Equal(TenTonsDeveloperId, mapped.Developer.Ids[0]);
    }

    [Fact]
    public void ApplySyncPush_MapsOrDeveloperList()
    {
        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "dev:10tons,17-BIT",
            DefaultLookup);

        Assert.NotNull(mapped.Developer);
        Assert.Equal(2, mapped.Developer.Ids.Count);
        Assert.Equal(TenTonsDeveloperId, mapped.Developer.Ids[0]);
        Assert.Equal(SeventeenBitDeveloperId, mapped.Developer.Ids[1]);
        Assert.False(mapped.UseAndFilteringStyle);
    }

    [Fact]
    public void ApplySyncPush_MapsOrDeveloperListWithMixedIdAndTextResolution()
    {
        DictionaryScopedFilterNameLookup lookup = new DictionaryScopedFilterNameLookup(
            idNames: new Dictionary<(ScopedFilterKind, Guid), string>
            {
                [(ScopedFilterKind.Developer, TenTonsDeveloperId)] = "10tons",
            },
            nameIds: new Dictionary<(ScopedFilterKind, string), Guid>
            {
                [(ScopedFilterKind.Developer, "10tons")] = TenTonsDeveloperId,
            });

        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "dev:10tons,17-BIT",
            lookup);

        Assert.NotNull(mapped.Developer);
        Assert.Equal("10tons, 17-BIT", mapped.Developer.Text);
        Assert.False(mapped.UseAndFilteringStyle);
    }

    [Fact]
    public void ApplySyncPush_MapsAndTagListViaRepeatedScope()
    {
        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "tag:fps tag:roguelike",
            DefaultLookup);

        Assert.NotNull(mapped.Tag);
        Assert.Equal("fps, roguelike", mapped.Tag.Text);
        Assert.True(mapped.UseAndFilteringStyle);
    }

    [Fact]
    public void ApplySyncPush_ClearsNegatedDeveloperOnMain()
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Developer = new IdItemFilterItemProperties("remedy"),
        };

        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            current,
            "Alan !dev:remedy",
            DefaultLookup);

        Assert.Equal("Alan", mapped.Name);
        Assert.Null(mapped.Developer);
    }

    [Fact]
    public void ApplySyncPush_ClearsDeveloperForNegatedOnlyQuery()
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Developer = new IdItemFilterItemProperties("remedy"),
        };

        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            current,
            "!dev:remedy",
            DefaultLookup);

        Assert.Equal(string.Empty, mapped.Name);
        Assert.Null(mapped.Developer);
    }

    [Fact]
    public void ApplySyncPush_DoesNotSetDeveloperForNegatedOnlyQueryOnEmptyMain()
    {
        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
            new FilterPresetSettings(),
            "!dev:remedy",
            DefaultLookup);

        Assert.Null(mapped.Developer);
    }

    [Fact]
    public void ToPlaylistQuery_ReconstructsScopedSyntax()
    {
        string query = MainSearchFilterMapper.ToPlaylistQuery(new FilterPresetSettings
        {
            Name = "Alan",
            Developer = new IdItemFilterItemProperties("remedy"),
        }, DefaultLookup);

        Assert.Equal("Alan dev:remedy", query);
    }

    [Fact]
    public void ToPlaylistQuery_ResolvesDeveloperIdsToNames()
    {
        string query = MainSearchFilterMapper.ToPlaylistQuery(new FilterPresetSettings
        {
            Developer = new IdItemFilterItemProperties(TenTonsDeveloperId),
        }, DefaultLookup);

        Assert.Equal("dev:10tons", query);
    }

    [Fact]
    public void ToPlaylistQuery_QuotesValuesWithSpaces()
    {
        string query = MainSearchFilterMapper.ToPlaylistQuery(new FilterPresetSettings
        {
            Developer = new IdItemFilterItemProperties("11 bit studios"),
        }, DefaultLookup);

        Assert.Equal("dev:\"11 bit studios\"", query);
    }

    [Fact]
    public void MatchesSyncedState_TrueWhenMappedFiltersUnchanged()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "Alan dev:remedy",
            DefaultLookup);

        FilterPresetSettings current = new FilterPresetSettings
        {
            Name = "Alan",
            Developer = new IdItemFilterItemProperties("remedy"),
        };

        Assert.True(MainSearchFilterMapper.MatchesSyncedState(current, snapshot, DefaultLookup));
    }

    [Fact]
    public void MatchesSyncedState_TrueWhenMainUsesIdsAndSnapshotUsesText()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "dev:10tons",
            DefaultLookup);

        FilterPresetSettings current = new FilterPresetSettings
        {
            Developer = new IdItemFilterItemProperties(TenTonsDeveloperId),
        };

        Assert.True(MainSearchFilterMapper.MatchesSyncedState(current, snapshot, DefaultLookup));
    }

    [Fact]
    public void MatchesSyncedState_FalseWhenMainNameChanged()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "Alan dev:remedy",
            DefaultLookup);

        FilterPresetSettings current = new FilterPresetSettings
        {
            Name = "Alan Wake",
            Developer = new IdItemFilterItemProperties("remedy"),
        };

        Assert.False(MainSearchFilterMapper.MatchesSyncedState(current, snapshot, DefaultLookup));
    }

    [Fact]
    public void ApplySyncPush_ClearsManagedScopedFiltersWhenRemovedFromPlaylistQuery()
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Name = "Alan",
            Developer = new IdItemFilterItemProperties("remedy"),
            Tag = new IdItemFilterItemProperties("backlog"),
        };

        FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(current, "Alan", DefaultLookup);

        Assert.Equal("Alan", mapped.Name);
        Assert.Null(mapped.Developer);
        Assert.Null(mapped.Tag);
    }

    [Fact]
    public void ResolveReturnQuery_RestoresPreservedWhenMainMatchesSnapshot()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "Alan !dev:remedy",
            DefaultLookup);

        string query = MainSearchFilterMapper.ResolveReturnQuery(
            "Alan !dev:remedy",
            snapshot,
            snapshot,
            DefaultLookup);

        Assert.Equal("Alan !dev:remedy", query);
    }

    [Fact]
    public void ResolveReturnQuery_RebuildsFromMainWhenFullySyncableAndMainChanged()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "Alan dev:remedy",
            DefaultLookup);

        FilterPresetSettings current = new FilterPresetSettings
        {
            Name = "Alan Wake",
            Developer = new IdItemFilterItemProperties("remedy"),
        };

        string query = MainSearchFilterMapper.ResolveReturnQuery(
            "Alan dev:remedy",
            snapshot,
            current,
            DefaultLookup);

        Assert.Equal("Alan Wake dev:remedy", query);
    }

    [Fact]
    public void ResolveReturnQuery_UsesMainWhenPlaylistOnlyAndSyncedFieldsCleared()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings { Name = "Alan", Developer = new IdItemFilterItemProperties("remedy") },
            "Alan !dev:remedy",
            DefaultLookup);

        FilterPresetSettings clearedMain = new FilterPresetSettings
        {
            Tag = new IdItemFilterItemProperties("backlog"),
        };

        string query = MainSearchFilterMapper.ResolveReturnQuery(
            "Alan !dev:remedy",
            snapshot,
            clearedMain,
            DefaultLookup);

        Assert.Equal("tag:backlog", query);
    }

    [Fact]
    public void ResolveReturnQuery_MergesMainNameWithPlaylistOnlyNegation()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.BuildSyncSnapshot(
            new FilterPresetSettings(),
            "!genre:shooter",
            DefaultLookup);

        FilterPresetSettings currentMain = new FilterPresetSettings
        {
            Name = "Alan",
        };

        string query = MainSearchFilterMapper.ResolveReturnQuery(
            "!genre:shooter",
            snapshot,
            currentMain,
            DefaultLookup);

        Assert.Equal("Alan !genre:shooter", query);
    }
}
