using System;
using Xunit;

namespace Playlist.UnitTests;

[Collection(nameof(PlaylistLocalizationTestCollection))]
public class CompletionStatusLocalizationTests
{
    [Fact]
    public void LocalizeDisplayName_uses_override_for_default_status_names()
    {
        Func<string, string> previous = PlaylistLocalization.TestGetString;
        try
        {
            PlaylistLocalization.TestGetString = key => key switch
            {
                "LOCCompletionStatusPlayed" => "Ua pāʻani ʻia",
                "LOCCompletionStatusNotPlayed" => "ʻAʻole i pāʻani ʻia",
                _ => key,
            };

            Assert.Equal("Ua pāʻani ʻia", CompletionStatusLocalization.LocalizeDisplayName("Played"));
            Assert.Equal("ʻAʻole i pāʻani ʻia", CompletionStatusLocalization.LocalizeDisplayName("Not Played"));
        }
        finally
        {
            PlaylistLocalization.TestGetString = previous;
        }
    }

    [Fact]
    public void LocalizeDisplayName_returns_custom_status_names_unchanged()
    {
        Func<string, string> previous = PlaylistLocalization.TestGetString;
        try
        {
            PlaylistLocalization.TestGetString = key => key;

            Assert.Equal("Endless", CompletionStatusLocalization.LocalizeDisplayName("Endless"));
        }
        finally
        {
            PlaylistLocalization.TestGetString = previous;
        }
    }
}
