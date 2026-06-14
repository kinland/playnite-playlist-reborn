using Xunit;

namespace Playlist.UnitTests;

public class HltbInteriorLabelOverlapTests
{
    [Fact]
    public void SuppressOverlapping_hides_narrower_label_when_centers_collide()
    {
        var plans = new[]
        {
            new HltbInteriorLabelOverlap.LabelPlan(centerX: 100, sliceWidth: 80),
            new HltbInteriorLabelOverlap.LabelPlan(centerX: 110, sliceWidth: 12),
        };
        var labelWidths = new[] { 40.0, 40.0 };
        var show = new[] { true, true };

        HltbInteriorLabelOverlap.SuppressOverlapping(plans, labelWidths, show);

        Assert.True(show[0]);
        Assert.False(show[1]);
    }

    [Fact]
    public void SuppressOverlapping_keeps_both_labels_when_far_enough_apart()
    {
        var plans = new[]
        {
            new HltbInteriorLabelOverlap.LabelPlan(centerX: 50, sliceWidth: 40),
            new HltbInteriorLabelOverlap.LabelPlan(centerX: 200, sliceWidth: 40),
        };
        var labelWidths = new[] { 30.0, 30.0 };
        var show = new[] { true, true };

        HltbInteriorLabelOverlap.SuppressOverlapping(plans, labelWidths, show);

        Assert.True(show[0]);
        Assert.True(show[1]);
    }

    [Fact]
    public void SuppressOverlapping_hides_first_label_when_it_is_narrower()
    {
        var plans = new[]
        {
            new HltbInteriorLabelOverlap.LabelPlan(centerX: 110, sliceWidth: 10),
            new HltbInteriorLabelOverlap.LabelPlan(centerX: 100, sliceWidth: 80),
        };
        var labelWidths = new[] { 40.0, 40.0 };
        var show = new[] { true, true };

        HltbInteriorLabelOverlap.SuppressOverlapping(plans, labelWidths, show);

        Assert.False(show[0]);
        Assert.True(show[1]);
    }
}
