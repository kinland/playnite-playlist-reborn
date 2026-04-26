using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Playlist
{
    /// <summary>
    /// Embeds the HowLongToBeat plugin's small game view control so the playlist can open HLTB for the row game.
    /// </summary>
    public sealed class HowLongToBeatPluginButtonHost : ContentControl
    {
        private static readonly Guid HltbPluginId = Guid.Parse("e08cd51f-9c9a-4ee3-a094-fde03b55492f");

        public static readonly DependencyProperty IsDragReorderSuspendedProperty =
            DependencyProperty.Register(
                nameof(IsDragReorderSuspended),
                typeof(bool),
                typeof(HowLongToBeatPluginButtonHost),
                new PropertyMetadata(false, (d, _) => ((HowLongToBeatPluginButtonHost)d).ApplyGameContext()));

        public bool IsDragReorderSuspended
        {
            get => (bool)GetValue(IsDragReorderSuspendedProperty);
            set => SetValue(IsDragReorderSuspendedProperty, value);
        }

        public HowLongToBeatPluginButtonHost()
        {
            MinWidth = 40;
            MinHeight = 28;
            VerticalAlignment = VerticalAlignment.Center;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            Loaded += (_, __) =>
            {
                TryCreateContent();
                ApplyGameContext();
            };
            DataContextChanged += (_, __) => ApplyGameContext();
        }

        private void TryCreateContent()
        {
            if (Content != null)
            {
                return;
            }

            IPlayniteAPI api = Playlist.StaticPlayniteApi;
            if (api?.Addons?.Plugins == null)
            {
                return;
            }

            Plugin plugin = api.Addons.Plugins.FirstOrDefault(p => p.Id == HltbPluginId);
            if (plugin == null)
            {
                return;
            }

            Control control = plugin.GetGameViewControl(new GetGameViewControlArgs
            {
                Name = "PluginButton",
                Mode = ApplicationMode.Desktop,
            });

            if (control != null)
            {
                Content = control;
            }
        }

        private void ApplyGameContext()
        {
            HltbRenderSettings settings = HowLongToBeatCache.GetRenderSettings(Playlist.StaticPlayniteApi);
            bool isVisible = settings.EnableIntegrationViewItem && settings.EnableIntegrationButton;
            Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (!isVisible)
            {
                return;
            }

            if (Content == null)
            {
                return;
            }

            Game game = IsDragReorderSuspended ? null : DataContext as Game;
            dynamic host = Content;
            try
            {
                host.GameContext = game;
            }
            catch
            {
            }
        }
    }
}
