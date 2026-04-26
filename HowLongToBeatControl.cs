using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Playlist
{
    /// <summary>
    /// Hosts HowLongToBeat's library view controls in the playlist grid. Playnite does not officially support
    /// embedding another extension's views here (see Playnite maintainer notes on PR #4); this uses
    /// <see cref="IAddons.Plugins"/> and may break in a future SDK. HowLongToBeat plugin id is stable.
    /// </summary>
    public class HowLongToBeatControl : ContentControl
    {
        public static readonly DependencyProperty IsDragReorderSuspendedProperty = DependencyProperty.Register(
            nameof(IsDragReorderSuspended),
            typeof(bool),
            typeof(HowLongToBeatControl),
            new PropertyMetadata(false, (d, e) => ((HowLongToBeatControl)d).ApplyGameContext()));

        private static readonly Guid HowLongToBeatPluginId = Guid.Parse("e08cd51f-9c9a-4ee3-a094-fde03b55492f");

        private static Plugin _plugin;
        private static bool _pluginResolved;

        private readonly string controlName;
        private PluginUserControl control;
        private Guid? appliedGameId;

        // Cache embedded plugin view controls per (controlName, Game.Id) so scrolling doesn't
        // force the plugin UI to "restart" and re-layout (visible as pop-in/flash).
        private static readonly Dictionary<string, Dictionary<Guid, PluginUserControl>> cachedControlsByName
            = new Dictionary<string, Dictionary<Guid, PluginUserControl>>();
        private static readonly Queue<Game> preloadQueue = new Queue<Game>();
        private static readonly HashSet<Guid> preloadQueuedIds = new HashSet<Guid>();
        private static readonly LinkedList<Guid> lruGameIds = new LinkedList<Guid>();
        private static readonly Dictionary<Guid, LinkedListNode<Guid>> lruNodesByGameId = new Dictionary<Guid, LinkedListNode<Guid>>();
        private static DispatcherTimer preloadTimer;
        private static int cacheCapGames = 300;

        private const string ProgressBarControlName = "PluginProgressBar";
        private const string ButtonControlName = "PluginButton";

        public bool IsDragReorderSuspended
        {
            get => (bool)GetValue(IsDragReorderSuspendedProperty);
            set => SetValue(IsDragReorderSuspendedProperty, value);
        }

        private static Plugin Plugin
        {
            get
            {
                if (!_pluginResolved)
                {
                    _pluginResolved = true;
                    try
                    {
                        // Not a supported integration surface; guard for API or load order changes.
                        _plugin = Playlist.StaticPlayniteApi?.Addons?.Plugins?.FirstOrDefault(p => p.Id == HowLongToBeatPluginId);
                    }
                    catch (Exception)
                    {
                        _plugin = null;
                    }
                }

                return _plugin;
            }
        }

        public static bool HowLongToBeatIsInstalled => Plugin != null;

        public static void SetCacheCapGames(int maxGames)
        {
            cacheCapGames = Math.Max(1, maxGames);
            EnforceCacheCap();
        }

        public static void PruneCacheToGames(IEnumerable<Game> gamesToKeep)
        {
            var keepIds = new HashSet<Guid>(
                (gamesToKeep ?? Enumerable.Empty<Game>())
                    .Where(g => g != null)
                    .Select(g => g.Id));

            var cachedIds = new HashSet<Guid>(lruNodesByGameId.Keys);
            foreach (var id in cachedIds)
            {
                if (!keepIds.Contains(id))
                {
                    RemoveGameFromCache(id);
                }
            }
        }

        public static void QueuePreloadAlternatingCacheMisses(IList<Game> orderedGames, int maxGames)
        {
            if (Plugin == null || orderedGames == null || orderedGames.Count == 0 || maxGames <= 0)
            {
                return;
            }

            var misses = GetAlternatingMissingGames(orderedGames, maxGames);
            QueuePreloadGames(misses);
        }

        public static void QueuePreloadGames(IEnumerable<Game> games)
        {
            if (Plugin == null || games == null)
            {
                return;
            }

            EnsurePreloadTimer();
            foreach (var game in games)
            {
                if (game == null || IsControlCached(ProgressBarControlName, game.Id) || !preloadQueuedIds.Add(game.Id))
                {
                    continue;
                }

                preloadQueue.Enqueue(game);
            }

            if (preloadQueue.Count > 0 && !preloadTimer.IsEnabled)
            {
                preloadTimer.Start();
            }
        }

        public HowLongToBeatControl(string controlName)
        {
            this.controlName = controlName ?? throw new ArgumentNullException(nameof(controlName));

            if (Plugin == null)
            {
                return;
            }

            // Stable height reduces visible "pop-in" layout as the plugin control finishes loading.
            MinHeight = 30;
            ApplyGameContext();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// ListView virtualization may unload/reload row visuals when they scroll off/on screen.
        /// To avoid visible "pop-in", we avoid clearing <see cref="PluginUserControl.GameContext"/> on unload and we only
        /// re-assign it when the <see cref="Game"/> instance actually changes (except during drag-reorder where we intentionally clear it).
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyGameContext();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (control != null && IsDragReorderSuspended)
            {
                control.GameContext = null;
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == DataContextProperty)
            {
                ApplyGameContext();
            }
        }

        private void ApplyGameContext()
        {
            if (Plugin == null)
            {
                return;
            }

            Game desiredGame = IsDragReorderSuspended ? null : DataContext as Game;
            Guid? desiredGameId = desiredGame?.Id;

            if (IsDragReorderSuspended)
            {
                appliedGameId = null;
                if (control != null)
                {
                    control.GameContext = null;
                }
                return;
            }

            if (desiredGameId == null)
            {
                appliedGameId = null;
                return;
            }

            Guid gameId = desiredGameId.Value;

            if (appliedGameId == gameId && control != null)
            {
                return;
            }

            var cached = GetOrCreateCachedControl(controlName, desiredGame, gameId);
            if (cached == null)
            {
                return;
            }

            // If cached control is parented to a different wrapper, detach it so it can be reused.
            // Reuse is essential for making preloading effective across virtualization container swaps.
            if (control != cached)
            {
                if (cached.Parent != null && cached.Parent != this)
                {
                    var owner = cached.Parent as HowLongToBeatControl;
                    if (owner != null)
                    {
                        owner.ReleaseCachedControlIfOwned(cached);
                    }

                    if (cached.Parent != null)
                    {
                        cached = Plugin.GetGameViewControl(new GetGameViewControlArgs
                        {
                            Name = controlName,
                            Mode = ApplicationMode.Desktop,
                        }) as PluginUserControl;

                        if (cached == null)
                        {
                            return;
                        }
                    }
                }

                control = cached;
                Content = control;
            }

            // Only set GameContext if it doesn't already match.
            if (!(control.GameContext is Game currentGame) || currentGame.Id != gameId)
            {
                control.GameContext = desiredGame;
            }

            appliedGameId = gameId;
        }

        private static void EnsurePreloadTimer()
        {
            if (preloadTimer != null)
            {
                return;
            }

            preloadTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                // Keep this conservative to avoid heavy background work while still warming cache quickly.
                Interval = TimeSpan.FromMilliseconds(100),
            };
            preloadTimer.Tick += OnPreloadTick;
        }

        private static void OnPreloadTick(object sender, EventArgs e)
        {
            if (Plugin == null || preloadQueue.Count == 0)
            {
                preloadTimer.Stop();
                return;
            }

            const int gamesPerTick = 5;
            for (int i = 0; i < gamesPerTick && preloadQueue.Count > 0; i++)
            {
                var game = preloadQueue.Dequeue();
                preloadQueuedIds.Remove(game.Id);

                // Preload only the progress bar; button is mouseover-only and can stay on-demand.
                GetOrCreateCachedControl(ProgressBarControlName, game, game.Id);
            }

            if (preloadQueue.Count == 0)
            {
                preloadTimer.Stop();
            }
        }

        private static PluginUserControl GetOrCreateCachedControl(string targetControlName, Game game, Guid gameId)
        {
            if (!cachedControlsByName.TryGetValue(targetControlName, out var byGame))
            {
                byGame = new Dictionary<Guid, PluginUserControl>();
                cachedControlsByName[targetControlName] = byGame;
            }

            if (!byGame.TryGetValue(gameId, out var cached) || cached == null)
            {
                cached = Plugin.GetGameViewControl(new GetGameViewControlArgs
                {
                    Name = targetControlName,
                    Mode = ApplicationMode.Desktop,
                }) as PluginUserControl;

                if (cached == null)
                {
                    return null;
                }

                byGame[gameId] = cached;
            }

            if (!(cached.GameContext is Game currentGame) || currentGame.Id != gameId)
            {
                cached.GameContext = game;
            }

            TouchLru(gameId);
            EnforceCacheCap();
            return cached;
        }

        private void ReleaseCachedControlIfOwned(PluginUserControl candidate)
        {
            if (!ReferenceEquals(control, candidate))
            {
                return;
            }

            Content = null;
            control = null;
            appliedGameId = null;
        }

        private static bool IsControlCached(string targetControlName, Guid gameId)
        {
            if (!cachedControlsByName.TryGetValue(targetControlName, out var byGame))
            {
                return false;
            }

            return byGame.TryGetValue(gameId, out var cached) && cached != null;
        }

        private static IEnumerable<Game> GetAlternatingMissingGames(IList<Game> orderedGames, int maxGames)
        {
            var idToIndex = new Dictionary<Guid, int>();
            for (int i = 0; i < orderedGames.Count; i++)
            {
                var game = orderedGames[i];
                if (game != null && !idToIndex.ContainsKey(game.Id))
                {
                    idToIndex[game.Id] = i;
                }
            }

            var cachedIndices = new List<int>();
            if (cachedControlsByName.TryGetValue(ProgressBarControlName, out var progressByGame))
            {
                foreach (var id in progressByGame.Keys)
                {
                    if (idToIndex.TryGetValue(id, out int idx))
                    {
                        cachedIndices.Add(idx);
                    }
                }
            }

            var results = new List<Game>(Math.Min(maxGames, orderedGames.Count));
            if (cachedIndices.Count == 0)
            {
                for (int i = 0; i < orderedGames.Count && results.Count < maxGames; i++)
                {
                    var game = orderedGames[i];
                    if (game != null && !IsControlCached(ProgressBarControlName, game.Id))
                    {
                        results.Add(game);
                    }
                }

                return results;
            }

            int low = cachedIndices.Min();
            int high = cachedIndices.Max();
            int delta = 1;
            while (results.Count < maxGames && (low - delta >= 0 || high + delta < orderedGames.Count))
            {
                int above = low - delta;
                if (above >= 0)
                {
                    var game = orderedGames[above];
                    if (game != null && !IsControlCached(ProgressBarControlName, game.Id))
                    {
                        results.Add(game);
                        if (results.Count >= maxGames)
                        {
                            break;
                        }
                    }
                }

                int below = high + delta;
                if (below < orderedGames.Count)
                {
                    var game = orderedGames[below];
                    if (game != null && !IsControlCached(ProgressBarControlName, game.Id))
                    {
                        results.Add(game);
                    }
                }

                delta++;
            }

            return results;
        }

        private static void TouchLru(Guid gameId)
        {
            if (lruNodesByGameId.TryGetValue(gameId, out var node))
            {
                lruGameIds.Remove(node);
                lruGameIds.AddFirst(node);
                return;
            }

            var newNode = lruGameIds.AddFirst(gameId);
            lruNodesByGameId[gameId] = newNode;
        }

        private static void EnforceCacheCap()
        {
            while (lruGameIds.Count > cacheCapGames)
            {
                var tail = lruGameIds.Last;
                if (tail == null)
                {
                    break;
                }

                RemoveGameFromCache(tail.Value);
            }
        }

        private static void RemoveGameFromCache(Guid gameId)
        {
            foreach (var byGame in cachedControlsByName.Values)
            {
                if (byGame.TryGetValue(gameId, out var controlToRemove) && controlToRemove != null)
                {
                    var owner = controlToRemove.Parent as HowLongToBeatControl;
                    owner?.ReleaseCachedControlIfOwned(controlToRemove);
                    byGame.Remove(gameId);
                }
            }

            if (lruNodesByGameId.TryGetValue(gameId, out var node))
            {
                lruNodesByGameId.Remove(gameId);
                lruGameIds.Remove(node);
            }

            preloadQueuedIds.Remove(gameId);
        }
    }

    public class HowLongToBeatProgressBar : HowLongToBeatControl
    {
        public HowLongToBeatProgressBar()
            : base("PluginProgressBar")
        {
        }
    }

    public class HowLongToBeatPluginButton : HowLongToBeatControl
    {
        public HowLongToBeatPluginButton()
            : base("PluginButton")
        {
        }
    }
}
