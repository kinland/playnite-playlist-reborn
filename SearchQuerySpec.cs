using System;
using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    internal sealed class SearchQuerySpec
    {
        public string NameQuery { get; }
        public IReadOnlyList<ScopedSearchClause> Clauses { get; }

        public bool HasPlaylistOnlySyntax => Clauses.Any(clause => clause.Negated) || HasNonSyncableClauseStructure();

        private SearchQuerySpec(string nameQuery, IReadOnlyList<ScopedSearchClause> clauses)
        {
            NameQuery = nameQuery ?? string.Empty;
            Clauses = clauses ?? Array.Empty<ScopedSearchClause>();
        }

        public IEnumerable<ScopedSearchClause> GetClauses(ScopedFilterKind kind)
        {
            return Clauses.Where(clause => clause.Kind == kind);
        }

        public bool HasNonSyncableClauseStructure()
        {
            foreach (ScopedFilterKind kind in Enum.GetValues(typeof(ScopedFilterKind)))
            {
                ScopedSearchClause[] kindClauses = GetClauses(kind).ToArray();
                if (kindClauses.Length == 0
                    || kindClauses.Any(clause => clause.Negated))
                {
                    continue;
                }

                if (HasNonSyncableStructureForClauses(kindClauses))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Per-kind playlist-only syntax for merge: negation, or non-syncable AND/OR structure within the kind.
        /// </summary>
        internal bool KindHasPlaylistOnlySyntax(ScopedFilterKind kind)
        {
            ScopedSearchClause[] kindClauses = GetClauses(kind).ToArray();
            if (kindClauses.Length == 0)
            {
                return false;
            }

            if (kindClauses.Any(clause => clause.Negated))
            {
                return true;
            }

            return HasNonSyncableStructureForClauses(kindClauses);
        }

        private static bool HasNonSyncableStructureForClauses(ScopedSearchClause[] kindClauses)
        {
            if (kindClauses.Length > 1
                && kindClauses.Any(clause => clause.CombineWithin == ScopedValueCombine.Or && clause.Values.Count > 1))
            {
                return true;
            }

            return kindClauses.Any(clause =>
                clause.CombineWithin == ScopedValueCombine.And
                && clause.Values.Count > 1
                && kindClauses.Length > 1);
        }

        public static SearchQuerySpec Parse(string rawQuery)
        {
            string trimmed = rawQuery?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                return new SearchQuerySpec(string.Empty, Array.Empty<ScopedSearchClause>());
            }

            List<string> nameParts = new List<string>();
            List<ScopedSearchClause> clauses = new List<ScopedSearchClause>();
            List<string> tokens = SearchQueryTokenizer.Tokenize(trimmed);
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (TryParseScopeToken(token, out bool negated, out ScopedFilterKind kind, out string valuePart))
                {
                    if (string.IsNullOrEmpty(valuePart))
                    {
                        int nextIndex = i + 1;
                        if (nextIndex < tokens.Count && !LooksLikeScopeToken(tokens[nextIndex]))
                        {
                            valuePart = tokens[nextIndex];
                            i = nextIndex;
                        }
                    }

                    ScopedValueCombine combine = DetectCombineMode(valuePart);
                    List<string> values = SearchQueryTokenizer.SplitScopedValues(valuePart, combine);
                    if (values.Count > 0)
                    {
                        clauses.Add(new ScopedSearchClause(kind, negated, combine, values));
                    }

                    continue;
                }

                nameParts.Add(SearchQueryTokenizer.StripSurroundingQuotes(token));
            }

            return new SearchQuerySpec(string.Join(" ", nameParts).Trim(), clauses);
        }

        private static bool LooksLikeScopeToken(string token)
        {
            return TryParseScopeToken(token, out _, out _, out _);
        }

        private static bool TryParseScopeToken(
            string token,
            out bool negated,
            out ScopedFilterKind kind,
            out string valuePart)
        {
            negated = false;
            kind = default;
            valuePart = string.Empty;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            string rest = token;
            if (rest[0] == '!')
            {
                negated = true;
                rest = rest.Substring(1);
            }

            int colonIndex = rest.IndexOf(':');
            if (colonIndex <= 0)
            {
                return false;
            }

            string prefix = rest.Substring(0, colonIndex);
            valuePart = rest.Substring(colonIndex + 1);
            if (!TryMapScopePrefix(prefix, out kind))
            {
                return false;
            }

            return true;
        }

        private static bool TryMapScopePrefix(string prefix, out ScopedFilterKind kind)
        {
            switch (prefix.ToLowerInvariant())
            {
                case "tag":
                    kind = ScopedFilterKind.Tag;
                    return true;
                case "genre":
                    kind = ScopedFilterKind.Genre;
                    return true;
                case "developer":
                case "dev":
                    kind = ScopedFilterKind.Developer;
                    return true;
                case "publisher":
                case "pub":
                    kind = ScopedFilterKind.Publisher;
                    return true;
                case "category":
                case "cat":
                    kind = ScopedFilterKind.Category;
                    return true;
                case "feature":
                case "feat":
                    kind = ScopedFilterKind.Feature;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private static ScopedValueCombine DetectCombineMode(string valuePart)
        {
            if (string.IsNullOrEmpty(valuePart))
            {
                return ScopedValueCombine.Or;
            }

            bool inQuote = false;
            char quote = '\0';
            for (int i = 0; i < valuePart.Length; i++)
            {
                char c = valuePart[i];
                if (inQuote)
                {
                    if (c == quote)
                    {
                        if (i + 1 < valuePart.Length && valuePart[i + 1] == quote)
                        {
                            i++;
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inQuote = true;
                    quote = c;
                    continue;
                }

                if (c == '&')
                {
                    return ScopedValueCombine.And;
                }
            }

            return ScopedValueCombine.Or;
        }
    }
}
