using System;
using System.Collections.Generic;
using System.Text;

namespace Playlist
{
    internal static class SearchQueryTokenizer
    {
        public static List<string> Tokenize(string query)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(query))
            {
                return tokens;
            }

            StringBuilder current = new StringBuilder();
            char? quote = null;
            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];
                if (quote != null)
                {
                    if (c == quote)
                    {
                        if (i + 1 < query.Length && query[i + 1] == quote)
                        {
                            current.Append(c);
                            i++;
                        }
                        else
                        {
                            current.Append(c);
                            quote = null;
                        }
                    }
                    else if (c == '\\' && i + 1 < query.Length)
                    {
                        current.Append(query[i + 1]);
                        i++;
                    }
                    else
                    {
                        current.Append(c);
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quote = c;
                    current.Append(c);
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    FlushToken(current, tokens);
                    continue;
                }

                current.Append(c);
            }

            FlushToken(current, tokens);
            return tokens;
        }

        public static List<string> SplitScopedValues(string valuePart, ScopedValueCombine combine)
        {
            List<string> values = new List<string>();
            if (string.IsNullOrWhiteSpace(valuePart))
            {
                return values;
            }

            char splitOn = combine == ScopedValueCombine.And ? '&' : ',';
            StringBuilder current = new StringBuilder();
            char? quote = null;
            for (int i = 0; i < valuePart.Length; i++)
            {
                char c = valuePart[i];
                if (quote != null)
                {
                    if (c == quote)
                    {
                        if (i + 1 < valuePart.Length && valuePart[i + 1] == quote)
                        {
                            current.Append(c);
                            i++;
                        }
                        else
                        {
                            current.Append(c);
                            quote = null;
                        }
                    }
                    else if (c == '\\' && i + 1 < valuePart.Length)
                    {
                        current.Append(valuePart[i + 1]);
                        i++;
                    }
                    else
                    {
                        current.Append(c);
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quote = c;
                    current.Append(c);
                    continue;
                }

                if (combine == ScopedValueCombine.Or && c == '|')
                {
                    FlushValue(current, values);
                    continue;
                }

                if (c == splitOn)
                {
                    FlushValue(current, values);
                    continue;
                }

                current.Append(c);
            }

            FlushValue(current, values);
            return values;
        }

        public static bool NeedsQuoting(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c) || c == ',' || c == '|' || c == '&' || c == ':' || c == '"' || c == '\'')
                {
                    return true;
                }
            }

            return false;
        }

        public static string StripSurroundingQuotes(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
            {
                return value ?? string.Empty;
            }

            char first = value[0];
            char last = value[value.Length - 1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        public static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value) || !NeedsQuoting(value))
            {
                return value ?? string.Empty;
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void FlushToken(StringBuilder current, List<string> tokens)
        {
            if (current.Length == 0)
            {
                return;
            }

            tokens.Add(current.ToString());
            current.Clear();
        }

        private static void FlushValue(StringBuilder current, List<string> values)
        {
            string trimmed = StripSurroundingQuotes(current.ToString().Trim());
            current.Clear();
            if (!string.IsNullOrEmpty(trimmed))
            {
                values.Add(trimmed);
            }
        }
    }
}
