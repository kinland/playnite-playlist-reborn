using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Playlist
{
    /// <summary>
    /// Query matcher used by playlist search for wildcard, partial, and typo-tolerant matching.
    /// </summary>
    internal sealed class SearchQueryMatcher
    {
        private readonly string normalizedQuery;
        private readonly string normalizedWildcardQuery;
        private readonly Regex wildcardRegex;
        private readonly bool useWildcard;
        private readonly int fuzzyThreshold;

        private SearchQueryMatcher(string normalizedQuery, string normalizedWildcardQuery, Regex wildcardRegex, bool useWildcard, int fuzzyThreshold)
        {
            this.normalizedQuery = normalizedQuery;
            this.normalizedWildcardQuery = normalizedWildcardQuery;
            this.wildcardRegex = wildcardRegex;
            this.useWildcard = useWildcard;
            this.fuzzyThreshold = fuzzyThreshold;
        }

        /// <summary>
        /// Creates a matcher instance from raw query text.
        /// </summary>
        public static SearchQueryMatcher Create(string query)
        {
            string trimmed = query?.Trim() ?? string.Empty;
            string normalized = Normalize(trimmed);
            if (string.IsNullOrEmpty(normalized))
            {
                return new SearchQueryMatcher(string.Empty, string.Empty, null, false, 0);
            }

            bool containsWildcard = trimmed.IndexOf('*') >= 0 || trimmed.IndexOf('?') >= 0;
            Regex regex = null;
            string normalizedWildcard = string.Empty;
            if (containsWildcard)
            {
                normalizedWildcard = NormalizeWildcardQuery(trimmed);
                regex = new Regex(BuildWildcardPattern(normalizedWildcard), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }

            int threshold = Math.Max(1, Math.Min(3, normalized.Length / 4));
            return new SearchQueryMatcher(normalized, normalizedWildcard, regex, containsWildcard, threshold);
        }

        /// <summary>
        /// Returns true if candidate text matches current query semantics.
        /// </summary>
        public bool IsMatch(string candidate)
        {
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return true;
            }

            string normalizedCandidate = Normalize(candidate);
            if (string.IsNullOrEmpty(normalizedCandidate))
            {
                return false;
            }

            if (useWildcard)
            {
                if (string.IsNullOrEmpty(normalizedWildcardQuery))
                {
                    return true;
                }

                return wildcardRegex?.IsMatch(normalizedCandidate) == true;
            }

            if (normalizedCandidate.Contains(normalizedQuery))
            {
                return true;
            }

            return IsFuzzyMatch(normalizedCandidate) || IsTokenFuzzyMatch(normalizedCandidate);
        }

        /// <summary>
        /// Whole-string fuzzy match and per-word fuzzy fallback.
        /// </summary>
        private bool IsFuzzyMatch(string normalizedCandidate)
        {
            if (normalizedQuery.Length < 3)
            {
                return false;
            }

            if (BoundedLevenshtein(normalizedCandidate, normalizedQuery, fuzzyThreshold) <= fuzzyThreshold)
            {
                return true;
            }

            string[] words = normalizedCandidate.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                if (word.Contains(normalizedQuery))
                {
                    return true;
                }

                if (BoundedLevenshtein(word, normalizedQuery, fuzzyThreshold) <= fuzzyThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts wildcard query text into regex pattern text.
        /// </summary>
        private static string BuildWildcardPattern(string query)
        {
            StringBuilder pattern = new StringBuilder();
            pattern.Append(".*");
            foreach (char c in query ?? string.Empty)
            {
                switch (c)
                {
                    case '*':
                        pattern.Append(".*");
                        break;
                    case '?':
                        pattern.Append(".");
                        break;
                    default:
                        pattern.Append(Regex.Escape(c.ToString()));
                        break;
                }
            }

            pattern.Append(".*");
            return pattern.ToString();
        }

        /// <summary>
        /// Token-level fuzzy match to support multi-word typo-tolerant queries.
        /// </summary>
        private bool IsTokenFuzzyMatch(string normalizedCandidate)
        {
            string[] queryTokens = normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] candidateTokens = normalizedCandidate.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (queryTokens.Length == 0 || candidateTokens.Length == 0)
            {
                return false;
            }

            foreach (string queryToken in queryTokens)
            {
                int tokenThreshold = Math.Max(1, Math.Min(2, queryToken.Length / 4));
                bool matchedToken = false;
                foreach (string candidateToken in candidateTokens)
                {
                    if (candidateToken.Contains(queryToken) ||
                        BoundedLevenshtein(candidateToken, queryToken, tokenThreshold) <= tokenThreshold)
                    {
                        matchedToken = true;
                        break;
                    }
                }

                if (!matchedToken)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Normalizes wildcard query text while preserving wildcard operators.
        /// </summary>
        private static string NormalizeWildcardQuery(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            foreach (char c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '*' || c == '?')
                {
                    builder.Append(c);
                    previousWasSpace = false;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// Normalizes plain text by lowering case, dropping punctuation, and collapsing spaces.
        /// </summary>
        internal static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            foreach (char c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    previousWasSpace = false;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// Bounded Levenshtein distance that exits early once maxDistance is exceeded.
        /// </summary>
        private static int BoundedLevenshtein(string source, string target, int maxDistance)
        {
            if (source == null)
            {
                return target == null ? 0 : target.Length;
            }

            if (target == null)
            {
                return source.Length;
            }

            int n = source.Length;
            int m = target.Length;
            if (Math.Abs(n - m) > maxDistance)
            {
                return maxDistance + 1;
            }

            int[] previous = new int[m + 1];
            int[] current = new int[m + 1];
            for (int j = 0; j <= m; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= n; i++)
            {
                current[0] = i;
                int rowMin = current[0];
                int start = Math.Max(1, i - maxDistance);
                int end = Math.Min(m, i + maxDistance);

                for (int j = 1; j < start; j++)
                {
                    current[j] = maxDistance + 1;
                }

                for (int j = start; j <= end; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    int deletion = previous[j] + 1;
                    int insertion = current[j - 1] + 1;
                    int substitution = previous[j - 1] + cost;
                    int distance = Math.Min(Math.Min(deletion, insertion), substitution);
                    current[j] = distance;
                    if (distance < rowMin)
                    {
                        rowMin = distance;
                    }
                }

                for (int j = end + 1; j <= m; j++)
                {
                    current[j] = maxDistance + 1;
                }

                if (rowMin > maxDistance)
                {
                    return maxDistance + 1;
                }

                int[] swap = previous;
                previous = current;
                current = swap;
            }

            return previous[m];
        }
    }
}
