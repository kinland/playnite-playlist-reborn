using System;
using System.Collections.Generic;

namespace Playlist
{
    internal sealed class SearchQuerySpec
    {
        public string NameQuery { get; }
        public IReadOnlyList<string> TagQueries { get; }
        public IReadOnlyList<string> GenreQueries { get; }
        public IReadOnlyList<string> DeveloperQueries { get; }
        public IReadOnlyList<string> PublisherQueries { get; }
        public IReadOnlyList<string> CategoryQueries { get; }
        public IReadOnlyList<string> FeatureQueries { get; }

        private SearchQuerySpec(
            string nameQuery,
            IReadOnlyList<string> tagQueries,
            IReadOnlyList<string> genreQueries,
            IReadOnlyList<string> developerQueries,
            IReadOnlyList<string> publisherQueries,
            IReadOnlyList<string> categoryQueries,
            IReadOnlyList<string> featureQueries)
        {
            NameQuery = nameQuery ?? string.Empty;
            TagQueries = tagQueries ?? Array.Empty<string>();
            GenreQueries = genreQueries ?? Array.Empty<string>();
            DeveloperQueries = developerQueries ?? Array.Empty<string>();
            PublisherQueries = publisherQueries ?? Array.Empty<string>();
            CategoryQueries = categoryQueries ?? Array.Empty<string>();
            FeatureQueries = featureQueries ?? Array.Empty<string>();
        }

        public static SearchQuerySpec Parse(string rawQuery)
        {
            string trimmed = rawQuery?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                return new SearchQuerySpec(
                    string.Empty,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            List<string> nameParts = new List<string>();
            List<string> tagQueries = new List<string>();
            List<string> genreQueries = new List<string>();
            List<string> developerQueries = new List<string>();
            List<string> publisherQueries = new List<string>();
            List<string> categoryQueries = new List<string>();
            List<string> featureQueries = new List<string>();

            string[] tokens = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (IsScopeToken(token, "tag", out string tagValue))
                {
                    string value = ResolveScopedValue(tagValue, tokens, ref i);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        tagQueries.Add(value);
                    }

                    continue;
                }

                if (IsScopeToken(token, "genre", out string genreValue))
                {
                    string value = ResolveScopedValue(genreValue, tokens, ref i);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        genreQueries.Add(value);
                    }

                    continue;
                }

                if (IsScopeToken(token, "developer", out string developerValue)
                    || IsScopeToken(token, "dev", out developerValue))
                {
                    string value = ResolveScopedValue(developerValue, tokens, ref i);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        developerQueries.Add(value);
                    }

                    continue;
                }

                if (IsScopeToken(token, "publisher", out string publisherValue)
                    || IsScopeToken(token, "pub", out publisherValue))
                {
                    string value = ResolveScopedValue(publisherValue, tokens, ref i);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        publisherQueries.Add(value);
                    }

                    continue;
                }

                if (IsScopeToken(token, "category", out string categoryValue)
                    || IsScopeToken(token, "cat", out categoryValue))
                {
                    string value = ResolveScopedValue(categoryValue, tokens, ref i);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        categoryQueries.Add(value);
                    }

                    continue;
                }

                if (IsScopeToken(token, "feature", out string featureValue)
                    || IsScopeToken(token, "feat", out featureValue))
                {
                    string value = ResolveScopedValue(featureValue, tokens, ref i);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        featureQueries.Add(value);
                    }

                    continue;
                }

                nameParts.Add(token);
            }

            return new SearchQuerySpec(
                string.Join(" ", nameParts).Trim(),
                tagQueries,
                genreQueries,
                developerQueries,
                publisherQueries,
                categoryQueries,
                featureQueries);
        }

        private static bool IsScopeToken(string token, string scope, out string value)
        {
            value = null;
            string prefix = scope + ":";
            if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            value = token.Substring(prefix.Length);
            return true;
        }

        private static string ResolveScopedValue(string inlineValue, string[] tokens, ref int index)
        {
            if (!string.IsNullOrWhiteSpace(inlineValue))
            {
                return inlineValue.Trim();
            }

            int nextIndex = index + 1;
            if (nextIndex >= tokens.Length)
            {
                return string.Empty;
            }

            string nextToken = tokens[nextIndex];
            if (nextToken.Contains(":"))
            {
                return string.Empty;
            }

            index = nextIndex;
            return nextToken.Trim();
        }
    }
}
