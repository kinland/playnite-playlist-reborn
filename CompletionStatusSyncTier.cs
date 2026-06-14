using Playnite.SDK.Models;
using System;
using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Resolves which Playnite completion statuses map to HLTB sync tiers for list UI styling.
    /// Uses saved playlist mapping when configured; otherwise Playnite name-based defaults.
    /// </summary>
    internal static class CompletionStatusSyncTier
    {
        internal static bool IsSyncableTier(
            Guid statusId,
            IEnumerable<CompletionStatus> completionStatuses,
            PlaylistSettings settings)
        {
            if (statusId == Guid.Empty)
            {
                return false;
            }

            HltbCompletionStatusMapping mapping = ResolveEffectiveMapping(completionStatuses, settings);
            return statusId == mapping.GameStatusPlaying
                || statusId == mapping.GameStatusCompleted
                || statusId == mapping.GameStatusCompletionist
                || statusId == mapping.GameStatusBacklog;
        }

        internal static HltbCompletionStatusMapping ResolveEffectiveMapping(
            IEnumerable<CompletionStatus> completionStatuses,
            PlaylistSettings settings)
        {
            HltbCompletionStatusMapping mapping;
            HltbCompletionStatusMapping fromSettings = settings?.ToHltbCompletionStatusMapping();
            if (fromSettings != null && fromSettings.IsConfigured())
            {
                mapping = fromSettings;
            }
            else
            {
                mapping = HltbCompletionStatusMapping.ResolveDefaults(completionStatuses);
            }

            mapping.ApplyFixedBacklogMapping(completionStatuses);
            return mapping;
        }
    }
}
