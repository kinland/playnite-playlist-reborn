using Xunit;

namespace Playlist.UnitTests;

[CollectionDefinition(nameof(PlaylistLocalizationTestCollection))]
public class PlaylistLocalizationTestCollection
{
}

internal static class PlaylistTestLocalization
{
    internal static void Install()
    {
        PlaylistLocalization.TestGetString = key => key switch
        {
            "LOCPlaylist_HLTB_EmptyTime" => "--",
            "LOCPlaylist_Playtime_Minutes" => "{0} minutes",
            "LOCPlaylist_Playtime_HoursOnly" => "{0}h",
            "LOCPlaylist_Playtime_MinuteUnit" => "{0}m",
            "LOCPlaylist_Playtime_HoursMinutes" => "{0}h {1}m",
            "LOCPlaylist_LastPlayed_MomentsAgo" => "Moments ago",
            "LOCPlaylist_LastPlayed_OneMinuteAgo" => "{0} minute ago",
            "LOCPlaylist_LastPlayed_MinutesAgo" => "{0} minutes ago",
            "LOCPlaylist_LastPlayed_OneHourAgo" => "{0} hour ago",
            "LOCPlaylist_LastPlayed_HoursAgo" => "{0} hours ago",
            "LOCPlaylist_LastPlayed_OneDayAgo" => "{0} day ago",
            "LOCPlaylist_LastPlayed_DaysAgo" => "{0} days ago",
            "LOCPlaylist_LastPlayed_OneWeekAgo" => "{0} week ago",
            "LOCPlaylist_LastPlayed_WeeksAgo" => "{0} weeks ago",
            "LOCPlaylist_LastPlayed_OneMonthAgo" => "{0} month ago",
            "LOCPlaylist_LastPlayed_MonthsAgo" => "{0} months ago",
            "LOCPlaylist_LastPlayed_OneYearAgo" => "{0} year ago",
            "LOCPlaylist_LastPlayed_LongAgo" => "Long ago",
            _ => key,
        };
    }
}
