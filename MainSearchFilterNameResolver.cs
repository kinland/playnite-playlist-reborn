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

    /// <summary>
    /// Resolves scoped filter names and IDs from the Playnite game database.
    /// </summary>
    internal sealed class DatabaseScopedFilterNameLookup : IScopedFilterNameLookup
    {
        private readonly IGameDatabaseAPI database;

        public DatabaseScopedFilterNameLookup(IGameDatabaseAPI database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public string ResolveId(ScopedFilterKind kind, Guid id)
        {
            switch (kind)
            {
                case ScopedFilterKind.Developer:
                case ScopedFilterKind.Publisher:
                    return database.Companies.Get(id)?.Name;
                case ScopedFilterKind.Tag:
                    return database.Tags.Get(id)?.Name;
                case ScopedFilterKind.Genre:
                    return database.Genres.Get(id)?.Name;
                case ScopedFilterKind.Category:
                    return database.Categories.Get(id)?.Name;
                case ScopedFilterKind.Feature:
                    return database.Features.Get(id)?.Name;
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
                    return database.Companies
                        .FirstOrDefault(company => string.Equals(company.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Tag:
                    return database.Tags
                        .FirstOrDefault(tag => string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Genre:
                    return database.Genres
                        .FirstOrDefault(genre => string.Equals(genre.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Category:
                    return database.Categories
                        .FirstOrDefault(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                case ScopedFilterKind.Feature:
                    return database.Features
                        .FirstOrDefault(feature => string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?.Id;
                default:
                    return null;
            }
        }
    }

    /// <summary>Delegates <see cref="IScopedFilterNameLookup"/> for main-panel search sync.</summary>
    internal sealed class MainSearchFilterNameResolver : IScopedFilterNameLookup
    {
        private readonly IScopedFilterNameLookup inner;

        public MainSearchFilterNameResolver(IPlayniteAPI playniteApi)
            : this(new DatabaseScopedFilterNameLookup(playniteApi?.Database ?? throw new ArgumentNullException(nameof(playniteApi))))
        {
        }

        internal MainSearchFilterNameResolver(IScopedFilterNameLookup inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string ResolveId(ScopedFilterKind kind, Guid id) => inner.ResolveId(kind, id);

        public IdItemFilterItemProperties ResolveQuery(ScopedFilterKind kind, string query) => inner.ResolveQuery(kind, query);
    }
}
