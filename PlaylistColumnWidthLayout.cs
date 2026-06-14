using System;
using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    /// <summary>
    /// Distributes GridView column widths across the available list width using per-column minimums,
    /// a small bonus on narrow columns, and remaining slack on Name and HowLongToBeat.
    /// </summary>
    internal static class PlaylistColumnWidthLayout
    {
        internal const string RankColumnKey = "Rank";
        internal const string IconColumnKey = "Icon";
        internal const string NameColumnKey = "Name";
        internal const string PlaytimeColumnKey = "Playtime";
        internal const string CompletionStatusColumnKey = "CompletionStatus";
        internal const string LastPlayedColumnKey = "LastPlayed";
        internal const string LastActivityColumnKey = "LastActivity";
        internal const string HowLongToBeatColumnKey = "HowLongToBeat";

        /// <summary>Extra padding added to each narrow column when the list is wider than the minimum layout.</summary>
        internal const double NarrowColumnBonusPadding = 6;

        /// <summary>At most this fraction of leftover width is spent on narrow-column bonuses.</summary>
        internal const double MaxNarrowBonusShareOfExtra = 0.15;

        internal static bool IsFlexColumn(string columnKey)
        {
            return columnKey == NameColumnKey || columnKey == HowLongToBeatColumnKey;
        }

        internal static double GetMinimumWidth(string columnKey)
        {
            switch (columnKey)
            {
                case RankColumnKey:
                    return 45;
                case IconColumnKey:
                    return PlaylistGridViewLayout.IconColumnWidth;
                case PlaytimeColumnKey:
                    return 111;
                case CompletionStatusColumnKey:
                    return 142;
                case LastPlayedColumnKey:
                    return 126;
                case LastActivityColumnKey:
                    return 124;
                case NameColumnKey:
                    return 200;
                case HowLongToBeatColumnKey:
                    return 120;
                default:
                    return 0;
            }
        }

        internal static IReadOnlyDictionary<string, double> Distribute(
            double availableWidth,
            IReadOnlyList<string> visibleColumnKeys,
            IReadOnlyDictionary<string, double> preferredWidths)
        {
            if (visibleColumnKeys == null || visibleColumnKeys.Count == 0 || availableWidth <= 0)
            {
                return new Dictionary<string, double>();
            }

            preferredWidths = preferredWidths ?? new Dictionary<string, double>();

            var narrowKeys = new List<string>();
            var flexKeys = new List<string>();
            bool hasIcon = false;
            foreach (string key in visibleColumnKeys)
            {
                if (key == IconColumnKey)
                {
                    hasIcon = true;
                    continue;
                }

                if (IsFlexColumn(key))
                {
                    flexKeys.Add(key);
                }
                else
                {
                    narrowKeys.Add(key);
                }
            }

            double GetBaseWidth(string columnKey)
            {
                double minimum = GetMinimumWidth(columnKey);
                if (preferredWidths.TryGetValue(columnKey, out double preferred)
                    && !double.IsNaN(preferred)
                    && preferred > 0)
                {
                    return Math.Max(minimum, preferred);
                }

                return minimum;
            }

            var result = new Dictionary<string, double>();
            double iconWidth = PlaylistGridViewLayout.IconColumnWidth;
            double usedWidth = 0;
            if (hasIcon)
            {
                result[IconColumnKey] = iconWidth;
                usedWidth += iconWidth;
            }

            Dictionary<string, double> narrowBases = narrowKeys.ToDictionary(key => key, GetBaseWidth);
            Dictionary<string, double> flexBases = flexKeys.ToDictionary(key => key, GetBaseWidth);
            double narrowBaseTotal = narrowBases.Values.Sum();
            double flexBaseTotal = flexBases.Values.Sum();
            double totalBaseWidth = usedWidth + narrowBaseTotal + flexBaseTotal;
            if (totalBaseWidth <= 0)
            {
                return result;
            }

            double extraWidth = availableWidth - totalBaseWidth;
            if (extraWidth <= 0)
            {
                double shrinkableWidth = narrowBaseTotal + flexBaseTotal;
                double targetShrinkableWidth = Math.Max(0, availableWidth - usedWidth);
                if (shrinkableWidth <= 0)
                {
                    return result;
                }

                double scale = targetShrinkableWidth / shrinkableWidth;
                foreach (string key in narrowKeys)
                {
                    result[key] = narrowBases[key] * scale;
                }

                foreach (string key in flexKeys)
                {
                    result[key] = flexBases[key] * scale;
                }

                return result;
            }

            double narrowBonusBudget = Math.Min(
                narrowKeys.Count * NarrowColumnBonusPadding,
                extraWidth * MaxNarrowBonusShareOfExtra);
            double bonusPerNarrow = narrowKeys.Count > 0 ? narrowBonusBudget / narrowKeys.Count : 0;
            double flexExtra = extraWidth - narrowBonusBudget;

            foreach (string key in narrowKeys)
            {
                result[key] = narrowBases[key] + bonusPerNarrow;
            }

            if (flexKeys.Count == 0)
            {
                if (narrowKeys.Count > 0 && flexExtra > 0 && narrowBaseTotal > 0)
                {
                    foreach (string key in narrowKeys)
                    {
                        result[key] += flexExtra * (narrowBases[key] / narrowBaseTotal);
                    }
                }
            }
            else if (flexBaseTotal > 0)
            {
                foreach (string key in flexKeys)
                {
                    result[key] = flexBases[key] + (flexExtra * (flexBases[key] / flexBaseTotal));
                }
            }

            double drift = availableWidth - result.Values.Sum();
            if (Math.Abs(drift) > 0.01)
            {
                string adjustKey = flexKeys.Count > 0
                    ? flexKeys[flexKeys.Count - 1]
                    : narrowKeys.LastOrDefault();
                if (!string.IsNullOrEmpty(adjustKey) && result.ContainsKey(adjustKey))
                {
                    result[adjustKey] += drift;
                }
            }

            return result;
        }
    }
}
