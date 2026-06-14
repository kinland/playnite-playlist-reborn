using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Windows;
using System.Windows.Media;

namespace Playlist
{
    public static class Playlist
    {
        static Playlist()
        {
            PlaylistLocalization.TestGetString = key => key switch
            {
                "LOCPlaylist_HLTB_EmptyTime" => "--",
                "LOCPlaylist_Playtime_HoursOnly" => "{0}h",
                "LOCPlaylist_Playtime_MinuteUnit" => "{0}m",
                "LOCPlaylist_Playtime_HoursMinutes" => "{0}h {1}m",
                "LOCTimePlayed" => "Played",
                _ => key,
            };
        }

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
        public Color FirstColor { get; set; }
        public Color SecondColor { get; set; }
        public Color ThirdColor { get; set; }
        public Color FirstMultiColor { get; set; }
        public Color SecondMultiColor { get; set; }
        public Color ThirdMultiColor { get; set; }
        public Brush FirstBrush { get; set; }
        public Brush SecondBrush { get; set; }
        public Brush ThirdBrush { get; set; }
        public Brush FirstMultiBrush { get; set; }
        public Brush SecondMultiBrush { get; set; }
        public Brush ThirdMultiBrush { get; set; }
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
            HltbRenderSettings settings = TestSettings ?? new HltbRenderSettings();
            EnsureDefaultSegmentBrushes(settings);
            return settings;
        }

        private static void EnsureDefaultSegmentBrushes(HltbRenderSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.FirstBrush ??= CreateDefaultBrush(settings.FirstColor, Colors.SteelBlue);
            settings.SecondBrush ??= CreateDefaultBrush(settings.SecondColor, Colors.MediumSeaGreen);
            settings.ThirdBrush ??= CreateDefaultBrush(settings.ThirdColor, Colors.Goldenrod);
            settings.FirstMultiBrush ??= CreateDefaultBrush(settings.FirstMultiColor, Colors.SteelBlue);
            settings.SecondMultiBrush ??= CreateDefaultBrush(settings.SecondMultiColor, Colors.MediumSeaGreen);
            settings.ThirdMultiBrush ??= CreateDefaultBrush(settings.ThirdMultiColor, Colors.Goldenrod);
        }

        private static SolidColorBrush CreateDefaultBrush(Color explicitColor, Color fallbackColor)
        {
            Color color = explicitColor.A != 0 ? explicitColor : fallbackColor;
            return new SolidColorBrush(color);
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

    internal enum HltbPreferredTimeType
    {
        MainStory,
        MainStoryExtra,
        Completionist,
        Solo,
        CoOp,
        Versus,
    }

    internal static class HltbColumnHeaderLabels
    {
        internal static string GetPreferredTimeTypeLabel(HltbPreferredTimeType type)
        {
            switch (type)
            {
                case HltbPreferredTimeType.MainStoryExtra:
                    return "Main + extra";
                case HltbPreferredTimeType.Completionist:
                    return "Completionist";
                case HltbPreferredTimeType.Solo:
                    return "Solo";
                case HltbPreferredTimeType.CoOp:
                    return "Co-op";
                case HltbPreferredTimeType.Versus:
                    return "Vs";
                case HltbPreferredTimeType.MainStory:
                default:
                    return "Main story";
            }
        }
    }

    internal static class HltbPlaytimeFormat
    {
        public static string FormatSeconds(long seconds, bool integrationViewItemOnlyHour, FrameworkElement themeScope)
        {
            if (seconds <= 0)
            {
                return PlaylistLocalization.GetString("LOCPlaylist_HLTB_EmptyTime");
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
