using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Media;

namespace Playlist
{
    internal enum HltbPreferredTimeType
    {
        MainStory = 0,
        MainStoryExtra = 1,
        Completionist = 2,
        Solo = 3,
        CoOp = 4,
        Versus = 5,
    }

    internal static class HowLongToBeatCache
    {
        private static readonly Guid HltbPluginId = Guid.Parse("e08cd51f-9c9a-4ee3-a094-fde03b55492f");
        private static readonly ConcurrentDictionary<Guid, HltbCachedTimes> CachedTimesByGameId = new ConcurrentDictionary<Guid, HltbCachedTimes>();
        private static readonly ConcurrentDictionary<Guid, bool> MissingCacheByGameId = new ConcurrentDictionary<Guid, bool>();
        private static HltbRenderSettings cachedSettings;
        private static string cachedSettingsPath;
        private static long cachedSettingsFileUtcTicks;

        /// <summary>
        /// Clears cached HLTB render settings so the next read reloads from disk (e.g. after HLTB settings save or playlist tab reopen).
        /// </summary>
        public static void InvalidateRenderSettingsCache()
        {
            cachedSettings = null;
            cachedSettingsPath = null;
            cachedSettingsFileUtcTicks = 0;
        }

        public static bool IsPluginLoaded(IPlayniteAPI api)
        {
            if (api?.Addons?.Plugins == null)
            {
                return false;
            }

            return api.Addons.Plugins.Any(p => p.Id == HltbPluginId);
        }

        /// <summary>Reads per-game HLTB JSON from the plugin cache; failures are cached as absent.</summary>
        public static bool TryGetCachedTimes(IPlayniteAPI api, Game game, out HltbCachedTimes times)
        {
            times = null;
            if (game == null)
            {
                return false;
            }

            if (CachedTimesByGameId.TryGetValue(game.Id, out times) && times != null)
            {
                return true;
            }

            if (MissingCacheByGameId.ContainsKey(game.Id))
            {
                return false;
            }

            string dbPath = GetHltbDatabasePath(api);
            if (string.IsNullOrEmpty(dbPath))
            {
                MissingCacheByGameId[game.Id] = true;
                return false;
            }

            string filePath = Path.Combine(dbPath, game.Id + ".json");
            if (!File.Exists(filePath))
            {
                MissingCacheByGameId[game.Id] = true;
                return false;
            }

            try
            {
                using (FileStream fs = HltbCacheFileAccess.OpenForSharedRead(filePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(HltbGameFile));
                    var data = serializer.ReadObject(fs) as HltbGameFile;
                    HltbGameData gameData = data?.Items != null && data.Items.Count > 0
                        ? data.Items[0]?.GameHltbData
                        : null;
                    if (gameData == null)
                    {
                        MissingCacheByGameId[game.Id] = true;
                        return false;
                    }

                    times = new HltbCachedTimes
                    {
                        GameType = data.Items[0]?.GameType ?? 0,
                        MainStory = new HltbTimeVariants
                        {
                            Classic = gameData.MainStoryClassic,
                            Median = gameData.MainStoryMedian,
                            Average = gameData.MainStoryAverage,
                            Rushed = gameData.MainStoryRushed,
                            Leisure = gameData.MainStoryLeisure,
                        },
                        MainExtra = new HltbTimeVariants
                        {
                            Classic = gameData.MainExtraClassic,
                            Median = gameData.MainExtraMedian,
                            Average = gameData.MainExtraAverage,
                            Rushed = gameData.MainExtraRushed,
                            Leisure = gameData.MainExtraLeisure,
                        },
                        Completionist = new HltbTimeVariants
                        {
                            Classic = gameData.CompletionistClassic,
                            Median = gameData.CompletionistMedian,
                            Average = gameData.CompletionistAverage,
                            Rushed = gameData.CompletionistRushed,
                            Leisure = gameData.CompletionistLeisure,
                        },
                        Solo = new HltbTimeVariants
                        {
                            Classic = gameData.SoloClassic,
                            Median = gameData.SoloMedian,
                            Average = gameData.SoloAverage,
                            Rushed = gameData.SoloRushed,
                            Leisure = gameData.SoloLeisure,
                        },
                        CoOp = new HltbTimeVariants
                        {
                            Classic = gameData.CoOpClassic,
                            Median = gameData.CoOpMedian,
                            Average = gameData.CoOpAverage,
                            Rushed = gameData.CoOpRushed,
                            Leisure = gameData.CoOpLeisure,
                        },
                        Vs = new HltbTimeVariants
                        {
                            Classic = gameData.VsClassic,
                            Median = gameData.VsMedian,
                            Average = gameData.VsAverage,
                            Rushed = gameData.VsRushed,
                            Leisure = gameData.VsLeisure,
                        },
                        Url = data.Items[0]?.Url,
                    };

                    CachedTimesByGameId[game.Id] = times;
                    return true;
                }
            }
            catch
            {
                MissingCacheByGameId[game.Id] = true;
                return false;
            }
        }

        /// <summary>Loads HLTB appearance settings, with in-memory caching keyed on file write time.</summary>
        public static HltbRenderSettings GetRenderSettings(IPlayniteAPI api)
        {
            string settingsPath = GetHltbSettingsPath(api);
            if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath))
            {
                cachedSettings = HltbRenderSettings.CreateDefaults();
                cachedSettingsPath = null;
                cachedSettingsFileUtcTicks = 0;
                return cachedSettings;
            }

            long ticks = File.GetLastWriteTimeUtc(settingsPath).Ticks;
            if (cachedSettings != null
                && string.Equals(settingsPath, cachedSettingsPath, StringComparison.OrdinalIgnoreCase)
                && ticks == cachedSettingsFileUtcTicks)
            {
                return cachedSettings;
            }

            try
            {
                string json = ReadCacheTextAllowingWriter(settingsPath);
                HltbRenderSettings merged = HltbRenderSettings.CreateDefaults();
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    using (var fs = new MemoryStream(bytes))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(HltbSettingsRoot));
                        var root = serializer.ReadObject(fs) as HltbSettingsRoot;
                        HltbSerializableSettings s = root?.Settings;
                        if (s == null)
                        {
                            fs.Position = 0;
                            var directSerializer = new DataContractJsonSerializer(typeof(HltbSerializableSettings));
                            s = directSerializer.ReadObject(fs) as HltbSerializableSettings;
                        }

                        if (s != null)
                        {
                            HltbRenderSettings.FromSerializableInto(s, merged);
                        }
                    }
                }
                catch
                {
                }

                HltbSettingsJson.MergeInto(json, merged);

                cachedSettings = merged;
                cachedSettingsPath = settingsPath;
                cachedSettingsFileUtcTicks = ticks;
                return cachedSettings;
            }
            catch
            {
                cachedSettings = HltbRenderSettings.CreateDefaults();
                cachedSettingsPath = settingsPath;
                cachedSettingsFileUtcTicks = ticks;
                return cachedSettings;
            }
        }

        private static string ReadCacheTextAllowingWriter(string filePath)
        {
            return HltbCacheFileAccess.ReadTextAllowingWriter(filePath);
        }

        private static string GetHltbDatabasePath(IPlayniteAPI api)
        {
            try
            {
                // Build from this plugin's user-data path: .../ExtensionsData/{this-plugin-id}/
                // => sibling HLTB folder .../ExtensionsData/{hltb-plugin-id}/HowLongToBeat
                string thisPluginPath = Playlist.StaticPluginUserDataPath;
                if (string.IsNullOrEmpty(thisPluginPath))
                {
                    return null;
                }

                string extensionsDataPath = Directory.GetParent(thisPluginPath)?.FullName;
                if (string.IsNullOrEmpty(extensionsDataPath))
                {
                    return null;
                }

                string pluginUserDataPath = Path.Combine(extensionsDataPath, HltbPluginId.ToString());
                return Path.Combine(pluginUserDataPath, "HowLongToBeat");
            }
            catch
            {
                return null;
            }
        }

        private static string GetHltbSettingsPath(IPlayniteAPI api)
        {
            try
            {
                string thisPluginPath = Playlist.StaticPluginUserDataPath;
                if (string.IsNullOrEmpty(thisPluginPath))
                {
                    return null;
                }

                string extensionsDataPath = Directory.GetParent(thisPluginPath)?.FullName;
                if (string.IsNullOrEmpty(extensionsDataPath))
                {
                    return null;
                }

                string pluginUserDataPath = Path.Combine(extensionsDataPath, HltbPluginId.ToString());
                string[] candidates = new[]
                {
                    Path.Combine(pluginUserDataPath, "settings.json"),
                    Path.Combine(pluginUserDataPath, "config.json"),
                    Path.Combine(pluginUserDataPath, "HowLongToBeatSettings.json"),
                };
                return candidates.FirstOrDefault(File.Exists);
            }
            catch
            {
                return null;
            }
        }
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

    internal sealed class HltbRenderSettings
    {
        public HltbPreferredTimeType PreferredForTimeToBeat { get; set; }
        public bool UseClassic { get; set; }
        public bool UseAverage { get; set; }
        public bool UseMedian { get; set; }
        public bool UseRushed { get; set; }
        public bool UseLeisure { get; set; }
        public bool EnableIntegrationViewItem { get; set; }
        public bool EnableIntegrationButton { get; set; }
        public bool EnableIntegrationProgressBar { get; set; }
        public bool IntegrationViewItemOnlyHour { get; set; }
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
                UseClassic = true,
                EnableIntegrationViewItem = true,
                EnableIntegrationButton = true,
                EnableIntegrationProgressBar = true,
                ShowMainTime = true,
                ShowExtraTime = true,
                ShowCompletionistTime = true,
                ShowSoloTime = true,
                ShowCoOpTime = true,
                ShowVsTime = true,
                ProgressBarShowTime = false,
                ProgressBarShowTimeInterior = true,
                ProgressBarShowTimeAbove = false,
                ProgressBarShowTimeBelow = false,
                ProgressBarShowToolTip = true,
            };
        }

        public static void FromSerializableInto(HltbSerializableSettings settings, HltbRenderSettings d)
        {
            if (settings == null || d == null)
            {
                return;
            }

            d.UseClassic = settings.UseHtltbClassic;
            d.UseAverage = settings.UseHtltbAverage;
            d.UseMedian = settings.UseHtltbMedian;
            d.UseRushed = settings.UseHtltbRushed;
            d.UseLeisure = settings.UseHtltbLeisure;
            d.PreferredForTimeToBeat = ParsePreferredForTimeToBeat(settings.PreferredForTimeToBeat);
            d.EnableIntegrationViewItem = settings.EnableIntegrationViewItem;
            d.EnableIntegrationButton = settings.EnableIntegrationButton;
            d.EnableIntegrationProgressBar = settings.EnableIntegrationProgressBar;
            d.IntegrationViewItemOnlyHour = settings.IntegrationViewItemOnlyHour;
            d.ShowMainTime = settings.ShowMainTime;
            d.ShowExtraTime = settings.ShowExtraTime;
            d.ShowCompletionistTime = settings.ShowCompletionistTime;
            d.ShowSoloTime = settings.ShowSoloTime;
            d.ShowCoOpTime = settings.ShowCoOpTime;
            d.ShowVsTime = settings.ShowVsTime;
            d.ProgressBarShowTime = settings.ProgressBarShowTime;
            d.ProgressBarShowTimeInterior = settings.ProgressBarShowTimeInterior;
            d.ProgressBarShowTimeAbove = settings.ProgressBarShowTimeAbove;
            d.ProgressBarShowTimeBelow = settings.ProgressBarShowTimeBelow;
            d.ProgressBarShowToolTip = settings.ProgressBarShowToolTip;
            ApplySerializedColorBrush(settings.FirstColorBrush, value => ApplySegmentColor(d, segment: 1, isMulti: false, value));
            ApplySerializedColorBrush(settings.SecondColorBrush, value => ApplySegmentColor(d, segment: 2, isMulti: false, value));
            ApplySerializedColorBrush(settings.ThirdColorBrush, value => ApplySegmentColor(d, segment: 3, isMulti: false, value));
            ApplySerializedColorBrush(settings.FirstMultiColorBrush, value => ApplySegmentColor(d, segment: 1, isMulti: true, value));
            ApplySerializedColorBrush(settings.SecondMultiColorBrush, value => ApplySegmentColor(d, segment: 2, isMulti: true, value));
            ApplySerializedColorBrush(settings.ThirdMultiColorBrush, value => ApplySegmentColor(d, segment: 3, isMulti: true, value));
            if (settings.ThumbSolidColorBrush != null)
            {
                Color thumb = ToColor(settings.ThumbSolidColorBrush, Colors.Transparent);
                if (thumb.A != 0)
                {
                    d.ThumbPlaytimeColor = thumb;
                    d.ThumbPlaytimeBrush = new SolidColorBrush(thumb);
                }
            }
        }

        private static void ApplySerializedColorBrush(HltbColorBrushData brushData, Action<Color> apply)
        {
            if (brushData == null)
            {
                return;
            }

            Color color = ToColor(brushData, Colors.Transparent);
            if (color.A == 0)
            {
                return;
            }

            apply(color);
        }

        private static void ApplySegmentColor(HltbRenderSettings settings, int segment, bool isMulti, Color color)
        {
            Brush brush = new SolidColorBrush(color);
            switch (segment)
            {
                case 1 when isMulti:
                    settings.FirstMultiColor = color;
                    settings.FirstMultiBrush = brush;
                    break;
                case 2 when isMulti:
                    settings.SecondMultiColor = color;
                    settings.SecondMultiBrush = brush;
                    break;
                case 3 when isMulti:
                    settings.ThirdMultiColor = color;
                    settings.ThirdMultiBrush = brush;
                    break;
                case 1:
                    settings.FirstColor = color;
                    settings.FirstBrush = brush;
                    break;
                case 2:
                    settings.SecondColor = color;
                    settings.SecondBrush = brush;
                    break;
                case 3:
                    settings.ThirdColor = color;
                    settings.ThirdBrush = brush;
                    break;
            }
        }

        private static HltbPreferredTimeType ParsePreferredForTimeToBeat(int rawValue)
        {
            switch (rawValue)
            {
                case 1:
                    return HltbPreferredTimeType.MainStoryExtra;
                case 2:
                    return HltbPreferredTimeType.Completionist;
                case 3:
                    return HltbPreferredTimeType.Solo;
                case 4:
                    return HltbPreferredTimeType.CoOp;
                case 5:
                    return HltbPreferredTimeType.Versus;
                default:
                    return HltbPreferredTimeType.MainStory;
            }
        }

        private static Color ToColor(HltbColorBrushData brush, Color fallback)
        {
            if (brush == null)
            {
                return fallback;
            }

            byte a;
            byte r;
            byte g;
            byte b;
            if (byte.TryParse(brush.A, out a) && byte.TryParse(brush.R, out r) && byte.TryParse(brush.G, out g) && byte.TryParse(brush.B, out b))
            {
                return Color.FromArgb(a, r, g, b);
            }

            return fallback;
        }
    }

    [DataContract]
    internal sealed class HltbSettingsRoot
    {
        [DataMember(Name = "Settings")]
        public HltbSerializableSettings Settings { get; set; }
    }

    [DataContract]
    internal sealed class HltbSerializableSettings
    {
        [DataMember(Name = "PreferredForTimeToBeat")]
        public int PreferredForTimeToBeat { get; set; }

        [DataMember(Name = "UseHtltbClassic")]
        public bool UseHtltbClassic { get; set; }
        [DataMember(Name = "UseHtltbAverage")]
        public bool UseHtltbAverage { get; set; }
        [DataMember(Name = "UseHtltbMedian")]
        public bool UseHtltbMedian { get; set; }
        [DataMember(Name = "UseHtltbRushed")]
        public bool UseHtltbRushed { get; set; }
        [DataMember(Name = "UseHtltbLeisure")]
        public bool UseHtltbLeisure { get; set; }
        [DataMember(Name = "EnableIntegrationViewItem")]
        public bool EnableIntegrationViewItem { get; set; }
        [DataMember(Name = "EnableIntegrationButton")]
        public bool EnableIntegrationButton { get; set; }
        [DataMember(Name = "EnableIntegrationProgressBar")]
        public bool EnableIntegrationProgressBar { get; set; }
        [DataMember(Name = "IntegrationViewItemOnlyHour")]
        public bool IntegrationViewItemOnlyHour { get; set; }
        [DataMember(Name = "ShowMainTime")]
        public bool ShowMainTime { get; set; }
        [DataMember(Name = "ShowExtraTime")]
        public bool ShowExtraTime { get; set; }
        [DataMember(Name = "ShowCompletionistTime")]
        public bool ShowCompletionistTime { get; set; }
        [DataMember(Name = "ShowSoloTime")]
        public bool ShowSoloTime { get; set; }
        [DataMember(Name = "ShowCoOpTime")]
        public bool ShowCoOpTime { get; set; }
        [DataMember(Name = "ShowVsTime")]
        public bool ShowVsTime { get; set; }
        [DataMember(Name = "ProgressBarShowTime")]
        public bool ProgressBarShowTime { get; set; }
        [DataMember(Name = "ProgressBarShowTimeInterior")]
        public bool ProgressBarShowTimeInterior { get; set; }
        [DataMember(Name = "ProgressBarShowTimeAbove")]
        public bool ProgressBarShowTimeAbove { get; set; }
        [DataMember(Name = "ProgressBarShowTimeBelow")]
        public bool ProgressBarShowTimeBelow { get; set; }
        [DataMember(Name = "ProgressBarShowToolTip")]
        public bool ProgressBarShowToolTip { get; set; }
        [DataMember(Name = "ThumbSolidColorBrush")]
        public HltbColorBrushData ThumbSolidColorBrush { get; set; }
        [DataMember(Name = "FirstColorBrush")]
        public HltbColorBrushData FirstColorBrush { get; set; }
        [DataMember(Name = "SecondColorBrush")]
        public HltbColorBrushData SecondColorBrush { get; set; }
        [DataMember(Name = "ThirdColorBrush")]
        public HltbColorBrushData ThirdColorBrush { get; set; }
        [DataMember(Name = "FirstMultiColorBrush")]
        public HltbColorBrushData FirstMultiColorBrush { get; set; }
        [DataMember(Name = "SecondMultiColorBrush")]
        public HltbColorBrushData SecondMultiColorBrush { get; set; }
        [DataMember(Name = "ThirdMultiColorBrush")]
        public HltbColorBrushData ThirdMultiColorBrush { get; set; }
    }

    [DataContract]
    internal sealed class HltbColorBrushData
    {
        [DataMember(Name = "Color")]
        public HltbColorData Color { get; set; }
        [IgnoreDataMember]
        public string A => Color?.A;
        [IgnoreDataMember]
        public string R => Color?.R;
        [IgnoreDataMember]
        public string G => Color?.G;
        [IgnoreDataMember]
        public string B => Color?.B;
    }

    [DataContract]
    internal sealed class HltbColorData
    {
        [DataMember(Name = "A")]
        public string A { get; set; }
        [DataMember(Name = "R")]
        public string R { get; set; }
        [DataMember(Name = "G")]
        public string G { get; set; }
        [DataMember(Name = "B")]
        public string B { get; set; }
    }

    [DataContract]
    internal sealed class HltbGameFile
    {
        [DataMember(Name = "Items")]
        public List<HltbGameFileItem> Items { get; set; }
    }

    [DataContract]
    internal sealed class HltbGameFileItem
    {
        [DataMember(Name = "GameHltbData")]
        public HltbGameData GameHltbData { get; set; }

        [DataMember(Name = "Url")]
        public string Url { get; set; }

        [DataMember(Name = "GameType")]
        public int GameType { get; set; }
    }

    [DataContract]
    internal sealed class HltbGameData
    {
        [DataMember(Name = "MainStoryClassic")]
        public long MainStoryClassic { get; set; }
        [DataMember(Name = "MainStoryMedian")]
        public long MainStoryMedian { get; set; }
        [DataMember(Name = "MainStoryAverage")]
        public long MainStoryAverage { get; set; }
        [DataMember(Name = "MainStoryRushed")]
        public long MainStoryRushed { get; set; }
        [DataMember(Name = "MainStoryLeisure")]
        public long MainStoryLeisure { get; set; }

        [DataMember(Name = "MainExtraClassic")]
        public long MainExtraClassic { get; set; }
        [DataMember(Name = "MainExtraMedian")]
        public long MainExtraMedian { get; set; }
        [DataMember(Name = "MainExtraAverage")]
        public long MainExtraAverage { get; set; }
        [DataMember(Name = "MainExtraRushed")]
        public long MainExtraRushed { get; set; }
        [DataMember(Name = "MainExtraLeisure")]
        public long MainExtraLeisure { get; set; }

        [DataMember(Name = "CompletionistClassic")]
        public long CompletionistClassic { get; set; }
        [DataMember(Name = "CompletionistMedian")]
        public long CompletionistMedian { get; set; }
        [DataMember(Name = "CompletionistAverage")]
        public long CompletionistAverage { get; set; }
        [DataMember(Name = "CompletionistRushed")]
        public long CompletionistRushed { get; set; }
        [DataMember(Name = "CompletionistLeisure")]
        public long CompletionistLeisure { get; set; }

        [DataMember(Name = "SoloClassic")]
        public long SoloClassic { get; set; }
        [DataMember(Name = "SoloMedian")]
        public long SoloMedian { get; set; }
        [DataMember(Name = "SoloAverage")]
        public long SoloAverage { get; set; }
        [DataMember(Name = "SoloRushed")]
        public long SoloRushed { get; set; }
        [DataMember(Name = "SoloLeisure")]
        public long SoloLeisure { get; set; }

        [DataMember(Name = "CoOpClassic")]
        public long CoOpClassic { get; set; }
        [DataMember(Name = "CoOpMedian")]
        public long CoOpMedian { get; set; }
        [DataMember(Name = "CoOpAverage")]
        public long CoOpAverage { get; set; }
        [DataMember(Name = "CoOpRushed")]
        public long CoOpRushed { get; set; }
        [DataMember(Name = "CoOpLeisure")]
        public long CoOpLeisure { get; set; }

        [DataMember(Name = "VsClassic")]
        public long VsClassic { get; set; }
        [DataMember(Name = "VsMedian")]
        public long VsMedian { get; set; }
        [DataMember(Name = "VsAverage")]
        public long VsAverage { get; set; }
        [DataMember(Name = "VsRushed")]
        public long VsRushed { get; set; }
        [DataMember(Name = "VsLeisure")]
        public long VsLeisure { get; set; }
    }
}
