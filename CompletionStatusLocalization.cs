using System;
using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Maps default Playnite completion status English names to LOCCompletionStatus* keys.
    /// </summary>
    internal static class CompletionStatusLocalization
    {
        private static readonly Dictionary<string, string> EnglishNameToLocKey =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Not Played"] = "LOCCompletionStatusNotPlayed",
                ["Played"] = "LOCCompletionStatusPlayed",
                ["Beaten"] = "LOCCompletionStatusBeaten",
                ["Completed"] = "LOCCompletionStatusCompleted",
                ["Playing"] = "LOCCompletionStatusPlaying",
                ["Abandoned"] = "LOCCompletionStatusAbandoned",
                ["On Hold"] = "LOCCompletionStatusOnHold",
                ["Plan to Play"] = "LOCCompletionStatusPlanToPlay",
            };

        internal static string LocalizeDisplayName(string completionStatusName)
        {
            if (string.IsNullOrWhiteSpace(completionStatusName))
            {
                return completionStatusName;
            }

            if (!EnglishNameToLocKey.TryGetValue(completionStatusName.Trim(), out string resourceKey))
            {
                return completionStatusName;
            }

            string localized = PlaylistLocalization.GetString(resourceKey);
            return string.Equals(localized, resourceKey, StringComparison.Ordinal)
                ? completionStatusName
                : localized;
        }
    }
}
