using System.Windows.Media;

namespace Playlist;

// Test-only minimal shape required by HltbSettingsJson and HltbPlaytimeFormat.
internal enum HltbPreferredTimeType
{
    MainStory = 0,
    MainStoryExtra = 1,
    Completionist = 2,
    Solo = 3,
    CoOp = 4,
    Versus = 5,
}

internal sealed class HltbRenderSettings
{
    public HltbPreferredTimeType PreferredForTimeToBeat { get; set; }
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
            PreferredForTimeToBeat = HltbPreferredTimeType.MainStory,
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
        };
    }
}

// Test-only minimal shapes required by HltbSortKeyBuilder tests.
internal sealed class HltbCachedTimes
{
    public HltbTimeVariants MainStory { get; set; }
    public HltbTimeVariants MainExtra { get; set; }
    public HltbTimeVariants Completionist { get; set; }
    public HltbTimeVariants Solo { get; set; }
    public HltbTimeVariants CoOp { get; set; }
    public HltbTimeVariants Vs { get; set; }
}

internal sealed class HltbTimeVariants
{
    public long Classic { get; set; }
    public long Median { get; set; }
    public long Average { get; set; }
    public long Rushed { get; set; }
    public long Leisure { get; set; }
}
