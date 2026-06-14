using Playnite.SDK.Models;
using Playlist;
using System;
using System.Collections.Generic;

namespace Playlist.UnitTests.TestStubs;

internal sealed class PassthroughScopedFilterNameLookup : IScopedFilterNameLookup
{
    public string ResolveId(ScopedFilterKind kind, Guid id) => null;

    public IdItemFilterItemProperties ResolveQuery(ScopedFilterKind kind, string query)
    {
        return new IdItemFilterItemProperties(query);
    }
}

internal sealed class DictionaryScopedFilterNameLookup : IScopedFilterNameLookup
{
    private readonly Dictionary<(ScopedFilterKind, Guid), string> idNames;
    private readonly Dictionary<(ScopedFilterKind, string), Guid> nameIds;

    public DictionaryScopedFilterNameLookup(
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
