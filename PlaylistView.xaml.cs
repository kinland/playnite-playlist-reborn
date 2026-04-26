using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK.Models;
using System.Windows.Controls.Primitives;

namespace Playlist
{
    /// <summary>
    /// Interaction logic for PlaylistView.xaml
    /// </summary>
    public partial class PlaylistView : UserControl
    {
        public PlaylistView()
        {
            InitializeComponent();
            playlistListView.SizeChanged += OnPlaylistListViewSizeChanged;
            playlistListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnPlaylistListViewColumnThumbDragCompleted), handledEventsToo: true);
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloadedPruneHowLongToBeatCache;
        }

        public PlaylistView(PlaylistViewModel model)
        {
            DataContext = model;
            InitializeComponent();
            playlistListView.SizeChanged += OnPlaylistListViewSizeChanged;
            playlistListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnPlaylistListViewColumnThumbDragCompleted), handledEventsToo: true);
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloadedPruneHowLongToBeatCache;
        }

        private const double HowLongToBeatColumnMinWidth = 120;

        private void OnPlaylistListViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateHowLongToBeatColumnFillWidth();
        }

        private void OnPlaylistListViewColumnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            UpdateHowLongToBeatColumnFillWidth();
        }

        /// <summary>
        /// Keep HLTB column at the current user-selected width and only enforce a small minimum.
        /// This avoids snapping back to a computed max width after drag-resize.
        /// </summary>
        private void UpdateHowLongToBeatColumnFillWidth()
        {
            if (playlistListView == null || howLongToBeatGridViewColumn == null
                || !(playlistListView.View is GridView gridView))
            {
                return;
            }

            if (!gridView.Columns.Contains(howLongToBeatGridViewColumn))
            {
                return;
            }

            double currentWidth = howLongToBeatGridViewColumn.Width;
            if (double.IsNaN(currentWidth) || currentWidth <= 0)
            {
                return;
            }

            howLongToBeatGridViewColumn.Width = Math.Max(HowLongToBeatColumnMinWidth, currentWidth);
        }

        private void OnPlaylistViewLoadedApplyHowLongToBeatColumn(object sender, RoutedEventArgs e)
        {
            HowLongToBeatCache.InvalidateRenderSettingsCache();
            if (HowLongToBeatCache.IsAvailable(Playlist.StaticPlayniteApi))
            {
                UpdateHowLongToBeatColumnFillWidth();
                return;
            }

            if (playlistListView?.View is GridView gridView && howLongToBeatGridViewColumn != null)
            {
                int hltbColumnIndex = gridView.Columns.IndexOf(howLongToBeatGridViewColumn);
                if (hltbColumnIndex >= 0)
                {
                    gridView.Columns.RemoveAt(hltbColumnIndex);
                }
            }
        }

        private void OnPlaylistViewUnloadedPruneHowLongToBeatCache(object sender, RoutedEventArgs e)
        {
        }

        private void OnPlaylistGridViewColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is GridViewColumnHeader header)
                || header.Role == GridViewColumnHeaderRole.Padding
                || !(header.Column is GridViewColumn column)
                || !(DataContext is PlaylistViewModel model)
                || !(playlistListView.View is GridView gridView))
            {
                return;
            }

            // GridViewColumn has no Tag in WPF; order must match <GridView> column sequence (icon column = 1, no sort).
            string sortKey;
            switch (gridView.Columns.IndexOf(column))
            {
                case 0:
                    sortKey = "Rank";
                    break;
                case 2:
                    sortKey = "Name";
                    break;
                case 3:
                    sortKey = "Playtime";
                    break;
                case 4:
                    sortKey = "CompletionStatus";
                    break;
                default:
                    return;
            }

            model.ToggleViewSort(sortKey);
        }

        private const string RankCellTag = "PlaylistRankCell";

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsUnderRankCell(e.OriginalSource as DependencyObject))
            {
                return;
            }

            Control item = sender as Control;
            (DataContext as PlaylistViewModel)?.StartGameCommand.Execute(item?.DataContext);
        }

        private void OnRankCellMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (!(sender is Grid container) || container.Children.Count < 2)
            {
                return;
            }

            if (!(container.Children[0] is TextBlock textBlock) || !(container.Children[1] is TextBox rankEditor))
            {
                return;
            }

            // Already editing — still mark handled so the row does not start the game.
            if (rankEditor.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                return;
            }

            rankEditor.Text = textBlock.Text;
            textBlock.Visibility = Visibility.Collapsed;
            rankEditor.Visibility = Visibility.Visible;
            rankEditor.Focus();
            rankEditor.SelectAll();
            e.Handled = true;
        }

        private void OnRankTextBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Row listens for double-click to launch; swallow while editing rank.
            e.Handled = true;
        }

        private static bool IsUnderRankCell(DependencyObject source)
        {
            for (DependencyObject d = source; d != null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is FrameworkElement fe && fe.Tag is string s && s == RankCellTag)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnRankTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox rankEditor))
            {
                return;
            }

            // Main keyboard Enter is Key.Return in WPF; numpad is often Key.Enter.
            // If we don't handle Return, the key stays unhandled and the row's IsDefault play button launches the game.
            if (IsCommitRankKey(e.Key))
            {
                CommitRankEdit(rankEditor);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelRankEdit(rankEditor);
                e.Handled = true;
            }
        }

        private static bool IsCommitRankKey(Key key)
        {
            return key == Key.Enter || key == Key.Return;
        }

        private void OnRankTextBoxLostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is TextBox rankEditor)
            {
                CancelRankEdit(rankEditor);
            }
        }

        private void CommitRankEdit(TextBox rankEditor)
        {
            if (!(rankEditor.DataContext is Playnite.SDK.Models.Game game) || !(DataContext is PlaylistViewModel model))
            {
                CancelRankEdit(rankEditor);
                return;
            }

            if (int.TryParse(rankEditor.Text, out int rank))
            {
                model.MoveGameToRank(game, rank);
                playlistListView.SelectedItem = game;
                playlistListView.ScrollIntoView(game);
            }

            CloseRankEditor(rankEditor);
        }

        private void CancelRankEdit(TextBox rankEditor)
        {
            CloseRankEditor(rankEditor);
        }

        private static void CloseRankEditor(TextBox rankEditor)
        {
            rankEditor.Visibility = System.Windows.Visibility.Collapsed;
            if (rankEditor.Parent is Grid container && container.Children.Count > 0 && container.Children[0] is TextBlock textBlock)
            {
                textBlock.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
