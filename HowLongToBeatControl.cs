using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

            // Lazily create or reuse the embedded plugin control for this Game.
            if (!cachedControlsByName.TryGetValue(controlName, out var byGame))
            {
                byGame = new Dictionary<Guid, PluginUserControl>();
                cachedControlsByName[controlName] = byGame;
            }

            if (!byGame.TryGetValue(gameId, out var cached) || cached == null)
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

                byGame[gameId] = cached;
            }

            // If the cached control is still parented elsewhere, don't reuse it (avoid WPF logical-parent issues).
            // Fallback to a fresh instance for this wrapper; it will still be "first load" for that wrapper.
            if (control != cached)
            {
                if (cached.Parent != null && cached.Parent != this)
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

                    byGame[gameId] = cached;
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
