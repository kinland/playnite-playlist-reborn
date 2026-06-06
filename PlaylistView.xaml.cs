using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK.Models;
using System.Windows.Controls.Primitives;

namespace Playlist
{
    /// <summary>
    /// Interaction logic for PlaylistView.xaml
    /// </summary>
    public partial class PlaylistView : UserControl
    {
        private readonly DispatcherTimer lastPlayedRefreshTimer;
        private static readonly TimeSpan LastPlayedRefreshInterval = TimeSpan.FromMinutes(1);

        public PlaylistView()
        {
            InitializeComponent();
            playlistListView.SizeChanged += OnPlaylistListViewSizeChanged;
            playlistListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnPlaylistListViewColumnThumbDragCompleted), handledEventsToo: true);
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloaded;
            lastPlayedRefreshTimer = CreateLastPlayedRefreshTimer();
        }

        public PlaylistView(PlaylistViewModel model)
        {
            DataContext = model;
            InitializeComponent();
            playlistListView.SizeChanged += OnPlaylistListViewSizeChanged;
            playlistListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnPlaylistListViewColumnThumbDragCompleted), handledEventsToo: true);
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloaded;
            lastPlayedRefreshTimer = CreateLastPlayedRefreshTimer();
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
            ApplySettings();
            UpdateLastPlayedTimerState();
        }

        private void OnPlaylistViewUnloaded(object sender, RoutedEventArgs e)
        {
            lastPlayedRefreshTimer?.Stop();
        }

        public void ApplySettings()
        {
            ApplyHowLongToBeatColumnVisibility();
            ApplyLastPlayedColumnVisibility();
            UpdateLastPlayedTimerState();
        }

        private DispatcherTimer CreateLastPlayedRefreshTimer()
        {
            DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = LastPlayedRefreshInterval,
            };
            timer.Tick += OnLastPlayedRefreshTick;
            return timer;
        }

        private void OnLastPlayedRefreshTick(object sender, EventArgs e)
        {
            if (!IsVisible)
            {
                return;
            }

            if (playlistListView?.ItemsSource is System.ComponentModel.ICollectionView view)
            {
                view.Refresh();
            }
        }

        private void UpdateLastPlayedTimerState()
        {
            bool shouldRun = IsVisible && (Playlist.StaticSettings?.ShowLastPlayedColumn ?? false);
            if (shouldRun)
            {
                lastPlayedRefreshTimer?.Start();
            }
            else
            {
                lastPlayedRefreshTimer?.Stop();
            }
        }

        private void ApplyLastPlayedColumnVisibility()
        {
            if (!(playlistListView?.View is GridView gridView) || lastPlayedGridViewColumn == null)
            {
                return;
            }

            bool shouldShow = Playlist.StaticSettings?.ShowLastPlayedColumn ?? true;
            bool currentlyVisible = gridView.Columns.Contains(lastPlayedGridViewColumn);
            if (shouldShow == currentlyVisible)
            {
                return;
            }

            if (!shouldShow)
            {
                gridView.Columns.Remove(lastPlayedGridViewColumn);
                return;
            }

            int targetIndex = howLongToBeatGridViewColumn != null && gridView.Columns.Contains(howLongToBeatGridViewColumn)
                ? gridView.Columns.IndexOf(howLongToBeatGridViewColumn)
                : gridView.Columns.Count;
            gridView.Columns.Insert(targetIndex, lastPlayedGridViewColumn);
        }

        private void ApplyHowLongToBeatColumnVisibility()
        {
            if (!(playlistListView?.View is GridView gridView) || howLongToBeatGridViewColumn == null)
            {
                return;
            }

            bool hltbAvailable = HowLongToBeatCache.IsAvailable(Playlist.StaticPlayniteApi);
            bool settingEnabled = Playlist.StaticSettings?.ShowHowLongToBeatColumn ?? true;
            bool shouldShow = hltbAvailable && settingEnabled;
            bool currentlyVisible = gridView.Columns.Contains(howLongToBeatGridViewColumn);

            if (shouldShow == currentlyVisible)
            {
                if (shouldShow)
                {
                    UpdateHowLongToBeatColumnFillWidth();
                }
                return;
            }

            if (!shouldShow)
            {
                gridView.Columns.Remove(howLongToBeatGridViewColumn);
                return;
            }

            gridView.Columns.Add(howLongToBeatGridViewColumn);
            UpdateHowLongToBeatColumnFillWidth();
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

            string sortKey = null;
            if (column == rankGridViewColumn)
            {
                sortKey = "Rank";
            }
            else if (column == nameGridViewColumn)
            {
                sortKey = "Name";
            }
            else if (column == playtimeGridViewColumn)
            {
                sortKey = "Playtime";
            }
            else if (column == completionStatusGridViewColumn)
            {
                sortKey = "CompletionStatus";
            }
            else if (column == lastPlayedGridViewColumn)
            {
                sortKey = "LastPlayed";
            }

            if (string.IsNullOrEmpty(sortKey))
            {
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

        private void OnPlaylistSearchTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox searchTextBox))
            {
                return;
            }

            // Explicitly handle Ctrl+X so parent-level shortcuts do not steal Cut.
            if (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                TryManualCut(searchTextBox);
                e.Handled = true;
            }
        }

        private static void TryManualCut(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            string selectedText = textBox.SelectedText;
            if (string.IsNullOrEmpty(selectedText))
            {
                return;
            }

            int selectionStart = textBox.SelectionStart;
            textBox.SelectedText = string.Empty;
            textBox.CaretIndex = selectionStart;

            // Clipboard writes can block when another process is locking it.
            // Do this after UI mutation so cut feels instant.
            textBox.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    Clipboard.SetText(selectedText);
                }
                catch
                {
                }
            }));
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
