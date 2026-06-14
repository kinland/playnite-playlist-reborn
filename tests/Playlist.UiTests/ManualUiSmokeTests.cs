using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace Playlist.UiTests;

public class ManualUiSmokeTests
{
    [Fact]
    public void ProgressBar_InitializesRoundedPlaytimeMarker()
    {
        StaUiTest.Run(() =>
        {
            global::Playlist.HowLongToBeatCache.Reset();
            var control = new global::Playlist.HowLongToBeatCachedProgressBar();
            Border marker = GetPrivateField<Border>(control, "playtimeMarker");

            Assert.Equal(12, marker.Width);
            Assert.Equal(30, marker.Height);
            Assert.Equal(new CornerRadius(2), marker.CornerRadius);
            Assert.Equal(Visibility.Collapsed, marker.Visibility);
        });
    }

    [Fact]
    public void ProgressBar_NonGameDataContext_ShowsUnknownLabel()
    {
        StaUiTest.Run(() =>
        {
            global::Playlist.HowLongToBeatCache.Reset();
            var control = new global::Playlist.HowLongToBeatCachedProgressBar
            {
                DataContext = new object()
            };

            InvokeRefresh(control);

            TextBlock empty = GetPrivateField<TextBlock>(control, "emptyLabel");
            Border marker = GetPrivateField<Border>(control, "playtimeMarker");

            Assert.Equal(Visibility.Visible, empty.Visibility);
            Assert.Equal("--", empty.Text);
            Assert.Equal(Visibility.Collapsed, marker.Visibility);
        });
    }

    [Fact]
    public void ProgressBar_GameData_RendersSegmentsAndTooltip()
    {
        StaUiTest.Run(() =>
        {
            global::Playlist.HowLongToBeatCache.Reset();
            global::Playlist.HowLongToBeatCache.TestSettings = new global::Playlist.HltbRenderSettings
            {
                EnableIntegrationViewItem = true,
                EnableIntegrationProgressBar = true,
                ProgressBarShowToolTip = true,
                ShowMainTime = true,
                ShowExtraTime = true,
                ShowCompletionistTime = true
            };
            global::Playlist.HowLongToBeatCache.CachedTimesResolver = _ => new global::Playlist.HltbCachedTimes
            {
                Url = "https://howlongtobeat.com/game?id=123",
                MainStory = new global::Playlist.HltbTimeVariants { Classic = 3600 },
                MainExtra = new global::Playlist.HltbTimeVariants { Classic = 7200 },
                Completionist = new global::Playlist.HltbTimeVariants { Classic = 10800 }
            };

            var control = new global::Playlist.HowLongToBeatCachedProgressBar
            {
                Width = 500,
                DataContext = new Playnite.SDK.Models.Game { Id = System.Guid.NewGuid(), Playtime = 1800 }
            };

            PrepareLayout(control);
            InvokeRefresh(control);

            StackPanel strip = GetPrivateField<StackPanel>(control, "segmentStrip");
            TextBlock empty = GetPrivateField<TextBlock>(control, "emptyLabel");

            Assert.Equal(3, strip.Children.Count);
            Assert.Null(control.ToolTip);

            var firstSegment = Assert.IsType<Border>(strip.Children[0]);
            string segmentToolTip = Assert.IsType<string>(firstSegment.ToolTip);
            Assert.Contains("(", segmentToolTip);
            Assert.Contains(")", segmentToolTip);

            Border marker = GetPrivateField<Border>(control, "playtimeMarker");
            string markerToolTip = Assert.IsType<string>(marker.ToolTip);
            Assert.Contains("(", markerToolTip);
            Assert.Contains(")", markerToolTip);
            Assert.Equal(Visibility.Collapsed, empty.Visibility);
        });
    }

    [Fact]
    public void ProgressBar_LabelPlacement_UsesAboveAndBelowStrips()
    {
        StaUiTest.Run(() =>
        {
            global::Playlist.HowLongToBeatCache.Reset();
            global::Playlist.HowLongToBeatCache.TestSettings = new global::Playlist.HltbRenderSettings
            {
                EnableIntegrationViewItem = true,
                EnableIntegrationProgressBar = true,
                ProgressBarShowTime = true,
                ProgressBarShowTimeInterior = false,
                ProgressBarShowTimeAbove = true,
                ProgressBarShowTimeBelow = true,
                ShowMainTime = true,
                ShowExtraTime = false,
                ShowCompletionistTime = false
            };
            global::Playlist.HowLongToBeatCache.CachedTimesResolver = _ => new global::Playlist.HltbCachedTimes
            {
                MainStory = new global::Playlist.HltbTimeVariants { Classic = 5400 }
            };

            var control = new global::Playlist.HowLongToBeatCachedProgressBar
            {
                Width = 500,
                DataContext = new Playnite.SDK.Models.Game { Id = System.Guid.NewGuid(), Playtime = 1000 }
            };

            PrepareLayout(control);
            InvokeRefresh(control);

            StackPanel top = GetPrivateField<StackPanel>(control, "topLabelStrip");
            StackPanel bottom = GetPrivateField<StackPanel>(control, "bottomLabelStrip");
            Canvas interior = GetPrivateField<Canvas>(control, "interiorLabelStrip");
            StackPanel strip = GetPrivateField<StackPanel>(control, "segmentStrip");
            Border firstSegment = Assert.IsType<Border>(strip.Children[0]);

            Assert.Equal(Visibility.Visible, top.Visibility);
            Assert.Equal(Visibility.Visible, bottom.Visibility);
            Assert.Equal(Visibility.Collapsed, interior.Visibility);
            Assert.Single(top.Children);
            Assert.Single(bottom.Children);
            Assert.Null(firstSegment.Child);
        });
    }

    [Fact]
    public void ProgressBar_LabelPlacement_UsesInteriorCanvas()
    {
        StaUiTest.Run(() =>
        {
            global::Playlist.HowLongToBeatCache.Reset();
            global::Playlist.HowLongToBeatCache.TestSettings = new global::Playlist.HltbRenderSettings
            {
                EnableIntegrationViewItem = true,
                EnableIntegrationProgressBar = true,
                ProgressBarShowTime = true,
                ProgressBarShowTimeInterior = true,
                ProgressBarShowTimeAbove = false,
                ProgressBarShowTimeBelow = false,
                ShowMainTime = true,
                ShowExtraTime = true,
                ShowCompletionistTime = false,
                IntegrationViewItemOnlyHour = false
            };
            global::Playlist.HowLongToBeatCache.CachedTimesResolver = _ => new global::Playlist.HltbCachedTimes
            {
                MainStory = new global::Playlist.HltbTimeVariants { Classic = 3600 },
                MainExtra = new global::Playlist.HltbTimeVariants { Classic = 7200 }
            };

            var control = new global::Playlist.HowLongToBeatCachedProgressBar
            {
                Width = 500,
                DataContext = new Playnite.SDK.Models.Game { Id = System.Guid.NewGuid(), Playtime = 1000 }
            };

            PrepareLayout(control);
            InvokeRefresh(control);

            Canvas interior = GetPrivateField<Canvas>(control, "interiorLabelStrip");
            StackPanel strip = GetPrivateField<StackPanel>(control, "segmentStrip");

            Assert.Equal(Visibility.Visible, interior.Visibility);
            Assert.Equal(2, interior.Children.Count);
            Assert.All(strip.Children.Cast<Border>(), border => Assert.Null(border.Child));
        });
    }

    [Fact]
    public void ProgressBar_UsesConfiguredSegmentBrush()
    {
        StaUiTest.Run(() =>
        {
            global::Playlist.HowLongToBeatCache.Reset();
            var gradient = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(10, 20, 30), 0),
                    new GradientStop(Color.FromRgb(40, 50, 60), 1)
                },
                new Point(0, 0),
                new Point(1, 0));

            global::Playlist.HowLongToBeatCache.TestSettings = new global::Playlist.HltbRenderSettings
            {
                EnableIntegrationViewItem = true,
                EnableIntegrationProgressBar = true,
                ShowMainTime = true,
                ShowExtraTime = false,
                ShowCompletionistTime = false,
                FirstBrush = gradient
            };
            global::Playlist.HowLongToBeatCache.CachedTimesResolver = _ => new global::Playlist.HltbCachedTimes
            {
                MainStory = new global::Playlist.HltbTimeVariants { Classic = 3600 }
            };

            var control = new global::Playlist.HowLongToBeatCachedProgressBar
            {
                Width = 500,
                DataContext = new Playnite.SDK.Models.Game { Id = System.Guid.NewGuid(), Playtime = 2000 }
            };

            PrepareLayout(control);
            InvokeRefresh(control);

            StackPanel strip = GetPrivateField<StackPanel>(control, "segmentStrip");
            Border firstSegment = Assert.IsType<Border>(strip.Children[0]);
            Assert.IsType<LinearGradientBrush>(firstSegment.Background);
        });
    }

    [Fact]
    public void ProgressBar_DisabledIntegration_CollapsesControl()
    {
        StaUiTest.Run(() =>
        {
            global::Playlist.HowLongToBeatCache.Reset();
            global::Playlist.HowLongToBeatCache.TestSettings = new global::Playlist.HltbRenderSettings
            {
                EnableIntegrationViewItem = true,
                EnableIntegrationProgressBar = false
            };
            global::Playlist.HowLongToBeatCache.CachedTimesResolver = _ => new global::Playlist.HltbCachedTimes
            {
                MainStory = new global::Playlist.HltbTimeVariants { Classic = 3600 }
            };

            var control = new global::Playlist.HowLongToBeatCachedProgressBar
            {
                Width = 500,
                DataContext = new Playnite.SDK.Models.Game { Id = System.Guid.NewGuid(), Playtime = 1000 }
            };

            PrepareLayout(control);
            InvokeRefresh(control);
            Assert.Equal(Visibility.Collapsed, control.Visibility);
        });
    }

    private static void InvokeRefresh(global::Playlist.HowLongToBeatCachedProgressBar control)
    {
        MethodInfo refresh = typeof(global::Playlist.HowLongToBeatCachedProgressBar)
            .GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
        refresh.Invoke(control, null);
    }

    private static void PrepareLayout(global::Playlist.HowLongToBeatCachedProgressBar control)
    {
        control.Measure(new Size(500, 80));
        control.Arrange(new Rect(0, 0, 500, 80));
        control.UpdateLayout();
    }

    private static T GetPrivateField<T>(object instance, string fieldName) where T : class
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field.GetValue(instance) as T;
    }
}
