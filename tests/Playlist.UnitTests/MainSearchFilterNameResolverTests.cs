using Playnite.SDK.Models;
using Playlist;
using Playlist.UnitTests.TestStubs;
using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

public class MainSearchFilterNameResolverTests
{
    [Fact]
    public void Resolver_delegates_to_inner_lookup()
    {
        var inner = new DictionaryScopedFilterNameLookup(
            nameIds: new System.Collections.Generic.Dictionary<(ScopedFilterKind, string), System.Guid>
            {
                [(ScopedFilterKind.Tag, "fps")] = System.Guid.Parse("11111111-1111-1111-1111-111111111111"),
            });
        var resolver = new MainSearchFilterNameResolver(inner);

        IdItemFilterItemProperties resolved = resolver.ResolveQuery(ScopedFilterKind.Tag, "fps");

        Assert.Equal(System.Guid.Parse("11111111-1111-1111-1111-111111111111"), resolved.Ids[0]);
    }
}
