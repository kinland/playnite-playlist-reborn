using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using Playnite.SDK.Models;
using System.Windows.Threading;
using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Interaction logic for PlaylistView.xaml
    /// </summary>
    public partial class PlaylistView : UserControl
    {
        /// <summary>Rows to keep cached above/below the visible (or realized) viewport when leaving Playlist or at startup.</summary>
        private const int HowLongToBeatScrollBufferRows = 5;

        public PlaylistView()
        {
            InitializeComponent();
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloadedPruneHowLongToBeatCache;
        }

        public PlaylistView(PlaylistViewModel model)
        {
            DataContext = model;
            InitializeComponent();
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloadedPruneHowLongToBeatCache;
        }

        private void OnPlaylistViewLoadedApplyHowLongToBeatColumn(object sender, RoutedEventArgs e)
        {
            if (HowLongToBeatControl.HowLongToBeatIsInstalled)
            {
                HowLongToBeatControl.SetCacheCapGames(300);

                if (DataContext is PlaylistViewModel)
                {
                    // On render, start alternating preload around currently cached block
                    // using the actual view order.
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        var inViewOrder = GetCurrentViewOrderedGames();
                        HowLongToBeatControl.QueuePreloadAlternatingCacheMisses(inViewOrder, 300);
                    }), DispatcherPriority.Background);
                }

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
            if (!HowLongToBeatControl.HowLongToBeatIsInstalled)
            {
                return;
            }

            var inViewOrder = GetCurrentViewOrderedGames();
            List<Game> gamesToKeep = GetGamesToKeepWithScrollBuffer(inViewOrder, HowLongToBeatScrollBufferRows);
            HowLongToBeatControl.PruneCacheToGames(gamesToKeep);

            // Warm the same buffered window off-tab so return + small scroll stays smooth.
            HowLongToBeatControl.QueuePreloadGames(gamesToKeep);
            HowLongToBeatControl.QueuePreloadAlternatingCacheMisses(inViewOrder, Math.Max(gamesToKeep.Count, 1));
        }

        private List<Game> GetCurrentViewOrderedGames()
        {
            return playlistListView?.Items?.OfType<Game>().Where(g => g != null).ToList()
                ?? new List<Game>();
        }

        /// <summary>
        /// Min/max index among realized row containers (best proxy for viewport when tab is closing).
        /// </summary>
        private bool TryGetRealizedRowIndexRange(out int minIndex, out int maxIndex)
        {
            minIndex = int.MaxValue;
            maxIndex = -1;
            if (playlistListView == null)
            {
                return false;
            }

            for (int i = 0; i < playlistListView.Items.Count; i++)
            {
                var container = playlistListView.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                if (container != null && playlistListView.Items[i] is Game)
                {
                    if (i < minIndex)
                    {
                        minIndex = i;
                    }

                    if (i > maxIndex)
                    {
                        maxIndex = i;
                    }
                }
            }

            return maxIndex >= 0;
        }

        /// <summary>
        /// Games to retain in HLTB cache: realized viewport ± <paramref name="bufferRows"/>, or first-page fallback with same buffer.
        /// </summary>
        private List<Game> GetGamesToKeepWithScrollBuffer(IReadOnlyList<Game> inViewOrder, int bufferRows)
        {
            int n = inViewOrder.Count;
            if (n == 0)
            {
                return new List<Game>();
            }

            int minIdx;
            int maxIdx;
            if (TryGetRealizedRowIndexRange(out minIdx, out maxIdx))
            {
                int lo = Math.Max(0, minIdx - bufferRows);
                int hi = Math.Min(n - 1, maxIdx + bufferRows);
                return SliceGames(inViewOrder, lo, hi);
            }

            // No realized rows (e.g. unload timing): assume first page on screen, same buffer below (nothing above row 0).
            const int fallbackVisibleCount = 35;
            int fallbackMax = Math.Min(n - 1, fallbackVisibleCount - 1);
            int loFb = Math.Max(0, 0 - bufferRows);
            int hiFb = Math.Min(n - 1, fallbackMax + bufferRows);
            return SliceGames(inViewOrder, loFb, hiFb);
        }

        private static List<Game> SliceGames(IReadOnlyList<Game> inViewOrder, int lo, int hi)
        {
            var list = new List<Game>(hi - lo + 1);
            for (int i = lo; i <= hi; i++)
            {
                Game g = inViewOrder[i];
                if (g != null)
                {
                    list.Add(g);
                }
            }

            return list;
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
