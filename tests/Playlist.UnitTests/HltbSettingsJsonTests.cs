using System.Windows.Media;
using Xunit;

namespace Playlist.UnitTests;

public class HltbSettingsJsonTests
{
    [Fact]
    public void MergeInto_ParsesCommonBooleansAndSolidBrushes()
    {
        string json = """
        {
          "EnableIntegrationViewItem": true,
          "EnableIntegrationButton": false,
          "EnableIntegrationProgressBar": true,
          "ProgressBarShowTime": true,
          "ProgressBarShowTimeInterior": false,
          "ProgressBarShowTimeAbove": true,
          "ProgressBarShowTimeBelow": false,
          "ProgressBarShowToolTip": true,
          "FirstColorBrush": "#FF102030",
          "ThumbSolidColorBrush": "#FF304050"
        }
        """;

        HltbRenderSettings target = HltbRenderSettings.CreateDefaults();

        HltbSettingsJson.MergeInto(json, target);

        Assert.True(target.EnableIntegrationViewItem);
        Assert.False(target.EnableIntegrationButton);
        Assert.True(target.EnableIntegrationProgressBar);
        Assert.True(target.ProgressBarShowTime);
        Assert.False(target.ProgressBarShowTimeInterior);
        Assert.True(target.ProgressBarShowTimeAbove);
        Assert.False(target.ProgressBarShowTimeBelow);
        Assert.True(target.ProgressBarShowToolTip);
        Assert.Equal(Color.FromArgb(0xFF, 0x10, 0x20, 0x30), target.FirstColor);
        Assert.Equal(Color.FromArgb(0xFF, 0x30, 0x40, 0x50), target.ThumbPlaytimeColor);
        Assert.IsType<SolidColorBrush>(target.FirstBrush);
    }

    [Fact]
    public void MergeInto_ParsesLinearGradientBrush()
    {
        string json = """
        {
          "FirstLinearGradient": {
            "StartPoint": { "X": 0, "Y": 0 },
            "EndPoint": { "X": 1, "Y": 0 },
            "GradientStops": [
              { "Color": "#FF112233", "Offset": 0.0 },
              { "Color": "#FF445566", "Offset": 1.0 }
            ]
          }
        }
        """;

        HltbRenderSettings target = HltbRenderSettings.CreateDefaults();

        HltbSettingsJson.MergeInto(json, target);

        LinearGradientBrush gradient = Assert.IsType<LinearGradientBrush>(target.FirstBrush);
        Assert.Equal(2, gradient.GradientStops.Count);
        Assert.Equal(Color.FromArgb(0xFF, 0x11, 0x22, 0x33), gradient.GradientStops[0].Color);
    }
}
