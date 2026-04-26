using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
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

        private PluginUserControl control;

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
            if (Plugin == null)
            {
                return;
            }

            control = Plugin.GetGameViewControl(new GetGameViewControlArgs
            {
                Name = controlName,
                Mode = ApplicationMode.Desktop,
            }) as PluginUserControl;

            if (control == null)
            {
                return;
            }

            // Stable height reduces visible "pop-in" layout as the plugin control finishes loading.
            MinHeight = 30;
            Content = control;
            ApplyGameContext();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// ListView virtualization unloads row visuals when they scroll off-screen; we clear <see cref="PluginUserControl.GameContext"/>
        /// on unload. WPF often does not raise <see cref="DataContextProperty"/> again when the same <see cref="Game"/> instance is
        /// re-shown, so we must re-apply context whenever the control re-enters the tree.
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyGameContext();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (control != null)
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
            if (control == null)
            {
                return;
            }

            control.GameContext = IsDragReorderSuspended ? null : DataContext as Game;
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
