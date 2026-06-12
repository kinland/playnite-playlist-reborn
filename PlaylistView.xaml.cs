using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
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
        private bool isRestoringLayoutState;
        private INotifyCollectionChanged gridColumnsNotifier;
        private const string RankColumnKey = "Rank";
        private const string IconColumnKey = "Icon";
        private const string NameColumnKey = "Name";
        private const string PlaytimeColumnKey = "Playtime";
        private const string CompletionStatusColumnKey = "CompletionStatus";
        private const string LastPlayedColumnKey = "LastPlayed";
        private const string LastActivityColumnKey = "LastActivity";
        private const string HowLongToBeatColumnKey = "HowLongToBeat";

        public PlaylistView()
        {
            InitializeComponent();
            playlistListView.SizeChanged += OnPlaylistListViewSizeChanged;
            playlistListView.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnPlaylistListViewColumnThumbDragDelta), handledEventsToo: true);
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
            playlistListView.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnPlaylistListViewColumnThumbDragDelta), handledEventsToo: true);
            playlistListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnPlaylistListViewColumnThumbDragCompleted), handledEventsToo: true);
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloaded;
            lastPlayedRefreshTimer = CreateLastPlayedRefreshTimer();
        }

        private const double HowLongToBeatColumnMinWidth = 120;

        private void OnPlaylistListViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateHowLongToBeatColumnFillWidth();
            RefreshSortHeaderVisualState();
        }

        private void OnPlaylistListViewColumnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            UpdateHowLongToBeatColumnFillWidth();
            RefreshSortHeaderVisualState();
            PersistLayoutState();
        }

        private void OnPlaylistListViewColumnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            RefreshSortHeaderVisualState();
        }

        /// <summary>
        /// Keep HLTB column at the current user-selected width and only enforce a small minimum.
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
            (DataContext as PlaylistViewModel)?.RefreshHowLongToBeatHeaderText();
            ApplySettings();
            SubscribeGridColumnCollectionChanged();
            RestoreLayoutState();
            UpdateLastPlayedTimerState();
            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
        }

        private void OnPlaylistViewUnloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeGridColumnCollectionChanged();
            PersistLayoutState();
            lastPlayedRefreshTimer?.Stop();
        }

        public void ApplySettings()
        {
            ApplyColumnVisibility();
            (DataContext as PlaylistViewModel)?.RefreshHowLongToBeatHeaderText();
            RestoreLayoutState();
            UpdateLastPlayedTimerState();
            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
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
            PlaylistSettings settings = Playlist.StaticSettings;
            // Both the Last Played and Last Activity columns render relative ("x minutes ago") labels
            // that must tick over time.
            bool relativeColumnVisible = (settings?.ShowLastPlayedColumn ?? false) || (settings?.ShowLastActivityColumn ?? false);
            bool shouldRun = IsVisible && relativeColumnVisible;
            if (shouldRun)
            {
                lastPlayedRefreshTimer?.Start();
            }
            else
            {
                lastPlayedRefreshTimer?.Stop();
            }
        }

        /// <summary>
        /// Ensures each toggleable column's presence matches the persisted visibility settings.
        /// Name and the icon column are always shown (they identify the game). HowLongToBeat additionally
        /// requires integration to be enabled in settings and the HowLongToBeat plugin to be available;
        /// visibility is controlled separately via the header right-click menu.
        /// </summary>
        private void ApplyColumnVisibility()
        {
            if (!(playlistListView?.View is GridView gridView))
            {
                return;
            }

            PlaylistSettings settings = Playlist.StaticSettings;
            bool hltbIntegrationEnabled = settings?.EnableHowLongToBeatIntegration ?? true;
            bool hltbAvailable = HowLongToBeatCache.IsAvailable(Playlist.StaticPlayniteApi);
            bool showHowLongToBeatColumn = hltbIntegrationEnabled
                && hltbAvailable
                && (settings?.ShowHowLongToBeatColumn ?? true);

            SetColumnVisible(gridView, rankGridViewColumn, settings?.ShowRankColumn ?? true);
            SetColumnVisible(gridView, playtimeGridViewColumn, settings?.ShowPlaytimeColumn ?? true);
            SetColumnVisible(gridView, completionStatusGridViewColumn, settings?.ShowCompletionStatusColumn ?? true);
            SetColumnVisible(gridView, lastPlayedGridViewColumn, settings?.ShowLastPlayedColumn ?? true);
            SetColumnVisible(gridView, lastActivityGridViewColumn, settings?.ShowLastActivityColumn ?? false);
            SetColumnVisible(gridView, howLongToBeatGridViewColumn, showHowLongToBeatColumn);

            UpdateHowLongToBeatColumnFillWidth();
        }

        private void SetColumnVisible(GridView gridView, GridViewColumn column, bool shouldShow)
        {
            if (column == null)
            {
                return;
            }

            bool present = gridView.Columns.Contains(column);
            if (shouldShow == present)
            {
                return;
            }

            if (!shouldShow)
            {
                gridView.Columns.Remove(column);
                return;
            }

            gridView.Columns.Insert(ComputeCanonicalInsertIndex(gridView, column), column);
        }

        /// <summary>
        /// Canonical left-to-right column order used to place a newly shown column sensibly.
        /// </summary>
        private int GetCanonicalColumnOrder(GridViewColumn column)
        {
            if (column == rankGridViewColumn) return 0;
            if (column == iconGridViewColumn) return 1;
            if (column == nameGridViewColumn) return 2;
            if (column == playtimeGridViewColumn) return 3;
            if (column == completionStatusGridViewColumn) return 4;
            if (column == lastPlayedGridViewColumn) return 5;
            if (column == lastActivityGridViewColumn) return 6;
            if (column == howLongToBeatGridViewColumn) return 7;
            return int.MaxValue;
        }

        private int ComputeCanonicalInsertIndex(GridView gridView, GridViewColumn column)
        {
            int target = GetCanonicalColumnOrder(column);
            int index = 0;
            foreach (GridViewColumn present in gridView.Columns)
            {
                if (GetCanonicalColumnOrder(present) < target)
                {
                    index++;
                }
            }

            return index;
        }

        private void OnPlaylistHeaderPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            GridViewColumnHeader header = FindAncestor<GridViewColumnHeader>(e.OriginalSource as DependencyObject);
            if (header == null || header.Role == GridViewColumnHeaderRole.Padding)
            {
                // Not a column header (e.g. a row) — let the default context menu handling proceed.
                return;
            }

            ContextMenu menu = BuildColumnVisibilityMenu();
            if (menu == null)
            {
                return;
            }

            menu.PlacementTarget = header;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Builds the column header right-click menu of checkable column-visibility toggles.
        /// The game's Name and icon are always shown and are intentionally not listed.
        /// </summary>
        private ContextMenu BuildColumnVisibilityMenu()
        {
            PlaylistSettings settings = Playlist.StaticSettings;
            if (settings == null)
            {
                return null;
            }

            ContextMenu menu = new ContextMenu();
            menu.Items.Add(BuildColumnToggleItem(RankColumnKey, ResourceProvider.GetString("LOCPlaylist_Column_Rank"), settings.ShowRankColumn, true, null));
            menu.Items.Add(BuildColumnToggleItem(PlaytimeColumnKey, ResourceProvider.GetString("LOCTimePlayed"), settings.ShowPlaytimeColumn, true, null));
            menu.Items.Add(BuildColumnToggleItem(CompletionStatusColumnKey, ResourceProvider.GetString("LOCCompletionStatus"), settings.ShowCompletionStatusColumn, true, null));
            menu.Items.Add(BuildColumnToggleItem(LastPlayedColumnKey, ResourceProvider.GetString("LOCPlaylist_LastPlayedColumn"), settings.ShowLastPlayedColumn, true, null));
            menu.Items.Add(BuildColumnToggleItem(LastActivityColumnKey, ResourceProvider.GetString("LOCPlaylist_LastActivityColumn"), settings.ShowLastActivityColumn, true, null));

            menu.Items.Add(BuildHowLongToBeatColumnMenuItem(settings));

            return menu;
        }

        /// <summary>
        /// Whether the HowLongToBeat column is actually on screen (integration, plugin, and visibility flag).
        /// </summary>
        private static bool IsHowLongToBeatColumnVisible(PlaylistSettings settings)
        {
            if (settings == null || !settings.EnableHowLongToBeatIntegration)
            {
                return false;
            }

            if (!HowLongToBeatCache.IsAvailable(Playlist.StaticPlayniteApi))
            {
                return false;
            }

            return settings.ShowHowLongToBeatColumn;
        }

        /// <summary>
        /// HowLongToBeat is a normal visibility toggle when integration is on. When integration is off but the
        /// HLTB plugin is installed, the item stays clickable and opens this plugin's settings instead.
        /// </summary>
        private MenuItem BuildHowLongToBeatColumnMenuItem(PlaylistSettings settings)
        {
            string header = ResourceProvider.GetString("LOCPlaylist_Column_HowLongToBeat");
            bool hltbAvailable = HowLongToBeatCache.IsAvailable(Playlist.StaticPlayniteApi);
            bool columnVisible = IsHowLongToBeatColumnVisible(settings);

            if (!hltbAvailable)
            {
                return BuildColumnToggleItem(
                    HowLongToBeatColumnKey,
                    header,
                    isChecked: false,
                    isEnabled: false,
                    disabledToolTip: ResourceProvider.GetString("LOCPlaylist_HltbRequiresPlugin"));
            }

            if (!settings.EnableHowLongToBeatIntegration)
            {
                MenuItem item = new MenuItem
                {
                    Header = header,
                    IsCheckable = true,
                    IsChecked = false,
                    IsEnabled = true,
                    ToolTip = ResourceProvider.GetString("LOCPlaylist_HltbOpenSettingsToEnable"),
                };
                // MenuItem auto-toggles IsChecked on click; keep unchecked until integration is enabled.
                item.Click += (s, e) =>
                {
                    item.IsChecked = false;
                    OpenPlaylistPluginSettings();
                };
                return item;
            }

            return BuildColumnToggleItem(
                HowLongToBeatColumnKey,
                header,
                columnVisible,
                isEnabled: true,
                disabledToolTip: null);
        }

        /// <summary>
        /// Opens this extension's settings dialog (Playnite SDK <c>Plugin.OpenSettingsView</c>, desktop only).
        /// </summary>
        private void OpenPlaylistPluginSettings()
        {
            Playlist.StaticPluginInstance?.OpenSettingsView();
            ApplyColumnVisibility();
            UpdateLastPlayedTimerState();
            PersistLayoutState();
            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
        }

        private MenuItem BuildColumnToggleItem(string columnKey, string header, bool isChecked, bool isEnabled, string disabledToolTip)
        {
            MenuItem item = new MenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = isChecked,
                IsEnabled = isEnabled,
            };

            if (!string.IsNullOrEmpty(disabledToolTip))
            {
                item.ToolTip = disabledToolTip;
            }

            item.Click += (s, e) => ToggleColumnVisibility(columnKey);
            return item;
        }

        private void ToggleColumnVisibility(string columnKey)
        {
            PlaylistSettings settings = Playlist.StaticSettings;
            if (settings == null)
            {
                return;
            }

            switch (columnKey)
            {
                case RankColumnKey:
                    settings.ShowRankColumn = !settings.ShowRankColumn;
                    break;
                case PlaytimeColumnKey:
                    settings.ShowPlaytimeColumn = !settings.ShowPlaytimeColumn;
                    break;
                case CompletionStatusColumnKey:
                    settings.ShowCompletionStatusColumn = !settings.ShowCompletionStatusColumn;
                    break;
                case LastPlayedColumnKey:
                    settings.ShowLastPlayedColumn = !settings.ShowLastPlayedColumn;
                    break;
                case LastActivityColumnKey:
                    settings.ShowLastActivityColumn = !settings.ShowLastActivityColumn;
                    break;
                case HowLongToBeatColumnKey:
                    settings.ShowHowLongToBeatColumn = !settings.ShowHowLongToBeatColumn;
                    break;
                default:
                    return;
            }

            ApplyColumnVisibility();
            UpdateLastPlayedTimerState();
            PersistLayoutState();
            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
        }

        private static T FindAncestor<T>(DependencyObject from) where T : DependencyObject
        {
            DependencyObject current = from;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                DependencyObject parent = null;
                if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                {
                    parent = VisualTreeHelper.GetParent(current);
                }

                if (parent == null)
                {
                    parent = LogicalTreeHelper.GetParent(current);
                }

                current = parent;
            }

            return null;
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
            else if (column == lastActivityGridViewColumn)
            {
                sortKey = "LastActivity";
            }
            else if (column == howLongToBeatGridViewColumn)
            {
                sortKey = "HowLongToBeat";
            }

            if (string.IsNullOrEmpty(sortKey))
            {
                return;
            }

            model.ToggleViewSort(sortKey);
            PersistLayoutState();
            RefreshSortHeaderVisualState();
        }

        private const string RankCellTag = "PlaylistRankCell";
        private const string RankHeaderHashTag = "RankHeaderHash";
        private const string RankHeaderGlyphTag = "RankHeaderGlyph";
        private const double RankHeaderGlyphGap = 2;
        private const double RankHeaderRightInset = 4;
        private const double SortHeaderRightEdgeReserve = 12;

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

        private void PersistLayoutState()
        {
            if (isRestoringLayoutState)
            {
                return;
            }

            if (!(DataContext is PlaylistViewModel model) || !(Playlist.StaticSettings is PlaylistSettings settings))
            {
                return;
            }

            if (!TryGetGridView(out GridView gridView))
            {
                return;
            }

            List<PlaylistColumnLayoutState> layouts = gridView.Columns
                .Select((column, index) => new { Column = column, Index = index })
                .Select(item =>
                {
                    string key = GetColumnKey(item.Column);
                    if (string.IsNullOrEmpty(key))
                    {
                        return null;
                    }

                    return new PlaylistColumnLayoutState
                    {
                        Key = key,
                        DisplayIndex = item.Index,
                        Width = item.Column.Width,
                    };
                })
                .Where(item => item != null)
                .ToList();

            settings.SaveRuntimeState(
                model.ActiveViewSortColumn,
                model.ActiveViewSortDirection,
                layouts);
        }

        private void SubscribeGridColumnCollectionChanged()
        {
            UnsubscribeGridColumnCollectionChanged();
            if (!TryGetGridView(out GridView gridView))
            {
                return;
            }

            gridColumnsNotifier = gridView.Columns as INotifyCollectionChanged;
            if (gridColumnsNotifier != null)
            {
                gridColumnsNotifier.CollectionChanged += OnGridColumnsCollectionChanged;
            }
        }

        private void UnsubscribeGridColumnCollectionChanged()
        {
            if (gridColumnsNotifier != null)
            {
                gridColumnsNotifier.CollectionChanged -= OnGridColumnsCollectionChanged;
                gridColumnsNotifier = null;
            }
        }

        private void OnGridColumnsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PersistLayoutState();
        }

        private void RestoreLayoutState()
        {
            if (isRestoringLayoutState)
            {
                return;
            }

            if (!(DataContext is PlaylistViewModel model) || !(Playlist.StaticSettings is PlaylistSettings settings))
            {
                return;
            }

            if (!TryGetGridView(out GridView gridView))
            {
                return;
            }

            isRestoringLayoutState = true;
            try
            {
                if (settings.ColumnLayouts != null && settings.ColumnLayouts.Count > 0)
                {
                    List<PlaylistColumnLayoutState> ordered = settings.ColumnLayouts
                        .Where(layout => layout != null && !string.IsNullOrWhiteSpace(layout.Key))
                        .OrderBy(layout => layout.DisplayIndex)
                        .ToList();

                    int targetIndex = 0;
                    foreach (PlaylistColumnLayoutState layout in ordered)
                    {
                        GridViewColumn column = GetColumnByKey(layout.Key);
                        if (column == null || !gridView.Columns.Contains(column))
                        {
                            continue;
                        }

                        int currentIndex = gridView.Columns.IndexOf(column);
                        if (currentIndex != targetIndex)
                        {
                            gridView.Columns.RemoveAt(currentIndex);
                            gridView.Columns.Insert(targetIndex, column);
                        }

                        targetIndex++;
                    }

                    foreach (PlaylistColumnLayoutState layout in ordered)
                    {
                        GridViewColumn column = GetColumnByKey(layout.Key);
                        if (column == null || !gridView.Columns.Contains(column))
                        {
                            continue;
                        }

                        if (!double.IsNaN(layout.Width) && layout.Width > 0)
                        {
                            column.Width = layout.Width;
                        }
                    }

                    UpdateHowLongToBeatColumnFillWidth();
                }

                if (!string.IsNullOrWhiteSpace(settings.ActiveSortColumnKey))
                {
                    model.RestoreViewSort(settings.ActiveSortColumnKey, settings.ActiveSortDirection);
                }
            }
            finally
            {
                isRestoringLayoutState = false;
            }

            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
        }

        private void RefreshSortHeaderVisualState()
        {
            if (!(DataContext is PlaylistViewModel model) || playlistListView == null)
            {
                return;
            }

            GridViewColumn activeColumn = GetColumnByKey(model.ActiveViewSortColumn);
            bool foundHeader = false;
            foreach (GridViewColumnHeader header in FindVisualChildren<GridViewColumnHeader>(playlistListView))
            {
                if (header == null)
                {
                    continue;
                }

                if (header.Role == GridViewColumnHeaderRole.Padding)
                {
                    HideGridViewPaddingColumnHeader(header);
                    continue;
                }

                if (!IsSortableColumn(header.Column))
                {
                    continue;
                }

                foundHeader = true;

                // Ensure header content can consume full cell width for right-aligned glyphs.
                ContentPresenter presenter = FindFirstVisualChild<ContentPresenter>(header);
                if (presenter != null)
                {
                    presenter.HorizontalAlignment = HorizontalAlignment.Stretch;
                    double availableWidth = Math.Max(0, header.ActualWidth - header.Padding.Left - header.Padding.Right - SortHeaderRightEdgeReserve);
                    presenter.Width = availableWidth;

                    if (header.Column == rankGridViewColumn)
                    {
                        UpdateRankHeaderCoupledLayout(header, presenter, availableWidth);
                    }
                }

                Border rootBorder = FindFirstVisualChild<Border>(header);
                if (header.Column == activeColumn)
                {
                    if (rootBorder != null)
                    {
                        rootBorder.Background = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
                        rootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
                    }
                    else
                    {
                        header.Background = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
                        header.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
                    }
                }
                else
                {
                    if (rootBorder != null)
                    {
                        rootBorder.ClearValue(Border.BackgroundProperty);
                        rootBorder.ClearValue(Border.BorderBrushProperty);
                    }

                    header.ClearValue(Control.BackgroundProperty);
                    header.ClearValue(Control.BorderBrushProperty);
                }
            }

            if (!foundHeader)
            {
                Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// GridView adds a filler header when columns do not span the full list width. Hide it so
        /// the unused area does not look like an extra resizable column.
        /// </summary>
        private static void HideGridViewPaddingColumnHeader(GridViewColumnHeader header)
        {
            header.Visibility = Visibility.Collapsed;
            header.Width = 0;
            header.MinWidth = 0;
            header.MaxWidth = 0;
            header.IsHitTestVisible = false;
        }

        /// <summary>
        /// Wide rank headers overlay a right-pinned glyph on a centered '#'. Once space tightens,
        /// switch both to a right-anchored pair so they move together at the same rate.
        /// </summary>
        private static void UpdateRankHeaderCoupledLayout(GridViewColumnHeader header, ContentPresenter presenter, double availableWidth)
        {
            TextBlock hashBlock = FindChildByTag<TextBlock>(presenter, RankHeaderHashTag);
            TextBlock glyphBlock = FindChildByTag<TextBlock>(presenter, RankHeaderGlyphTag);
            if (hashBlock == null)
            {
                return;
            }

            if (glyphBlock == null || glyphBlock.Visibility != Visibility.Visible)
            {
                hashBlock.HorizontalAlignment = HorizontalAlignment.Center;
                hashBlock.ClearValue(FrameworkElement.MarginProperty);
                return;
            }

            double hashWidth = hashBlock.ActualWidth > 0 ? hashBlock.ActualWidth : 10;
            double glyphWidth = glyphBlock.ActualWidth > 0 ? glyphBlock.ActualWidth : 11;
            double coupleThreshold = hashWidth + (2 * (glyphWidth + RankHeaderGlyphGap + RankHeaderRightInset));

            if (availableWidth <= coupleThreshold)
            {
                hashBlock.HorizontalAlignment = HorizontalAlignment.Right;
                hashBlock.Margin = new Thickness(0, 0, glyphWidth + RankHeaderGlyphGap + RankHeaderRightInset, 0);
                return;
            }

            hashBlock.HorizontalAlignment = HorizontalAlignment.Center;
            hashBlock.ClearValue(FrameworkElement.MarginProperty);
        }

        private static T FindChildByTag<T>(DependencyObject parent, string tag) where T : FrameworkElement
        {
            foreach (T child in FindVisualChildren<T>(parent))
            {
                if (child.Tag as string == tag)
                {
                    return child;
                }
            }

            return null;
        }

        private bool IsSortableColumn(GridViewColumn column)
        {
            return column == rankGridViewColumn
                || column == nameGridViewColumn
                || column == playtimeGridViewColumn
                || column == completionStatusGridViewColumn
                || column == lastPlayedGridViewColumn
                || column == lastActivityGridViewColumn
                || column == howLongToBeatGridViewColumn;
        }

        private static T FindFirstVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (T child in FindVisualChildren<T>(parent))
            {
                return child;
            }

            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                yield break;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T nestedChild in FindVisualChildren<T>(child))
                {
                    yield return nestedChild;
                }
            }
        }

        private bool TryGetGridView(out GridView gridView)
        {
            gridView = playlistListView?.View as GridView;
            return gridView != null;
        }

        private string GetColumnKey(GridViewColumn column)
        {
            if (column == rankGridViewColumn)
            {
                return RankColumnKey;
            }

            if (column == iconGridViewColumn)
            {
                return IconColumnKey;
            }

            if (column == nameGridViewColumn)
            {
                return NameColumnKey;
            }

            if (column == playtimeGridViewColumn)
            {
                return PlaytimeColumnKey;
            }

            if (column == completionStatusGridViewColumn)
            {
                return CompletionStatusColumnKey;
            }

            if (column == lastPlayedGridViewColumn)
            {
                return LastPlayedColumnKey;
            }

            if (column == lastActivityGridViewColumn)
            {
                return LastActivityColumnKey;
            }

            if (column == howLongToBeatGridViewColumn)
            {
                return HowLongToBeatColumnKey;
            }

            return null;
        }

        private GridViewColumn GetColumnByKey(string key)
        {
            switch (key)
            {
                case RankColumnKey:
                    return rankGridViewColumn;
                case IconColumnKey:
                    return iconGridViewColumn;
                case NameColumnKey:
                    return nameGridViewColumn;
                case PlaytimeColumnKey:
                    return playtimeGridViewColumn;
                case CompletionStatusColumnKey:
                    return completionStatusGridViewColumn;
                case LastPlayedColumnKey:
                    return lastPlayedGridViewColumn;
                case LastActivityColumnKey:
                    return lastActivityGridViewColumn;
                case HowLongToBeatColumnKey:
                    return howLongToBeatGridViewColumn;
                default:
                    return null;
            }
        }
    }
}
