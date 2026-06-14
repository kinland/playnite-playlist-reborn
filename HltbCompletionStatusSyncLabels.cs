namespace Playlist
{
    /// <summary>
    /// Formats HLTB sync target labels for settings UI using HLTB plugin localization.
    /// </summary>
    internal static class HltbCompletionStatusSyncLabels
    {
        internal static string FormatSyncTarget(string hltbResourceKey, string englishBaseline)
        {
            string hltbLabel = HltbLocalizedStringResolver.Resolve(hltbResourceKey, string.Empty, englishBaseline);
            return "HLTB: " + hltbLabel;
        }
    }
}
