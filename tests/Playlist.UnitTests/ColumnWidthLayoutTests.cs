using System;
using System.Collections.Generic;
using Xunit;

namespace Playlist.UnitTests;

public class ColumnWidthLayoutTests
{
    [Fact]
    public void Distribute_FillsAvailableWidthOnWideList()
    {
        var visible = new[]
        {
            PlaylistColumnWidthLayout.RankColumnKey,
            PlaylistColumnWidthLayout.IconColumnKey,
            PlaylistColumnWidthLayout.LastPlayedColumnKey,
            PlaylistColumnWidthLayout.NameColumnKey,
            PlaylistColumnWidthLayout.PlaytimeColumnKey,
            PlaylistColumnWidthLayout.CompletionStatusColumnKey,
            PlaylistColumnWidthLayout.HowLongToBeatColumnKey,
            PlaylistColumnWidthLayout.LastActivityColumnKey,
        };

        var preferred = new Dictionary<string, double>
        {
            [PlaylistColumnWidthLayout.RankColumnKey] = 45.33,
            [PlaylistColumnWidthLayout.IconColumnKey] = 38,
            [PlaylistColumnWidthLayout.LastPlayedColumnKey] = 126,
            [PlaylistColumnWidthLayout.NameColumnKey] = 531.67,
            [PlaylistColumnWidthLayout.PlaytimeColumnKey] = 110.67,
            [PlaylistColumnWidthLayout.CompletionStatusColumnKey] = 142.33,
            [PlaylistColumnWidthLayout.HowLongToBeatColumnKey] = 400,
            [PlaylistColumnWidthLayout.LastActivityColumnKey] = 124,
        };

        IReadOnlyDictionary<string, double> widths = PlaylistColumnWidthLayout.Distribute(1800, visible, preferred);

        Assert.Equal(1800, widths.Values.Sum(), 1);
        Assert.True(widths[PlaylistColumnWidthLayout.NameColumnKey] > preferred[PlaylistColumnWidthLayout.NameColumnKey]);
        Assert.True(widths[PlaylistColumnWidthLayout.HowLongToBeatColumnKey] > preferred[PlaylistColumnWidthLayout.HowLongToBeatColumnKey]);
        double playtimeBase = Math.Max(
            PlaylistColumnWidthLayout.GetMinimumWidth(PlaylistColumnWidthLayout.PlaytimeColumnKey),
            preferred[PlaylistColumnWidthLayout.PlaytimeColumnKey]);
        Assert.InRange(
            widths[PlaylistColumnWidthLayout.PlaytimeColumnKey] - playtimeBase,
            0,
            PlaylistColumnWidthLayout.NarrowColumnBonusPadding + 0.01);
    }

    [Fact]
    public void Distribute_SendsMostExtraWidthToFlexColumns()
    {
        var visible = new[]
        {
            PlaylistColumnWidthLayout.IconColumnKey,
            PlaylistColumnWidthLayout.NameColumnKey,
            PlaylistColumnWidthLayout.PlaytimeColumnKey,
            PlaylistColumnWidthLayout.HowLongToBeatColumnKey,
        };

        var preferred = new Dictionary<string, double>
        {
            [PlaylistColumnWidthLayout.NameColumnKey] = 300,
            [PlaylistColumnWidthLayout.PlaytimeColumnKey] = 111,
            [PlaylistColumnWidthLayout.HowLongToBeatColumnKey] = 400,
        };

        IReadOnlyDictionary<string, double> widths = PlaylistColumnWidthLayout.Distribute(1400, visible, preferred);

        double playtimeGain = widths[PlaylistColumnWidthLayout.PlaytimeColumnKey] - preferred[PlaylistColumnWidthLayout.PlaytimeColumnKey];
        double nameGain = widths[PlaylistColumnWidthLayout.NameColumnKey] - preferred[PlaylistColumnWidthLayout.NameColumnKey];
        double hltbGain = widths[PlaylistColumnWidthLayout.HowLongToBeatColumnKey] - preferred[PlaylistColumnWidthLayout.HowLongToBeatColumnKey];

        Assert.True(nameGain > playtimeGain * 2);
        Assert.True(hltbGain > playtimeGain * 2);
        Assert.Equal(1400, widths.Values.Sum(), 1);
    }

    [Fact]
    public void Distribute_ScalesDownWhenSpaceIsTight()
    {
        var visible = new[]
        {
            PlaylistColumnWidthLayout.IconColumnKey,
            PlaylistColumnWidthLayout.NameColumnKey,
            PlaylistColumnWidthLayout.HowLongToBeatColumnKey,
        };

        IReadOnlyDictionary<string, double> widths = PlaylistColumnWidthLayout.Distribute(300, visible, null);

        Assert.Equal(300, widths.Values.Sum(), 1);
        Assert.Equal(PlaylistGridViewLayout.IconColumnWidth, widths[PlaylistColumnWidthLayout.IconColumnKey]);
        Assert.True(widths[PlaylistColumnWidthLayout.NameColumnKey] >= PlaylistColumnWidthLayout.GetMinimumWidth(PlaylistColumnWidthLayout.NameColumnKey) * 0.5);
    }
}
