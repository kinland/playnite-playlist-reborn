using Playnite.SDK;

namespace Playlist
{
    /// <summary>
    /// Copy for the HowLongToBeat column header base label and hover/active sort suffixes.
    /// Time-type labels prefer the HLTB plugin's localization when translated; Playlist keys are the fallback.
    /// </summary>
    internal static class HltbColumnHeaderLabels
    {
        internal const string ColumnHeaderLocKey = "LOCPlaylist_Column_HowLongToBeat";
        private const string HltbColumnLocKey = "LOCHowLongToBeat";
        private const string HltbColumnEnglishBaseline = "HowLongToBeat";

        public static string GetColumnBaseText()
        {
            return HltbLocalizedStringResolver.Resolve(
                HltbColumnLocKey,
                ColumnHeaderLocKey,
                HltbColumnEnglishBaseline);
        }

        public static string GetPreferredTimeTypeLabel(HltbPreferredTimeType type)
        {
            return HltbLocalizedStringResolver.Resolve(
                GetHltbTimeTypeResourceKey(type),
                GetPreferredTimeTypeResourceKey(type),
                GetHltbTimeTypeEnglishBaseline(type));
        }

        internal static string GetHltbTimeTypeResourceKey(HltbPreferredTimeType type)
        {
            switch (type)
            {
                case HltbPreferredTimeType.MainStoryExtra:
                    return "LOCHowLongToBeatMainExtra";
                case HltbPreferredTimeType.Completionist:
                    return "LOCHowLongToBeatCompletionist";
                case HltbPreferredTimeType.Solo:
                    return "LOCHowLongToBeatSolo";
                case HltbPreferredTimeType.CoOp:
                    return "LOCHowLongToBeatCoOp";
                case HltbPreferredTimeType.Versus:
                    return "LOCHowLongToBeatVs";
                case HltbPreferredTimeType.MainStory:
                default:
                    return "LOCHowLongToBeatMainStory";
            }
        }

        internal static string GetPreferredTimeTypeResourceKey(HltbPreferredTimeType type)
        {
            switch (type)
            {
                case HltbPreferredTimeType.MainStoryExtra:
                    return "LOCPlaylist_Hltb_TimeType_MainExtra";
                case HltbPreferredTimeType.Completionist:
                    return "LOCPlaylist_Hltb_TimeType_Completionist";
                case HltbPreferredTimeType.Solo:
                    return "LOCPlaylist_Hltb_TimeType_Solo";
                case HltbPreferredTimeType.CoOp:
                    return "LOCPlaylist_Hltb_TimeType_CoOp";
                case HltbPreferredTimeType.Versus:
                    return "LOCPlaylist_Hltb_TimeType_Versus";
                case HltbPreferredTimeType.MainStory:
                default:
                    return "LOCPlaylist_Hltb_TimeType_MainStory";
            }
        }

        internal static string GetHltbTimeTypeEnglishBaseline(HltbPreferredTimeType type)
        {
            switch (type)
            {
                case HltbPreferredTimeType.MainStoryExtra:
                    return "Main + extra";
                case HltbPreferredTimeType.Completionist:
                    return "Completionist";
                case HltbPreferredTimeType.Solo:
                    return "Solo";
                case HltbPreferredTimeType.CoOp:
                    return "Co-Op";
                case HltbPreferredTimeType.Versus:
                    return "Vs";
                case HltbPreferredTimeType.MainStory:
                default:
                    return "Main story";
            }
        }

        public static string FormatActiveSortSuffix(string typeLabel)
        {
            return PlaylistLocalization.Format("LOCPlaylist_Hltb_SortSuffix_Active", typeLabel);
        }

        public static string FormatHoverSortSuffix(string typeLabel)
        {
            return PlaylistLocalization.Format("LOCPlaylist_Hltb_SortSuffix_Hover", typeLabel);
        }
    }
}
