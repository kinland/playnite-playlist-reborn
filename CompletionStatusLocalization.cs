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

            // Borrowed LOCCompletionStatus* keys exist only in supplemental override dictionaries.
            // Playnite core uses English status names directly and does not ship these resource keys.
            if (PlaylistLocalizationOverride.TryGetString(resourceKey, out string localized))
            {
                return localized;
            }

            return completionStatusName;
        }
    }
}
