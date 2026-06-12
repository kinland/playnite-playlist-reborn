using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Windows;
using System.Windows.Media;

namespace Playlist
{
    public static class Playlist
    {
        public static IPlayniteAPI StaticPlayniteApi { get; set; }
        public static HltbSettingsStub StaticSettings { get; set; } = new HltbSettingsStub();
    }

    public sealed class HltbSettingsStub
    {
        public bool EnableHowLongToBeatIntegration { get; set; } = true;
    }

    internal sealed class HltbRenderSettings
    {
        public bool EnableIntegrationViewItem { get; set; } = true;
        public bool EnableIntegrationProgressBar { get; set; } = true;
        public bool EnableIntegrationButton { get; set; } = true;
        public bool IntegrationViewItemOnlyHour { get; set; }
        public bool ProgressBarShowTime { get; set; }
        public bool ProgressBarShowTimeInterior { get; set; }
        public bool ProgressBarShowTimeAbove { get; set; }
        public bool ProgressBarShowTimeBelow { get; set; }
        public bool ProgressBarShowToolTip { get; set; } = true;
        public bool ShowMainTime { get; set; } = true;
        public bool ShowExtraTime { get; set; } = true;
        public bool ShowCompletionistTime { get; set; } = true;
        public bool ShowSoloTime { get; set; } = true;
        public bool ShowCoOpTime { get; set; } = true;
        public bool ShowVsTime { get; set; } = true;
        public bool UseClassic { get; set; } = true;
        public bool UseAverage { get; set; }
        public bool UseMedian { get; set; }
        public bool UseRushed { get; set; }
        public bool UseLeisure { get; set; }
        public Color FirstColor { get; set; } = Colors.DarkCyan;
        public Color SecondColor { get; set; } = Colors.RoyalBlue;
        public Color ThirdColor { get; set; } = Colors.ForestGreen;
        public Color FirstMultiColor { get; set; } = Colors.DarkCyan;
        public Color SecondMultiColor { get; set; } = Colors.RoyalBlue;
        public Color ThirdMultiColor { get; set; } = Colors.ForestGreen;
        public Brush FirstBrush { get; set; } = new SolidColorBrush(Colors.DarkCyan);
        public Brush SecondBrush { get; set; } = new SolidColorBrush(Colors.RoyalBlue);
        public Brush ThirdBrush { get; set; } = new SolidColorBrush(Colors.ForestGreen);
        public Brush FirstMultiBrush { get; set; } = new SolidColorBrush(Colors.DarkCyan);
        public Brush SecondMultiBrush { get; set; } = new SolidColorBrush(Colors.RoyalBlue);
        public Brush ThirdMultiBrush { get; set; } = new SolidColorBrush(Colors.ForestGreen);
        public Color? ThumbPlaytimeColor { get; set; }
        public Brush ThumbPlaytimeBrush { get; set; }
    }

    internal sealed class HltbCachedTimes
    {
        public int GameType { get; set; }
        public HltbTimeVariants MainStory { get; set; }
        public HltbTimeVariants MainExtra { get; set; }
        public HltbTimeVariants Completionist { get; set; }
        public HltbTimeVariants Solo { get; set; }
        public HltbTimeVariants CoOp { get; set; }
        public HltbTimeVariants Vs { get; set; }
        public string Url { get; set; }
    }

    internal sealed class HltbTimeVariants
    {
        public long Classic { get; set; }
        public long Median { get; set; }
        public long Average { get; set; }
        public long Rushed { get; set; }
        public long Leisure { get; set; }
    }

    internal static class HowLongToBeatCache
    {
        public static HltbRenderSettings TestSettings { get; set; } = new HltbRenderSettings();
        public static Func<Game, HltbCachedTimes> CachedTimesResolver { get; set; }

        public static void Reset()
        {
            TestSettings = new HltbRenderSettings();
            CachedTimesResolver = null;
        }

        public static HltbRenderSettings GetRenderSettings(IPlayniteAPI api)
        {
            return TestSettings ?? new HltbRenderSettings();
        }

        public static bool TryGetCachedTimes(IPlayniteAPI api, Game game, out HltbCachedTimes times)
        {
            if (CachedTimesResolver == null)
            {
                times = null;
                return false;
            }

            times = CachedTimesResolver(game);
            return times != null;
        }
    }

    internal static class HltbPlaytimeFormat
    {
        public static string FormatSeconds(long seconds, bool integrationViewItemOnlyHour, FrameworkElement themeScope)
        {
            if (seconds <= 0)
            {
                return "--";
            }

            if (integrationViewItemOnlyHour)
            {
                long h = (long)Math.Round(seconds / 3600.0, MidpointRounding.AwayFromZero);
                if (h <= 0)
                {
                    h = 1;
                }

                return h + "h";
            }

            long totalMinutes = seconds / 60;
            return (totalMinutes / 60) + "h " + (totalMinutes % 60) + "m";
        }
    }
}
