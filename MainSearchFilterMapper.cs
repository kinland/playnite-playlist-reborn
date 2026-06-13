using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    /// <summary>
    /// Maps between Playlist scoped search syntax and Playnite filter-panel fields.
    /// </summary>
    internal static class MainSearchFilterMapper
    {
        public static FilterPresetSettings ApplyPlaylistQuery(
            FilterPresetSettings currentMain,
            string playlistQuery,
            IScopedFilterNameLookup nameLookup)
        {
            FilterPresetSettings settings = CloneMainSettings(currentMain);
            SearchQuerySpec spec = SearchQuerySpec.Parse(playlistQuery);

            settings.Name = spec.NameQuery;
            settings.Developer = ToIdFilter(spec.DeveloperQueries, ScopedFilterKind.Developer, nameLookup);
            settings.Tag = ToIdFilter(spec.TagQueries, ScopedFilterKind.Tag, nameLookup);
            settings.Genre = ToIdFilter(spec.GenreQueries, ScopedFilterKind.Genre, nameLookup);
            settings.Publisher = ToIdFilter(spec.PublisherQueries, ScopedFilterKind.Publisher, nameLookup);
            settings.Category = ToIdFilter(spec.CategoryQueries, ScopedFilterKind.Category, nameLookup);
            settings.Feature = ToIdFilter(spec.FeatureQueries, ScopedFilterKind.Feature, nameLookup);

            return settings;
        }

        public static string ToPlaylistQuery(FilterPresetSettings mainSettings, IScopedFilterNameLookup nameLookup)
        {
            if (mainSettings == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(mainSettings.Name))
            {
                parts.Add(mainSettings.Name.Trim());
            }

            AppendScoped(parts, "dev", ScopedFilterKind.Developer, mainSettings.Developer, nameLookup);
            AppendScoped(parts, "tag", ScopedFilterKind.Tag, mainSettings.Tag, nameLookup);
            AppendScoped(parts, "genre", ScopedFilterKind.Genre, mainSettings.Genre, nameLookup);
            AppendScoped(parts, "pub", ScopedFilterKind.Publisher, mainSettings.Publisher, nameLookup);
            AppendScoped(parts, "cat", ScopedFilterKind.Category, mainSettings.Category, nameLookup);
            AppendScoped(parts, "feat", ScopedFilterKind.Feature, mainSettings.Feature, nameLookup);

            return string.Join(" ", parts).Trim();
        }

        public static bool MatchesSyncedState(
            FilterPresetSettings left,
            FilterPresetSettings right,
            IScopedFilterNameLookup nameLookup)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(NormalizeName(left.Name), NormalizeName(right.Name), StringComparison.Ordinal)
                && FilterValuesEqual(left.Developer, right.Developer, ScopedFilterKind.Developer, nameLookup)
                && FilterValuesEqual(left.Tag, right.Tag, ScopedFilterKind.Tag, nameLookup)
                && FilterValuesEqual(left.Genre, right.Genre, ScopedFilterKind.Genre, nameLookup)
                && FilterValuesEqual(left.Publisher, right.Publisher, ScopedFilterKind.Publisher, nameLookup)
                && FilterValuesEqual(left.Category, right.Category, ScopedFilterKind.Category, nameLookup)
                && FilterValuesEqual(left.Feature, right.Feature, ScopedFilterKind.Feature, nameLookup);
        }

        private static FilterPresetSettings CloneMainSettings(FilterPresetSettings source)
        {
            if (source == null)
            {
                return new FilterPresetSettings();
            }

            return new FilterPresetSettings
            {
                UseAndFilteringStyle = source.UseAndFilteringStyle,
                IsInstalled = source.IsInstalled,
                IsUnInstalled = source.IsUnInstalled,
                Hidden = source.Hidden,
                Favorite = source.Favorite,
                Name = source.Name,
                Version = source.Version,
                ReleaseYear = CloneStringFilter(source.ReleaseYear),
                Genre = CloneIdFilter(source.Genre),
                Platform = CloneIdFilter(source.Platform),
                Publisher = CloneIdFilter(source.Publisher),
                Developer = CloneIdFilter(source.Developer),
                Category = CloneIdFilter(source.Category),
                Tag = CloneIdFilter(source.Tag),
                Series = CloneIdFilter(source.Series),
                Region = CloneIdFilter(source.Region),
                Source = CloneIdFilter(source.Source),
                AgeRating = CloneIdFilter(source.AgeRating),
                Library = CloneIdFilter(source.Library),
                CompletionStatuses = CloneIdFilter(source.CompletionStatuses),
                Feature = CloneIdFilter(source.Feature),
                UserScore = CloneEnumFilter(source.UserScore),
                CriticScore = CloneEnumFilter(source.CriticScore),
                CommunityScore = CloneEnumFilter(source.CommunityScore),
                LastActivity = CloneEnumFilter(source.LastActivity),
                RecentActivity = CloneEnumFilter(source.RecentActivity),
                Added = CloneEnumFilter(source.Added),
                Modified = CloneEnumFilter(source.Modified),
                PlayTime = CloneEnumFilter(source.PlayTime),
                InstallSize = CloneEnumFilter(source.InstallSize),
            };
        }

        private static IdItemFilterItemProperties ToIdFilter(
            IReadOnlyList<string> queries,
            ScopedFilterKind kind,
            IScopedFilterNameLookup nameLookup)
        {
            if (queries == null || queries.Count == 0)
            {
                return null;
            }

            if (queries.Count == 1)
            {
                return nameLookup?.ResolveQuery(kind, queries[0]);
            }

            List<Guid> ids = new List<Guid>();
            List<string> unresolved = new List<string>();
            foreach (string query in queries)
            {
                IdItemFilterItemProperties resolved = nameLookup?.ResolveQuery(kind, query);
                if (resolved?.Ids != null && resolved.Ids.Count == 1)
                {
                    ids.Add(resolved.Ids[0]);
                }
                else if (!string.IsNullOrWhiteSpace(resolved?.Text))
                {
                    unresolved.Add(resolved.Text.Trim());
                }
            }

            if (unresolved.Count > 0)
            {
                return new IdItemFilterItemProperties(string.Join(", ", unresolved));
            }

            if (ids.Count == 1)
            {
                return new IdItemFilterItemProperties(ids[0]);
            }

            if (ids.Count > 1)
            {
                return new IdItemFilterItemProperties(ids);
            }

            return null;
        }

        private static void AppendScoped(
            List<string> parts,
            string prefix,
            ScopedFilterKind kind,
            IdItemFilterItemProperties filter,
            IScopedFilterNameLookup nameLookup)
        {
            if (filter == null)
            {
                return;
            }

            foreach (string value in GetFilterDisplayValues(filter, kind, nameLookup))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    parts.Add($"{prefix}:{value}");
                }
            }
        }

        private static IEnumerable<string> GetFilterDisplayValues(
            IdItemFilterItemProperties filter,
            ScopedFilterKind kind,
            IScopedFilterNameLookup nameLookup)
        {
            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                foreach (string value in filter.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = value.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        yield return trimmed;
                    }
                }

                yield break;
            }

            if (filter.Ids == null || filter.Ids.Count == 0)
            {
                yield break;
            }

            foreach (Guid id in filter.Ids)
            {
                string name = nameLookup?.ResolveId(kind, id);
                yield return string.IsNullOrEmpty(name) ? id.ToString() : name;
            }
        }

        private static bool FilterValuesEqual(
            IdItemFilterItemProperties left,
            IdItemFilterItemProperties right,
            ScopedFilterKind kind,
            IScopedFilterNameLookup nameLookup)
        {
            return string.Equals(
                GetComparableFilterSignature(left, kind, nameLookup),
                GetComparableFilterSignature(right, kind, nameLookup),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetComparableFilterSignature(
            IdItemFilterItemProperties filter,
            ScopedFilterKind kind,
            IScopedFilterNameLookup nameLookup)
        {
            if (filter == null)
            {
                return string.Empty;
            }

            List<string> values = GetFilterDisplayValues(filter, kind, nameLookup)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrEmpty(value))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return string.Join(",", values);
        }

        private static string NormalizeName(string name)
        {
            return name ?? string.Empty;
        }

        private static StringFilterItemProperties CloneStringFilter(StringFilterItemProperties source)
        {
            if (source?.Values == null)
            {
                return null;
            }

            return new StringFilterItemProperties(source.Values.ToList());
        }

        private static IdItemFilterItemProperties CloneIdFilter(IdItemFilterItemProperties source)
        {
            if (source == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(source.Text))
            {
                return new IdItemFilterItemProperties(source.Text);
            }

            if (source.Ids == null || source.Ids.Count == 0)
            {
                return null;
            }

            return new IdItemFilterItemProperties(source.Ids.ToList());
        }

        private static EnumFilterItemProperties CloneEnumFilter(EnumFilterItemProperties source)
        {
            if (source?.Values == null)
            {
                return null;
            }

            return new EnumFilterItemProperties(source.Values.ToList());
        }
    }
}
