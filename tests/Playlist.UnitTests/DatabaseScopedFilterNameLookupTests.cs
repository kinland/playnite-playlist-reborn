using Moq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playlist;
using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

public class DatabaseScopedFilterNameLookupTests
{
    private static readonly Guid RemedyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void ResolveId_returns_company_name_for_developer()
    {
        var companies = new Mock<IItemCollection<Company>>();
        companies.Setup(collection => collection.Get(RemedyId)).Returns(new Company { Id = RemedyId, Name = "Remedy Entertainment" });
        var database = new Mock<IGameDatabaseAPI>();
        database.Setup(db => db.Companies).Returns(companies.Object);
        var lookup = new DatabaseScopedFilterNameLookup(database.Object);

        Assert.Equal("Remedy Entertainment", lookup.ResolveId(ScopedFilterKind.Developer, RemedyId));
    }

    [Fact]
    public void ResolveQuery_matches_tag_name_case_insensitively()
    {
        var tagId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var tagList = new List<Tag> { new Tag { Id = tagId, Name = "Roguelike" } };
        var tags = new Mock<IItemCollection<Tag>>();
        tags.Setup(collection => collection.Get(tagId)).Returns((Tag)null);
        tags.As<IEnumerable<Tag>>().Setup(collection => collection.GetEnumerator()).Returns(tagList.GetEnumerator());
        var database = new Mock<IGameDatabaseAPI>();
        database.Setup(db => db.Tags).Returns(tags.Object);
        var lookup = new DatabaseScopedFilterNameLookup(database.Object);

        IdItemFilterItemProperties resolved = lookup.ResolveQuery(ScopedFilterKind.Tag, "roguelike");

        Assert.Equal(tagId, resolved.Ids[0]);
    }

    [Fact]
    public void ResolveQuery_falls_back_to_text_when_name_unknown()
    {
        var tags = new Mock<IItemCollection<Tag>>();
        tags.As<IEnumerable<Tag>>().Setup(collection => collection.GetEnumerator())
            .Returns(new List<Tag>().GetEnumerator());
        var database = new Mock<IGameDatabaseAPI>();
        database.Setup(db => db.Tags).Returns(tags.Object);
        var lookup = new DatabaseScopedFilterNameLookup(database.Object);

        IdItemFilterItemProperties resolved = lookup.ResolveQuery(ScopedFilterKind.Tag, "Unknown Tag");

        Assert.Equal("Unknown Tag", resolved.Text);
    }
}
