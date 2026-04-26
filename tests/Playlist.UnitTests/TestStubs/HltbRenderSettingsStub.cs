using System.Windows.Media;

namespace Playlist;

// Test-only minimal shape required by HltbSettingsJson and HltbPlaytimeFormat.
internal sealed class HltbRenderSettings
{
    public bool IntegrationViewItemOnlyHour { get; set; }
    public bool UseClassic { get; set; }
    public bool UseAverage { get; set; }
    public bool UseMedian { get; set; }
    public bool UseRushed { get; set; }
    public bool UseLeisure { get; set; }
    public bool EnableIntegrationViewItem { get; set; }
    public bool EnableIntegrationButton { get; set; }
    public bool EnableIntegrationProgressBar { get; set; }
    public bool ShowMainTime { get; set; }
    public bool ShowExtraTime { get; set; }
    public bool ShowCompletionistTime { get; set; }
    public bool ShowSoloTime { get; set; }
    public bool ShowCoOpTime { get; set; }
    public bool ShowVsTime { get; set; }
    public bool ProgressBarShowTime { get; set; }
    public bool ProgressBarShowTimeInterior { get; set; }
    public bool ProgressBarShowTimeAbove { get; set; }
    public bool ProgressBarShowTimeBelow { get; set; }
    public bool ProgressBarShowToolTip { get; set; }
    public Color? ThumbPlaytimeColor { get; set; }
    public Brush ThumbPlaytimeBrush { get; set; }
    public Color FirstColor { get; set; }
    public Brush FirstBrush { get; set; }
    public Color SecondColor { get; set; }
    public Brush SecondBrush { get; set; }
    public Color ThirdColor { get; set; }
    public Brush ThirdBrush { get; set; }
    public Color FirstMultiColor { get; set; }
    public Brush FirstMultiBrush { get; set; }
    public Color SecondMultiColor { get; set; }
    public Brush SecondMultiBrush { get; set; }
    public Color ThirdMultiColor { get; set; }
    public Brush ThirdMultiBrush { get; set; }

    public static HltbRenderSettings CreateDefaults()
    {
        return new HltbRenderSettings
        {
            EnableIntegrationViewItem = true,
            EnableIntegrationButton = true,
            EnableIntegrationProgressBar = true,
            UseClassic = true,
            ShowMainTime = true,
            ShowExtraTime = true,
            ShowCompletionistTime = true,
            ShowSoloTime = true,
            ShowCoOpTime = true,
            ShowVsTime = true,
            FirstColor = Colors.DarkCyan,
            SecondColor = Colors.RoyalBlue,
            ThirdColor = Colors.ForestGreen,
            FirstMultiColor = Colors.DarkCyan,
            SecondMultiColor = Colors.RoyalBlue,
            ThirdMultiColor = Colors.ForestGreen,
            FirstBrush = new SolidColorBrush(Colors.DarkCyan),
            SecondBrush = new SolidColorBrush(Colors.RoyalBlue),
            ThirdBrush = new SolidColorBrush(Colors.ForestGreen),
            FirstMultiBrush = new SolidColorBrush(Colors.DarkCyan),
            SecondMultiBrush = new SolidColorBrush(Colors.RoyalBlue),
            ThirdMultiBrush = new SolidColorBrush(Colors.ForestGreen),
        };
    }
}
