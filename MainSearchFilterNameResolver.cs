using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Linq;

namespace Playlist
{
    internal enum ScopedFilterKind
    {
        Developer,
        Publisher,
        Tag,
        Genre,
        Category,
        Feature,
    }

    internal interface IScopedFilterNameLookup
    {
        /// <summary>Resolves a database ID to its display name for the given scope kind.</summary>
        string ResolveId(ScopedFilterKind kind, Guid id);

        /// <summary>Resolves user text to an ID filter when possible; otherwise returns a text-only filter.</summary>
        IdItemFilterItemProperties ResolveQuery(ScopedFilterKind kind, string query);
    }

    /// <summary>Playnite database-backed implementation of <see cref="IScopedFilterNameLookup"/>.</summary>
    internal sealed class MainSearchFilterNameResolver : IScopedFilterNameLookup
    {
        private readonly IPlayniteAPI playniteApi;

        public MainSearchFilterNameResolver(IPlayniteAPI playniteApi)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
        }

        public string ResolveId(ScopedFilterKind kind, Guid id)
        {
            switch (kind)
            {
                case ScopedFilterKind.Developer:
                case ScopedFilterKind.Publisher:
                    return playniteApi.Database.Companies.Get(id)?.Name;
                case ScopedFilterKind.Tag:
                    return playniteApi.Database.Tags.Get(id)?.Name;
                case ScopedFilterKind.Genre:
                    return playniteApi.Database.Genres.Get(id)?.Name;
                case ScopedFilterKind.Category:
                    return playniteApi.Database.Categories.Get(id)?.Name;
                case ScopedFilterKind.Feature:
                    return playniteApi.Database.Features.Get(id)?.Name;
                default:
                    return null;
            }
        }

        public IdItemFilterItemProperties ResolveQuery(ScopedFilterKind kind, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            string trimmed = query.Trim();
            if (Guid.TryParse(trimmed, out Guid parsedId) && !string.IsNullOrEmpty(ResolveId(kind, parsedId)))
            {
                return new IdItemFilterItemProperties(parsedId);
            }

            Guid? matchedId = TryFindIdByName(kind, trimmed);
            if (matchedId.HasValue)
            {
                return new IdItemFilterItemProperties(matchedId.Value);
            }

            return new IdItemFilterItemProperties(trimmed);
        }

        private Guid? TryFindIdByName(ScopedFilterKind kind, string name)
        {
            switch (kind)
            {
                case ScopedFilterKind.Developer:
                case ScopedFilterKind.Publisher:
                    return playniteApi.Database.Companies
                        .FirstOrDefault(company => string.Equals(company.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Tag:
                    return playniteApi.Database.Tags
                        .FirstOrDefault(tag => string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Genre:
                    return playniteApi.Database.Genres
                        .FirstOrDefault(genre => string.Equals(genre.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Category:
                    return playniteApi.Database.Categories
                        .FirstOrDefault(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Feature:
                    return playniteApi.Database.Features
                        .FirstOrDefault(feature => string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                default:
                    return null;
            }
        }
    }
}
