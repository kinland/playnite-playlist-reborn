using Xunit;

namespace Playlist.UnitTests;

public class PlaylistDragReorderMessagesTests
{
    public PlaylistDragReorderMessagesTests()
    {
        PlaylistDragReorderMessages.TestGetString = key => key switch
        {
            "LOCPlaylist_DragReorderBlocked_SortActive" => "Reorder is disabled while sorted by {0}. Sort by {1} to reorder.",
            "LOCPlaylist_DragReorderBlocked_Bucket" => "Reorder only within the same time group ({0}).",
            "LOCPlaylist_Column_Rank" => "Rank (#)",
            "LOCTimePlayed" => "Time Played",
            _ => key,
        };
    }

    [Fact]
    public void BuildSortBlockedMessage_includes_active_sort_and_rank_column()
    {
        string message = PlaylistDragReorderMessages.BuildSortBlockedMessage("Playtime");
        Assert.Equal("Reorder is disabled while sorted by Time Played. Sort by Rank (#) to reorder.", message);
    }

    [Fact]
    public void BuildBucketBlockedMessage_includes_bucket_label()
    {
        string message = PlaylistDragReorderMessages.BuildBucketBlockedMessage("3 days ago");
        Assert.Equal("Reorder only within the same time group (3 days ago).", message);
    }
}
