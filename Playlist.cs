using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Playlist
{
    public class Playlist : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static IPlayniteAPI StaticPlayniteApi { get; set; }
        public static string StaticPluginUserDataPath { get; set; }
        public static PlaylistSettings StaticSettings { get; private set; }
        internal static Playlist StaticPluginInstance { get; private set; }

        private PlaylistViewModel PlaylistViewModel { get; set; }

        private PlaylistView PlaylistView { get; set; }

        private MainSearchSync MainSearchSync { get; set; }

        private bool isPlaylistViewOpen;

        public ObservableCollection<Game> PlaylistGames { get; set; }
        private readonly PlaylistSettings settings;

        private const string playlistPath = "playlist.txt";

        /// <summary>
        /// Tag stored on games that appear in the sidebar playlist; the "Playlist" filter preset matches this tag.
        /// Fixed English name so it stays stable across locales and matches one logical tag in the library.
        /// </summary>
        private const string playlistMembershipTagName = "Playlist";

        private const string playlistFilterPresetName = "Playlist";

        private Tag playlistMembershipTag;

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = ResourceProvider.GetString("LOCPlaylist_Playlist"),
                Type = SiderbarItemType.View,
                Icon = new TextBlock
                {
                    Text = "\ueca6", // Circled play button
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                },
                Opened = () => {
                    if (PlaylistViewModel == null)
                    {
                        PlaylistViewModel = new PlaylistViewModel(PlaylistGames, PlayniteApi);
                        PlaylistView = new PlaylistView(PlaylistViewModel);
                    }
                    else
                    {
                        // Settings can change from the Extensions menu while another sidebar view is active.
                        PlaylistView.ApplySettings();
                    }

                    MainSearchSync.Attach(PlaylistViewModel);
                    isPlaylistViewOpen = true;
                    MainSearchSync.OnViewOpened();
                    return PlaylistView;
                },
                Closed = () =>
                {
                    isPlaylistViewOpen = false;
                    MainSearchSync.OnViewClosed();
                }
            };
        }
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                Description = ResourceProvider.GetString("LOCPlaylist_Menu_AddToPlaylist"),
                Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png"),
                Action = (itemArgs) =>
                {
                    foreach (Game game in args.Games)
                    {
                        PlaylistGames.AddMissing(game);
                    }
                }
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            if (args.IsGlobalSearchRequest)
            {
                yield break;
            }

            foreach (MainMenuItem item in PlaylistQuickAccessMenuBuilder.BuildExtensionMainMenuItems())
            {
                yield return item;
            }
        }

        public override Guid Id { get; } = Guid.Parse("b0313f81-2b86-4eba-9f24-1a727dedbd45");

        public Playlist(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            // Ensure the library loaded now, relative to the extension DLL.
            // If the XAML trys to load it later it will incorrectly load it relative to Playnite's executable
            Assembly.Load("GongSolutions.WPF.DragDrop");

            StaticPlayniteApi = api;
            StaticPluginUserDataPath = GetPluginUserDataPath();
            StaticPluginInstance = this;
            settings = LoadPluginSettings<PlaylistSettings>() ?? new PlaylistSettings(this);
            settings.AttachPlugin(this);
            StaticSettings = settings;
            PlaylistLocalizationOverride.ApplyFromSettings(settings);
            MainSearchSync = new MainSearchSync(api, () => settings.SyncSearchWithMainPanel);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            settings.RefreshHowLongToBeatInstallState();
            settings.RefreshLanguageOptions();
            PlaylistSettingsView view = new PlaylistSettingsView
            {
                DataContext = settings,
            };
            return view;
        }

        internal void SaveSettings(PlaylistSettings updatedSettings)
        {
            SavePluginSettings(updatedSettings);
            StaticSettings = updatedSettings;
        }

        internal void PersistSettings()
        {
            SavePluginSettings(settings);
            StaticSettings = settings;
            settings?.NotifyPersistedToStorage();
        }

        internal void ApplySettingsToOpenView()
        {
            StaticSettings = settings;
            settings?.RefreshHowLongToBeatInstallState();
            PlaylistView?.ApplySettings();
            MainSearchSync?.ApplySettingsChange(isPlaylistViewOpen);
        }

        private IEnumerable<Game> LoadPlaylistFile()
        {
            string path = Path.Combine(GetPluginUserDataPath(), playlistPath);
            if (File.Exists(path))
            {
                foreach (string guid in File.ReadLines(path))
                {
                    Game game = PlayniteApi.Database.Games.Get(Guid.Parse(guid));
                    if (game != null)
                    {
                        yield return game;
                    }
                }
            }
        }

        private void UpdatePlaylistFile()
        {
            string path = Path.Combine(GetPluginUserDataPath(), playlistPath);
            File.WriteAllLines(path, PlaylistGames.Select((g) => g.Id.ToString()));
        }

        private Tag GetOrCreatePlaylistMembershipTag()
        {
            return PlayniteApi.Database.Tags.Add(playlistMembershipTagName);
        }

        private void EnsurePlaylistFilterPreset(Tag membershipTag)
        {
            IGameDatabase db = PlayniteApi.Database;
            FilterPresetSettings settings = new FilterPresetSettings
            {
                Tag = new IdItemFilterItemProperties(membershipTag.Id),
            };

            FilterPreset existing = db.FilterPresets.FirstOrDefault(p => p.Name == playlistFilterPresetName);
            if (existing != null)
            {
                bool tagOk = existing.Settings?.Tag?.Ids != null
                    && existing.Settings.Tag.Ids.Count == 1
                    && existing.Settings.Tag.Ids[0] == membershipTag.Id;
                if (!tagOk)
                {
                    existing.Settings = settings;
                    db.FilterPresets.Update(existing);
                }

                return;
            }

            db.FilterPresets.Add(new FilterPreset
            {
                Id = Guid.NewGuid(),
                Name = playlistFilterPresetName,
                Settings = settings,
                ShowInFullscreeQuickSelection = true,
            });
        }

        private void SetPlaylistMembershipTag(Game game, bool inPlaylist)
        {
            if (game == null || playlistMembershipTag == null)
            {
                return;
            }

            List<Guid> tagIds = game.TagIds ?? (game.TagIds = new List<Guid>());
            bool has = tagIds.Contains(playlistMembershipTag.Id);
            if (inPlaylist == has)
            {
                return;
            }

            if (inPlaylist)
            {
                tagIds.AddMissing(playlistMembershipTag.Id);
            }
            else
            {
                tagIds.Remove(playlistMembershipTag.Id);
            }

            PlayniteApi.Database.Games.Update(game);
        }

        private void OnPlaylistGamesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdatePlaylistFile();
            if (playlistMembershipTag == null)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (Game game in e.NewItems.Cast<Game>())
                    {
                        SetPlaylistMembershipTag(game, true);
                    }

                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (Game game in e.OldItems.Cast<Game>())
                    {
                        SetPlaylistMembershipTag(game, false);
                    }

                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                    {
                        foreach (Game game in e.OldItems.Cast<Game>())
                        {
                            SetPlaylistMembershipTag(game, false);
                        }
                    }

                    if (e.NewItems != null)
                    {
                        foreach (Game game in e.NewItems.Cast<Game>())
                        {
                            SetPlaylistMembershipTag(game, true);
                        }
                    }

                    break;
                case NotifyCollectionChangedAction.Reset:
                    // Reset is used e.g. by ObservableCollection.Clear() and does not list removed items.
                    // This extension only removes games via Remove (handled above), so we do not scan the full library.
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                default:
                    break;
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            try
            {
                // Initialization is done inside OnApplicationStarted, otherwise
                // loadPlaylistFile runs too early in Playnite's startup and
                // cannot call PlayniteApi.Database.Games.Get()

                PlaylistGames = new ObservableCollection<Game>(LoadPlaylistFile());
                playlistMembershipTag = GetOrCreatePlaylistMembershipTag();
                EnsurePlaylistFilterPreset(playlistMembershipTag);
                using (PlayniteApi.Database.Games.BufferedUpdate())
                {
                    foreach (Game game in PlaylistGames)
                    {
                        SetPlaylistMembershipTag(game, true);
                    }
                }

                PlaylistGames.CollectionChanged += OnPlaylistGamesCollectionChanged;
                PlayniteApi.Database.Games.ItemCollectionChanged += (sender, changedArgs) =>
                {
                    foreach (Game game in changedArgs.RemovedItems)
                    {
                        PlaylistGames.Remove(game);
                    }
                };

                settings.ExpireSessionOnlyHltbPendingFlags();
                settings.ExpireAddonPendingIfHltbStillUnavailable();
                settings.RefreshHowLongToBeatInstallState();
                PlaylistLocalizationOverride.ApplyFromSettings(settings);
                ApplySettingsToOpenView();
            }
            catch (Exception e)
            {
                logger.Error(e, "Error loading PlaylistGames in OnApplicationStarted");
                PlayniteApi.Notifications.Add($"{Id}-OnApplicationStarted", $"{ResourceProvider.GetString("LOCPlaylist_ErrorNotLoadFile")} {e.Message}", NotificationType.Error);
            }
        }

    }
}