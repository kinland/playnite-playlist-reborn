using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            Padding = new Thickness(2);
            SnapsToDevicePixels = true;
            Loaded += (_, __) =>
            {
                TryCreateContent();
                ApplyGameContext();
                ClearHighlightChromeWhenNotManaged();
            };
            DataContextChanged += (_, __) => ApplyGameContext();
            MouseEnter += (_, __) => SyncRowHighlightFromListViewItem(isRowHoverActive: false);
            MouseLeave += (_, __) => SyncRowHighlightFromListViewItem(isRowHoverActive: false);
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
                HookEmbeddedContentDirectHover(control);
                ClearHighlightChromeWhenNotManaged();
            }
        }

        private void HookEmbeddedContentDirectHover(Control control)
        {
            control.MouseEnter -= OnEmbeddedContentDirectHoverChanged;
            control.MouseLeave -= OnEmbeddedContentDirectHoverChanged;
            control.MouseEnter += OnEmbeddedContentDirectHoverChanged;
            control.MouseLeave += OnEmbeddedContentDirectHoverChanged;
        }

        private void OnEmbeddedContentDirectHoverChanged(object sender, MouseEventArgs e)
        {
            SyncRowHighlightFromListViewItem(isRowHoverActive: false);
        }

        private void ApplyGameContext()
        {
            if (!(Playlist.StaticSettings?.EnableHowLongToBeatIntegration ?? true))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

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

        internal void ClearHighlightChrome()
        {
            PlaylistSortHeaderLayout.ClearListRowControlChrome(this);
            if (Content is Control control)
            {
                PlaylistSortHeaderLayout.ClearListRowControlChrome(control);
            }
        }

        internal void SyncRowHighlightFromListViewItem(bool isRowHoverActive)
        {
            if (!PlaylistSortHeaderLayout.UsesInvertedRowHighlightChrome(TryFindResource))
            {
                ClearHighlightChrome();
                return;
            }

            ListViewItem row = FindListViewItemAncestor(this);
            bool isRowHighlightActive = GetRowHighlightActive(isRowHoverActive);
            bool isDirectHover = IsMouseOver || (Content is UIElement content && content.IsMouseOver);
            PlaylistSortHeaderLayout.ApplyListRowEmbeddedControlChrome(this, row, isRowHighlightActive, isDirectHover, TryFindResource);

            if (Content is Control control)
            {
                PlaylistSortHeaderLayout.ClearListRowControlChrome(control);
            }
        }

        private bool GetRowHighlightActive(bool isRowHoverActive)
        {
            ListViewItem row = FindListViewItemAncestor(this);
            return row != null && (row.IsSelected || isRowHoverActive || row.IsMouseOver);
        }

        private void ClearHighlightChromeWhenNotManaged()
        {
            if (!PlaylistSortHeaderLayout.UsesInvertedRowHighlightChrome(TryFindResource))
            {
                ClearHighlightChrome();
            }
        }

        private static ListViewItem FindListViewItemAncestor(DependencyObject d)
        {
            for (var p = d; p != null; p = VisualTreeHelper.GetParent(p))
            {
                if (p is ListViewItem lvi)
                {
                    return lvi;
                }
            }

            return null;
        }
    }
}
