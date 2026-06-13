using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

public class MainSearchFilterMapperTests
{
    private static readonly Guid TenTonsDeveloperId = Guid.Parse("9911f200-458f-4cd7-9025-9e03b241f867");

    private sealed class TestScopedFilterNameLookup : IScopedFilterNameLookup
    {
        private readonly Dictionary<(ScopedFilterKind, Guid), string> idNames;
        private readonly Dictionary<(ScopedFilterKind, string), Guid> nameIds;

        public TestScopedFilterNameLookup(
            Dictionary<(ScopedFilterKind, Guid), string> idNames = null,
            Dictionary<(ScopedFilterKind, string), Guid> nameIds = null)
        {
            this.idNames = idNames ?? new Dictionary<(ScopedFilterKind, Guid), string>();
            this.nameIds = nameIds ?? new Dictionary<(ScopedFilterKind, string), Guid>();
        }

        public string ResolveId(ScopedFilterKind kind, Guid id)
        {
            return idNames.TryGetValue((kind, id), out string name) ? name : null;
        }

        public IdItemFilterItemProperties ResolveQuery(ScopedFilterKind kind, string query)
        {
            if (Guid.TryParse(query, out Guid parsedId) && idNames.ContainsKey((kind, parsedId)))
            {
                return new IdItemFilterItemProperties(parsedId);
            }

            if (nameIds.TryGetValue((kind, query), out Guid id))
            {
                return new IdItemFilterItemProperties(id);
            }

            return new IdItemFilterItemProperties(query);
        }
    }

    private static readonly TestScopedFilterNameLookup DefaultLookup = new TestScopedFilterNameLookup(
        idNames: new Dictionary<(ScopedFilterKind, Guid), string>
        {
            [(ScopedFilterKind.Developer, TenTonsDeveloperId)] = "10tons",
        },
        nameIds: new Dictionary<(ScopedFilterKind, string), Guid>
        {
            [(ScopedFilterKind.Developer, "10tons")] = TenTonsDeveloperId,
        });

    [Fact]
    public void ApplyPlaylistQuery_MapsNameAndDeveloperSeparately()
    {
        FilterPresetSettings mapped = MainSearchFilterMapper.ApplyPlaylistQuery(
            new FilterPresetSettings(),
            "Alan dev:remedy",
            DefaultLookup);

        Assert.Equal("Alan", mapped.Name);
        Assert.NotNull(mapped.Developer);
        Assert.Equal("remedy", mapped.Developer.Text);
    }

    [Fact]
    public void ApplyPlaylistQuery_ResolvesKnownDeveloperNameToId()
    {
        FilterPresetSettings mapped = MainSearchFilterMapper.ApplyPlaylistQuery(
            new FilterPresetSettings(),
            "dev:10tons",
            DefaultLookup);

        Assert.NotNull(mapped.Developer);
        Assert.Single(mapped.Developer.Ids);
        Assert.Equal(TenTonsDeveloperId, mapped.Developer.Ids[0]);
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
    public void MatchesSyncedState_TrueWhenMappedFiltersUnchanged()
    {
        FilterPresetSettings snapshot = MainSearchFilterMapper.ApplyPlaylistQuery(
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
        FilterPresetSettings snapshot = MainSearchFilterMapper.ApplyPlaylistQuery(
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
        FilterPresetSettings snapshot = MainSearchFilterMapper.ApplyPlaylistQuery(
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
    public void ApplyPlaylistQuery_ClearsManagedScopedFiltersWhenRemovedFromPlaylistQuery()
    {
        FilterPresetSettings current = new FilterPresetSettings
        {
            Name = "Alan",
            Developer = new IdItemFilterItemProperties("remedy"),
            Tag = new IdItemFilterItemProperties("backlog"),
        };

        FilterPresetSettings mapped = MainSearchFilterMapper.ApplyPlaylistQuery(current, "Alan", DefaultLookup);

        Assert.Equal("Alan", mapped.Name);
        Assert.Null(mapped.Developer);
        Assert.Null(mapped.Tag);
    }
}
