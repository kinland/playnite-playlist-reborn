using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    /// <summary>AND-combines scoped clauses; each clause may OR or AND its values.</summary>
    internal sealed class ScopedSearchClauseGroup
    {
        public ScopedSearchClauseGroup(IReadOnlyList<ScopedSearchClause> clauses)
        {
            Clauses = clauses ?? new ScopedSearchClause[0];
        }

        public IReadOnlyList<ScopedSearchClause> Clauses { get; }

        /// <summary>Returns true when every clause matches at least one candidate string (tags, genres, etc.).</summary>
        public bool Matches(IEnumerable<string> candidates)
        {
            string[] scopedCandidates = candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .ToArray();

            foreach (ScopedSearchClause clause in Clauses)
            {
                if (!EvaluateClause(clause, scopedCandidates))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateClause(ScopedSearchClause clause, string[] candidates)
        {
            if (clause.Values.Count == 0)
            {
                return true;
            }

            bool matched;
            if (clause.CombineWithin == ScopedValueCombine.And)
            {
                matched = clause.Values.All(value => ValueMatches(value, candidates));
            }
            else
            {
                matched = clause.Values.Any(value => ValueMatches(value, candidates));
            }

            return clause.Negated ? !matched : matched;
        }

        private static bool ValueMatches(string value, string[] candidates)
        {
            SearchQueryMatcher matcher = SearchQueryMatcher.Create(value);
            return candidates.Any(candidate => matcher.IsMatch(candidate));
        }
    }
}
