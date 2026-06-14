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
        /// <summary>Parses a Playlist query into per-scope push plans and syncability metadata.</summary>
        public static MainSearchSyncAnalysis AnalyzePlaylistQuery(string playlistQuery)
        {
            SearchQuerySpec spec = SearchQuerySpec.Parse(playlistQuery);
            bool hasIntraKindConflict = false;
            ScopePushPlan developer = BuildScopePlan(spec, ScopedFilterKind.Developer, ref hasIntraKindConflict);
            ScopePushPlan tag = BuildScopePlan(spec, ScopedFilterKind.Tag, ref hasIntraKindConflict);
            ScopePushPlan genre = BuildScopePlan(spec, ScopedFilterKind.Genre, ref hasIntraKindConflict);
            ScopePushPlan publisher = BuildScopePlan(spec, ScopedFilterKind.Publisher, ref hasIntraKindConflict);
            ScopePushPlan category = BuildScopePlan(spec, ScopedFilterKind.Category, ref hasIntraKindConflict);
            ScopePushPlan feature = BuildScopePlan(spec, ScopedFilterKind.Feature, ref hasIntraKindConflict);

            bool? useAndFilteringStyle = ResolveUseAndFilteringStyle(
                developer, tag, genre, publisher, category, feature, hasIntraKindConflict);
            bool hasMatchAllConflict = HasMatchAllConflict(developer, tag, genre, publisher, category, feature);

            return new MainSearchSyncAnalysis(
                spec.NameQuery,
                developer,
                tag,
                genre,
                publisher,
                category,
                feature,
                spec.HasPlaylistOnlySyntax,
                hasMatchAllConflict,
                useAndFilteringStyle);
        }

        /// <summary>Writes syncable Playlist query parts into a copy of the main filter preset.</summary>
        public static FilterPresetSettings ApplySyncPush(
            FilterPresetSettings currentMain,
            string playlistQuery,
            IScopedFilterNameLookup nameLookup)
        {
            FilterPresetSettings settings = CloneMainSettings(currentMain);
            MainSearchSyncAnalysis analysis = AnalyzePlaylistQuery(playlistQuery);

            settings.Name = analysis.Name;
            ApplyScopePush(settings, ScopedFilterKind.Developer, analysis.Developer, nameLookup, analysis.HasMatchAllConflict, analysis.IsFullySyncable);
            ApplyScopePush(settings, ScopedFilterKind.Tag, analysis.Tag, nameLookup, analysis.HasMatchAllConflict, analysis.IsFullySyncable);
            ApplyScopePush(settings, ScopedFilterKind.Genre, analysis.Genre, nameLookup, analysis.HasMatchAllConflict, analysis.IsFullySyncable);
            ApplyScopePush(settings, ScopedFilterKind.Publisher, analysis.Publisher, nameLookup, analysis.HasMatchAllConflict, analysis.IsFullySyncable);
            ApplyScopePush(settings, ScopedFilterKind.Category, analysis.Category, nameLookup, analysis.HasMatchAllConflict, analysis.IsFullySyncable);
            ApplyScopePush(settings, ScopedFilterKind.Feature, analysis.Feature, nameLookup, analysis.HasMatchAllConflict, analysis.IsFullySyncable);

            if (analysis.UseAndFilteringStyle.HasValue)
            {
                settings.UseAndFilteringStyle = analysis.UseAndFilteringStyle.Value;
            }

            return settings;
        }

        /// <summary>Builds a snapshot of syncable fields without clearing playlist-only negations on the live main preset.</summary>
        public static FilterPresetSettings BuildSyncSnapshot(
            FilterPresetSettings currentMain,
            string playlistQuery,
            IScopedFilterNameLookup nameLookup)
        {
            FilterPresetSettings settings = CloneMainSettings(currentMain);
            MainSearchSyncAnalysis analysis = AnalyzePlaylistQuery(playlistQuery);

            settings.Name = analysis.Name;
            CopyScopeToSnapshot(settings, ScopedFilterKind.Developer, analysis.Developer, nameLookup, analysis.HasMatchAllConflict);
            CopyScopeToSnapshot(settings, ScopedFilterKind.Tag, analysis.Tag, nameLookup, analysis.HasMatchAllConflict);
            CopyScopeToSnapshot(settings, ScopedFilterKind.Genre, analysis.Genre, nameLookup, analysis.HasMatchAllConflict);
            CopyScopeToSnapshot(settings, ScopedFilterKind.Publisher, analysis.Publisher, nameLookup, analysis.HasMatchAllConflict);
            CopyScopeToSnapshot(settings, ScopedFilterKind.Category, analysis.Category, nameLookup, analysis.HasMatchAllConflict);
            CopyScopeToSnapshot(settings, ScopedFilterKind.Feature, analysis.Feature, nameLookup, analysis.HasMatchAllConflict);

            if (analysis.UseAndFilteringStyle.HasValue)
            {
                settings.UseAndFilteringStyle = analysis.UseAndFilteringStyle.Value;
            }

            return settings;
        }

        /// <summary>Reconstructs Playlist scoped syntax from the main filter preset.</summary>
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

            bool useAnd = mainSettings.UseAndFilteringStyle;
            AppendScopedFromMain(parts, "dev", ScopedFilterKind.Developer, mainSettings.Developer, nameLookup, useAnd);
            AppendScopedFromMain(parts, "tag", ScopedFilterKind.Tag, mainSettings.Tag, nameLookup, useAnd);
            AppendScopedFromMain(parts, "genre", ScopedFilterKind.Genre, mainSettings.Genre, nameLookup, useAnd);
            AppendScopedFromMain(parts, "pub", ScopedFilterKind.Publisher, mainSettings.Publisher, nameLookup, useAnd);
            AppendScopedFromMain(parts, "cat", ScopedFilterKind.Category, mainSettings.Category, nameLookup, useAnd);
            AppendScopedFromMain(parts, "feat", ScopedFilterKind.Feature, mainSettings.Feature, nameLookup, useAnd);

            return string.Join(" ", parts).Trim();
        }

        /// <summary>Compares syncable main-panel fields, resolving IDs to names where needed.</summary>
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
                && left.UseAndFilteringStyle == right.UseAndFilteringStyle
                && SyncedScopeEqual(left.Developer, right.Developer, ScopedFilterKind.Developer, nameLookup)
                && SyncedScopeEqual(left.Tag, right.Tag, ScopedFilterKind.Tag, nameLookup)
                && SyncedScopeEqual(left.Genre, right.Genre, ScopedFilterKind.Genre, nameLookup)
                && SyncedScopeEqual(left.Publisher, right.Publisher, ScopedFilterKind.Publisher, nameLookup)
                && SyncedScopeEqual(left.Category, right.Category, ScopedFilterKind.Category, nameLookup)
                && SyncedScopeEqual(left.Feature, right.Feature, ScopedFilterKind.Feature, nameLookup);
        }

        /// <summary>True when the user cleared every syncable field that existed in a prior push snapshot.</summary>
        public static bool SyncedFieldsCleared(FilterPresetSettings main, FilterPresetSettings snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (!SnapshotHadAnySyncableContent(snapshot))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.Name) && !string.IsNullOrWhiteSpace(main?.Name))
            {
                return false;
            }

            if (SnapshotScopeWasSet(snapshot.Developer) && SnapshotScopeWasSet(main?.Developer))
            {
                return false;
            }

            if (SnapshotScopeWasSet(snapshot.Tag) && SnapshotScopeWasSet(main?.Tag))
            {
                return false;
            }

            if (SnapshotScopeWasSet(snapshot.Genre) && SnapshotScopeWasSet(main?.Genre))
            {
                return false;
            }

            if (SnapshotScopeWasSet(snapshot.Publisher) && SnapshotScopeWasSet(main?.Publisher))
            {
                return false;
            }

            if (SnapshotScopeWasSet(snapshot.Category) && SnapshotScopeWasSet(main?.Category))
            {
                return false;
            }

            if (SnapshotScopeWasSet(snapshot.Feature) && SnapshotScopeWasSet(main?.Feature))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Chooses the Playlist query to show when opening the view or after main-panel edits.
        /// Merges preserved playlist-only syntax with current main fields when appropriate.
        /// </summary>
        public static string ResolveReturnQuery(
            string preservedPlaylistQuery,
            FilterPresetSettings snapshot,
            FilterPresetSettings currentMain,
            IScopedFilterNameLookup nameLookup)
        {
            if (string.IsNullOrEmpty(preservedPlaylistQuery) && snapshot == null)
            {
                return ToPlaylistQuery(currentMain, nameLookup);
            }

            if (snapshot != null && MatchesSyncedState(currentMain, snapshot, nameLookup))
            {
                return preservedPlaylistQuery ?? string.Empty;
            }

            SearchQuerySpec spec = SearchQuerySpec.Parse(preservedPlaylistQuery);
            if (spec.HasPlaylistOnlySyntax)
            {
                if (SyncedFieldsCleared(currentMain, snapshot))
                {
                    return ToPlaylistQuery(currentMain, nameLookup);
                }

                return MergeSyncableMainWithPlaylistOnlyParts(
                    preservedPlaylistQuery,
                    currentMain,
                    nameLookup);
            }

            return ToPlaylistQuery(currentMain, nameLookup);
        }

        /// <summary>
        /// Rebuilds a query that still contains playlist-only clauses while replacing syncable kinds
        /// with the current main-panel values.
        /// </summary>
        private static string MergeSyncableMainWithPlaylistOnlyParts(
            string preservedPlaylistQuery,
            FilterPresetSettings currentMain,
            IScopedFilterNameLookup nameLookup)
        {
            SearchQuerySpec preserved = SearchQuerySpec.Parse(preservedPlaylistQuery);
            SearchQuerySpec mainSpec = SearchQuerySpec.Parse(ToPlaylistQuery(currentMain, nameLookup));
            List<string> parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(mainSpec.NameQuery))
            {
                parts.Add(mainSpec.NameQuery);
            }

            foreach (ScopedFilterKind kind in ManagedScopedFilterKinds)
            {
                IEnumerable<ScopedSearchClause> clauses = preserved.KindHasPlaylistOnlySyntax(kind)
                    ? preserved.GetClauses(kind)
                    : mainSpec.GetClauses(kind);

                foreach (ScopedSearchClause clause in clauses)
                {
                    string formatted = FormatScopedClause(clause);
                    if (!string.IsNullOrEmpty(formatted))
                    {
                        parts.Add(formatted);
                    }
                }
            }

            return string.Join(" ", parts).Trim();
        }

        private static readonly ScopedFilterKind[] ManagedScopedFilterKinds =
        {
            ScopedFilterKind.Developer,
            ScopedFilterKind.Tag,
            ScopedFilterKind.Genre,
            ScopedFilterKind.Publisher,
            ScopedFilterKind.Category,
            ScopedFilterKind.Feature,
        };

        private static string FormatScopedClause(ScopedSearchClause clause)
        {
            if (clause.Values.Count == 0)
            {
                return string.Empty;
            }

            string prefix = GetScopedSyntaxPrefix(clause.Kind);
            string valuePart;
            if (clause.Values.Count == 1)
            {
                valuePart = SearchQueryTokenizer.QuoteIfNeeded(clause.Values[0]);
            }
            else if (clause.CombineWithin == ScopedValueCombine.And)
            {
                valuePart = string.Join(
                    "&",
                    clause.Values.Select(SearchQueryTokenizer.QuoteIfNeeded));
            }
            else
            {
                valuePart = string.Join(
                    ",",
                    clause.Values.Select(SearchQueryTokenizer.QuoteIfNeeded));
            }

            string scoped = $"{prefix}:{valuePart}";
            return clause.Negated ? $"!{scoped}" : scoped;
        }

        private static string GetScopedSyntaxPrefix(ScopedFilterKind kind)
        {
            switch (kind)
            {
                case ScopedFilterKind.Developer: return "dev";
                case ScopedFilterKind.Tag: return "tag";
                case ScopedFilterKind.Genre: return "genre";
                case ScopedFilterKind.Publisher: return "pub";
                case ScopedFilterKind.Category: return "cat";
                case ScopedFilterKind.Feature: return "feat";
                default: return kind.ToString().ToLowerInvariant();
            }
        }

        private static bool SnapshotHadAnySyncableContent(FilterPresetSettings snapshot)
        {
            return !string.IsNullOrWhiteSpace(snapshot.Name)
                || SnapshotScopeWasSet(snapshot.Developer)
                || SnapshotScopeWasSet(snapshot.Tag)
                || SnapshotScopeWasSet(snapshot.Genre)
                || SnapshotScopeWasSet(snapshot.Publisher)
                || SnapshotScopeWasSet(snapshot.Category)
                || SnapshotScopeWasSet(snapshot.Feature);
        }

        private static bool SnapshotScopeWasSet(IdItemFilterItemProperties filter)
        {
            return filter != null
                && (!string.IsNullOrWhiteSpace(filter.Text) || (filter.Ids != null && filter.Ids.Count > 0));
        }

        private static ScopePushPlan BuildScopePlan(
            SearchQuerySpec spec,
            ScopedFilterKind kind,
            ref bool hasIntraKindConflict)
        {
            // Multiple clauses for one kind (e.g. dev:a dev:b) are playlist-only OR and cannot be pushed as a single main field.
            ScopedSearchClause[] clauses = spec.GetClauses(kind).ToArray();
            if (clauses.Length == 0)
            {
                return ScopePushPlan.ClearField;
            }

            if (clauses.Any(clause => clause.Negated))
            {
                return ScopePushPlan.ClearNegatedField;
            }

            if (clauses.Length > 1)
            {
                if (clauses.Any(clause => clause.Values.Count > 1))
                {
                    hasIntraKindConflict = true;
                    return ScopePushPlan.Skip;
                }

                List<string> values = clauses.SelectMany(clause => clause.Values).ToList();
                return new ScopePushPlan(ScopePushMode.PushAndList, values);
            }

            ScopedSearchClause singleClause = clauses[0];
            if (singleClause.Values.Count == 0)
            {
                return ScopePushPlan.ClearField;
            }

            if (singleClause.Values.Count == 1)
            {
                return new ScopePushPlan(ScopePushMode.PushSingle, singleClause.Values);
            }

            if (singleClause.CombineWithin == ScopedValueCombine.And)
            {
                return new ScopePushPlan(ScopePushMode.PushAndList, singleClause.Values);
            }

            return new ScopePushPlan(ScopePushMode.PushOrList, singleClause.Values);
        }

        private static bool? ResolveUseAndFilteringStyle(
            ScopePushPlan developer,
            ScopePushPlan tag,
            ScopePushPlan genre,
            ScopePushPlan publisher,
            ScopePushPlan category,
            ScopePushPlan feature,
            bool hasIntraKindConflict)
        {
            if (hasIntraKindConflict)
            {
                return false;
            }

            GetCrossKindAndOrNeeds(
                developer, tag, genre, publisher, category, feature,
                out bool needsAnd,
                out bool needsOr);

            if (needsAnd)
            {
                return true;
            }

            if (needsOr)
            {
                return false;
            }

            return null;
        }

        private static bool HasMatchAllConflict(
            ScopePushPlan developer,
            ScopePushPlan tag,
            ScopePushPlan genre,
            ScopePushPlan publisher,
            ScopePushPlan category,
            ScopePushPlan feature)
        {
            GetCrossKindAndOrNeeds(
                developer, tag, genre, publisher, category, feature,
                out bool needsAnd,
                out bool needsOr);
            return needsAnd && needsOr;
        }

        private static void GetCrossKindAndOrNeeds(
            ScopePushPlan developer,
            ScopePushPlan tag,
            ScopePushPlan genre,
            ScopePushPlan publisher,
            ScopePushPlan category,
            ScopePushPlan feature,
            out bool needsAnd,
            out bool needsOr)
        {
            needsAnd = IsPushAndList(developer)
                || IsPushAndList(tag)
                || IsPushAndList(genre)
                || IsPushAndList(publisher)
                || IsPushAndList(category)
                || IsPushAndList(feature);

            needsOr = IsPushOrOrSingle(developer)
                || IsPushOrOrSingle(tag)
                || IsPushOrOrSingle(genre)
                || IsPushOrOrSingle(publisher)
                || IsPushOrOrSingle(category)
                || IsPushOrOrSingle(feature);
        }

        private static bool IsPushAndList(ScopePushPlan plan) => plan.Mode == ScopePushMode.PushAndList;

        private static bool IsPushOrOrSingle(ScopePushPlan plan) =>
            plan.Mode == ScopePushMode.PushOrList || plan.Mode == ScopePushMode.PushSingle;

        private static void ApplyScopePush(
            FilterPresetSettings settings,
            ScopedFilterKind kind,
            ScopePushPlan plan,
            IScopedFilterNameLookup nameLookup,
            bool hasMatchAllConflict,
            bool isFullySyncable)
        {
            if (plan.Mode == ScopePushMode.Skip)
            {
                return;
            }

            if (plan.Mode == ScopePushMode.ClearNegated)
            {
                SetScopeFilter(settings, kind, null);
                return;
            }

            if (plan.Mode == ScopePushMode.Clear)
            {
                if (isFullySyncable)
                {
                    SetScopeFilter(settings, kind, null);
                }

                return;
            }

            if (hasMatchAllConflict && plan.Mode == ScopePushMode.PushAndList)
            {
                return;
            }

            SetScopeFilter(settings, kind, BuildIdFilter(plan, kind, nameLookup));
        }

        private static void CopyScopeToSnapshot(
            FilterPresetSettings settings,
            ScopedFilterKind kind,
            ScopePushPlan plan,
            IScopedFilterNameLookup nameLookup,
            bool hasMatchAllConflict)
        {
            if (plan.Mode == ScopePushMode.Skip)
            {
                return;
            }

            if (plan.Mode == ScopePushMode.ClearNegated)
            {
                SetScopeFilter(settings, kind, null);
                return;
            }

            if (plan.Mode == ScopePushMode.Clear)
            {
                SetScopeFilter(settings, kind, null);
                return;
            }

            if (hasMatchAllConflict && plan.Mode == ScopePushMode.PushAndList)
            {
                return;
            }

            SetScopeFilter(settings, kind, BuildIdFilter(plan, kind, nameLookup));
        }

        private static IdItemFilterItemProperties BuildIdFilter(
            ScopePushPlan plan,
            ScopedFilterKind kind,
            IScopedFilterNameLookup nameLookup)
        {
            List<Guid> ids = new List<Guid>();
            List<string> textValues = new List<string>();
            bool anyTextOnly = false;

            foreach (string value in plan.Values)
            {
                IdItemFilterItemProperties resolved = nameLookup?.ResolveQuery(kind, value);
                if (resolved?.Ids != null && resolved.Ids.Count == 1)
                {
                    ids.Add(resolved.Ids[0]);
                    string name = nameLookup?.ResolveId(kind, resolved.Ids[0]);
                    textValues.Add(string.IsNullOrEmpty(name) ? resolved.Ids[0].ToString() : name);
                }
                else if (!string.IsNullOrWhiteSpace(resolved?.Text))
                {
                    anyTextOnly = true;
                    textValues.Add(resolved.Text.Trim());
                }
            }

            if (textValues.Count == 0)
            {
                return null;
            }

            if (!anyTextOnly && ids.Count == textValues.Count)
            {
                if (ids.Count == 1)
                {
                    return new IdItemFilterItemProperties(ids[0]);
                }

                return new IdItemFilterItemProperties(ids);
            }

            return new IdItemFilterItemProperties(string.Join(", ", textValues));
        }

        private static void SetScopeFilter(
            FilterPresetSettings settings,
            ScopedFilterKind kind,
            IdItemFilterItemProperties value)
        {
            switch (kind)
            {
                case ScopedFilterKind.Developer:
                    settings.Developer = value;
                    break;
                case ScopedFilterKind.Tag:
                    settings.Tag = value;
                    break;
                case ScopedFilterKind.Genre:
                    settings.Genre = value;
                    break;
                case ScopedFilterKind.Publisher:
                    settings.Publisher = value;
                    break;
                case ScopedFilterKind.Category:
                    settings.Category = value;
                    break;
                case ScopedFilterKind.Feature:
                    settings.Feature = value;
                    break;
            }
        }

        private static void AppendScopedFromMain(
            List<string> parts,
            string prefix,
            ScopedFilterKind kind,
            IdItemFilterItemProperties filter,
            IScopedFilterNameLookup nameLookup,
            bool useAndFilteringStyle)
        {
            if (filter == null)
            {
                return;
            }

            List<string> values = GetFilterDisplayValues(filter, kind, nameLookup).ToList();
            if (values.Count == 0)
            {
                return;
            }

            if (values.Count == 1)
            {
                parts.Add($"{prefix}:{SearchQueryTokenizer.QuoteIfNeeded(values[0])}");
                return;
            }

            if (useAndFilteringStyle)
            {
                foreach (string value in values)
                {
                    parts.Add($"{prefix}:{SearchQueryTokenizer.QuoteIfNeeded(value)}");
                }

                return;
            }

            string joined = string.Join(",", values.Select(SearchQueryTokenizer.QuoteIfNeeded));
            parts.Add($"{prefix}:{joined}");
        }

        private static IEnumerable<string> GetFilterDisplayValues(
            IdItemFilterItemProperties filter,
            ScopedFilterKind kind,
            IScopedFilterNameLookup nameLookup)
        {
            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                foreach (string value in filter.Text.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
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

        private static bool SyncedScopeEqual(
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
