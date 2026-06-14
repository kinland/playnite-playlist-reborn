using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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

        private readonly HashSet<GridViewColumnHeader> sortHeaderMouseHooked = new HashSet<GridViewColumnHeader>();
        private ListViewItem hoveredRowHighlightItem;
        private ScrollViewer rowHighlightScrollViewer;
        private Style playButtonThemedStyle;
        private Style playButtonManagedStyle;
        private readonly Dictionary<Button, ListViewItem> playButtonRowContext = new Dictionary<Button, ListViewItem>();
        private int syncedHeaderBodyOffsetPixels = int.MinValue;
        private bool headerBodyOffsetLocked;
        private bool rowHighlightRefreshPending;
        private readonly PlaylistColumnReorderDropIndicator columnReorderDropIndicator;
        private readonly PlaylistDragReorderStatusIndicator dragReorderStatusIndicator;
        private INotifyPropertyChanged dragReorderStatusViewModel;
        private bool dragReorderGiveFeedbackHooked;
        private bool dragReorderQueryContinueDragHooked;

        /// <summary>
        /// Left margin applied to each row's <see cref="PlaylistGridViewRowPresenter"/> so body cells
        /// align with column headers when the host applies extra header-row inset.
        /// </summary>
        public static readonly DependencyProperty GridViewRowPresenterMarginProperty =
            DependencyProperty.Register(
                nameof(GridViewRowPresenterMargin),
                typeof(Thickness),
                typeof(PlaylistView),
                new PropertyMetadata(new Thickness(0)));

        public Thickness GridViewRowPresenterMargin
        {
            get => (Thickness)GetValue(GridViewRowPresenterMarginProperty);
            set => SetValue(GridViewRowPresenterMarginProperty, value);
        }

        public PlaylistView()
        {
            InitializeComponent();
            columnReorderDropIndicator = new PlaylistColumnReorderDropIndicator(playlistListView);
            columnReorderDropIndicator.Attach();
            dragReorderStatusIndicator = new PlaylistDragReorderStatusIndicator(playlistListView);
            playlistListView.SizeChanged += OnPlaylistListViewSizeChanged;
            playlistListView.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnPlaylistListViewColumnThumbDragDelta), handledEventsToo: true);
            playlistListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnPlaylistListViewColumnThumbDragCompleted), handledEventsToo: true);
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloaded;
            IsVisibleChanged += OnPlaylistViewIsVisibleChanged;
            DataContextChanged += OnPlaylistViewDataContextChanged;
            lastPlayedRefreshTimer = CreateLastPlayedRefreshTimer();
        }

        public PlaylistView(PlaylistViewModel model)
        {
            DataContext = model;
            InitializeComponent();
            columnReorderDropIndicator = new PlaylistColumnReorderDropIndicator(playlistListView);
            columnReorderDropIndicator.Attach();
            dragReorderStatusIndicator = new PlaylistDragReorderStatusIndicator(playlistListView);
            playlistListView.SizeChanged += OnPlaylistListViewSizeChanged;
            playlistListView.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnPlaylistListViewColumnThumbDragDelta), handledEventsToo: true);
            playlistListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnPlaylistListViewColumnThumbDragCompleted), handledEventsToo: true);
            Loaded += OnPlaylistViewLoadedApplyHowLongToBeatColumn;
            Unloaded += OnPlaylistViewUnloaded;
            IsVisibleChanged += OnPlaylistViewIsVisibleChanged;
            DataContextChanged += OnPlaylistViewDataContextChanged;
            lastPlayedRefreshTimer = CreateLastPlayedRefreshTimer();
        }

        private void OnPlaylistViewDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SubscribeDragReorderStatusPopup();
        }

        private void OnPlaylistViewIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible)
            {
                return;
            }

            ApplyColumnVisibility();
            (DataContext as PlaylistViewModel)?.RefreshHowLongToBeatHeaderText();
        }

        private void EnsureSortHeaderMouseHook(GridViewColumnHeader header)
        {
            if (header == null || !sortHeaderMouseHooked.Add(header))
            {
                return;
            }

            DependencyPropertyDescriptor
                .FromProperty(UIElement.IsMouseOverProperty, typeof(GridViewColumnHeader))
                ?.AddValueChanged(header, OnSortHeaderIsMouseOverChanged);
        }

        private void DetachSortHeaderMouseHooks()
        {
            foreach (GridViewColumnHeader header in sortHeaderMouseHooked)
            {
                DependencyPropertyDescriptor
                    .FromProperty(UIElement.IsMouseOverProperty, typeof(GridViewColumnHeader))
                    ?.RemoveValueChanged(header, OnSortHeaderIsMouseOverChanged);
            }

            sortHeaderMouseHooked.Clear();
        }

        private void SubscribeRowHighlightHooks()
        {
            if (playlistListView == null)
            {
                return;
            }

            playlistListView.PreviewMouseMove += OnPlaylistListViewPreviewMouseMove;
            playlistListView.MouseLeave += OnPlaylistListViewMouseLeave;
            playlistListView.SelectionChanged += OnPlaylistListViewSelectionChanged;
            playlistListView.ItemContainerGenerator.StatusChanged += OnRowItemContainerGeneratorStatusChanged;

            rowHighlightScrollViewer = PlaylistVisualTree.FindFirstVisualChild<ScrollViewer>(playlistListView);
            if (rowHighlightScrollViewer != null)
            {
                rowHighlightScrollViewer.ScrollChanged += OnPlaylistListViewScrollChanged;
            }

            UpdateHoveredRowHighlight(forceRefresh: true);
            RefreshAllGeneratedRowHighlights();
        }

        private void UnsubscribeRowHighlightHooks()
        {
            if (playlistListView == null)
            {
                return;
            }

            playlistListView.PreviewMouseMove -= OnPlaylistListViewPreviewMouseMove;
            playlistListView.MouseLeave -= OnPlaylistListViewMouseLeave;
            playlistListView.SelectionChanged -= OnPlaylistListViewSelectionChanged;
            playlistListView.ItemContainerGenerator.StatusChanged -= OnRowItemContainerGeneratorStatusChanged;

            if (rowHighlightScrollViewer != null)
            {
                rowHighlightScrollViewer.ScrollChanged -= OnPlaylistListViewScrollChanged;
                rowHighlightScrollViewer = null;
            }

            hoveredRowHighlightItem = null;
        }

        private void OnPlaylistListViewPreviewMouseMove(object sender, MouseEventArgs e)
        {
            UpdateHoveredRowHighlight(forceRefresh: false, e);
        }

        private void OnRowItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (rowHighlightRefreshPending)
            {
                return;
            }

            rowHighlightRefreshPending = true;
            Dispatcher.BeginInvoke((Action)(() =>
            {
                rowHighlightRefreshPending = false;
                RefreshAllGeneratedRowHighlights();
            }), DispatcherPriority.Loaded);
        }

        private void RefreshAllGeneratedRowHighlights()
        {
            if (playlistListView?.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                return;
            }

            bool usesInvertedChrome = PlaylistThemeColors.UsesInvertedRowHighlightChrome(TryFindResource);
            foreach (object item in playlistListView.Items)
            {
                if (playlistListView.ItemContainerGenerator.ContainerFromItem(item) is not ListViewItem listViewItem)
                {
                    continue;
                }

                bool isHoverActive = ReferenceEquals(listViewItem, hoveredRowHighlightItem);
                if (usesInvertedChrome && (listViewItem.IsSelected || isHoverActive))
                {
                    ApplyRowHighlightForeground(listViewItem);
                }
                else
                {
                    ClearRowHighlightVisuals(listViewItem);
                }
            }
        }

        private void OnPlaylistListViewMouseLeave(object sender, MouseEventArgs e)
        {
            ListViewItem previous = hoveredRowHighlightItem;
            hoveredRowHighlightItem = null;
            if (previous != null)
            {
                ApplyRowHighlightForeground(previous);
            }
        }

        private void OnPlaylistListViewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0 || e.ViewportHeightChange != 0)
            {
                UpdateHoveredRowHighlight(forceRefresh: true);
            }
        }

        private void OnPlaylistListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (object item in e.RemovedItems)
            {
                ApplyRowHighlightToDataItem(item);
            }

            foreach (object item in e.AddedItems)
            {
                ApplyRowHighlightToDataItem(item);
            }

            UpdateHoveredRowHighlight(forceRefresh: true);
        }

        private void ApplyRowHighlightToDataItem(object item)
        {
            if (playlistListView?.ItemContainerGenerator.ContainerFromItem(item) is ListViewItem listViewItem)
            {
                ApplyRowHighlightForeground(listViewItem);
            }
        }

        private void UpdateHoveredRowHighlight(bool forceRefresh, MouseEventArgs e = null)
        {
            ListViewItem current = GetListViewItemUnderMouse(e);
            if (!forceRefresh && ReferenceEquals(current, hoveredRowHighlightItem))
            {
                return;
            }

            ListViewItem previous = hoveredRowHighlightItem;
            hoveredRowHighlightItem = current;

            if (previous != null && !ReferenceEquals(previous, current))
            {
                ApplyRowHighlightForeground(previous);
            }

            if (current != null)
            {
                ApplyRowHighlightForeground(current);
            }
        }

        private ListViewItem GetListViewItemUnderMouse(MouseEventArgs e = null)
        {
            if (playlistListView == null)
            {
                return null;
            }

            Point position = e != null
                ? e.GetPosition(playlistListView)
                : Mouse.GetPosition(playlistListView);
            if (position.X < 0 || position.Y < 0
                || position.X > playlistListView.ActualWidth
                || position.Y > playlistListView.ActualHeight)
            {
                return null;
            }

            HitTestResult hit = VisualTreeHelper.HitTest(playlistListView, position);
            for (DependencyObject node = hit?.VisualHit; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is ListViewItem listViewItem)
                {
                    return listViewItem;
                }
            }

            return null;
        }

        private void ApplyRowHighlightForeground(ListViewItem item)
        {
            if (item == null)
            {
                return;
            }

            bool isHoverActive = ReferenceEquals(item, hoveredRowHighlightItem);
            PlaylistManagedRowChrome.ApplyListRowHighlightForeground(item, TryFindResource, isHoverActive);
            SyncRowHighlightVisuals(item, isHoverActive);
        }

        private void ClearRowHighlightVisuals(ListViewItem item)
        {
            if (item == null)
            {
                return;
            }

            item.ClearValue(Control.ForegroundProperty);

            foreach (HowLongToBeatPluginButtonHost host in PlaylistVisualTree.FindVisualChildren<HowLongToBeatPluginButtonHost>(item))
            {
                host.ClearHighlightChrome();
            }

            foreach (HowLongToBeatCachedProgressBar bar in PlaylistVisualTree.FindVisualChildren<HowLongToBeatCachedProgressBar>(item))
            {
                bar.SyncRowForegroundFromListViewItem(isHoverActive: false);
            }

            foreach (Button button in PlaylistVisualTree.FindVisualChildren<Button>(item))
            {
                SyncPlayButtonStyle(button, item, isHoverActive: false);
            }
        }

        private void SyncRowHighlightVisuals(ListViewItem item, bool isHoverActive)
        {
            foreach (HowLongToBeatPluginButtonHost host in PlaylistVisualTree.FindVisualChildren<HowLongToBeatPluginButtonHost>(item))
            {
                host.SyncRowHighlightFromListViewItem(isHoverActive);
            }

            foreach (HowLongToBeatCachedProgressBar bar in PlaylistVisualTree.FindVisualChildren<HowLongToBeatCachedProgressBar>(item))
            {
                bar.SyncRowForegroundFromListViewItem(isHoverActive);
            }

            foreach (Button button in PlaylistVisualTree.FindVisualChildren<Button>(item))
            {
                SyncPlayButtonStyle(button, item, isHoverActive);
            }
        }

        private void EnsurePlayButtonStyles()
        {
            if (playButtonThemedStyle == null)
            {
                playButtonThemedStyle = FindResource("playlistIconPlayButtonStyleThemed") as Style;
                playButtonManagedStyle = FindResource("playlistIconPlayButtonStyleManaged") as Style;
            }
        }

        private void SyncPlayButtonStyle(Button button, ListViewItem item, bool isHoverActive)
        {
            EnsurePlayButtonStyles();
            if (PlaylistThemeColors.UsesInvertedRowHighlightChrome(TryFindResource))
            {
                if (playButtonManagedStyle != null && !ReferenceEquals(button.Style, playButtonManagedStyle))
                {
                    button.Style = playButtonManagedStyle;
                }

                EnsurePlayButtonDirectHoverHook(button, item);
                ApplyManagedPlayButtonChrome(button, item);
                return;
            }

            ReleasePlayButtonDirectHoverHook(button);
            if (playButtonThemedStyle != null)
            {
                button.Style = playButtonThemedStyle;
            }

            PlaylistManagedRowChrome.ClearListRowControlChrome(button);
        }

        private void EnsurePlayButtonDirectHoverHook(Button button, ListViewItem item)
        {
            playButtonRowContext[button] = item;
            button.MouseEnter -= OnPlayButtonDirectHoverChanged;
            button.MouseLeave -= OnPlayButtonDirectHoverChanged;
            button.MouseEnter += OnPlayButtonDirectHoverChanged;
            button.MouseLeave += OnPlayButtonDirectHoverChanged;
        }

        private void ReleasePlayButtonDirectHoverHook(Button button)
        {
            button.MouseEnter -= OnPlayButtonDirectHoverChanged;
            button.MouseLeave -= OnPlayButtonDirectHoverChanged;
            playButtonRowContext.Remove(button);
        }

        private void OnPlayButtonDirectHoverChanged(object sender, MouseEventArgs e)
        {
            if (!(sender is Button button) || !playButtonRowContext.TryGetValue(button, out ListViewItem item))
            {
                return;
            }

            ApplyManagedPlayButtonChrome(button, item);
        }

        private void ApplyManagedPlayButtonChrome(Button button, ListViewItem item)
        {
            bool isRowHighlightActive = item.IsSelected || ReferenceEquals(item, hoveredRowHighlightItem);
            bool isDirectHover = button.IsMouseOver;
            PlaylistManagedRowChrome.ApplyListRowPlayButtonChrome(
                button,
                item,
                isRowHighlightActive,
                isDirectHover,
                TryFindResource);
        }

        private void OnSortHeaderIsMouseOverChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.ApplicationIdle);
        }

        private const double CollapsedColumnWidthThreshold = PlaylistColumnLayoutPersistence.CollapsedColumnWidthThreshold;

        /// <summary>
        /// When a toggleable column is resized to zero width, hide it via the same settings as the header menu.
        /// </summary>
        private void TryHideColumnsCollapsedToZeroWidth()
        {
            if (!(Playlist.StaticSettings is PlaylistSettings settings))
            {
                return;
            }

            if (!TryGetGridView(out GridView gridView))
            {
                return;
            }

            bool changed = false;
            foreach (GridViewColumn column in gridView.Columns.ToList())
            {
                double width = column.Width;
                if (double.IsNaN(width) || width > CollapsedColumnWidthThreshold)
                {
                    continue;
                }

                RestoreColumnWidthIfCollapsed(column, settings);

                if (PlaylistColumnVisibilitySettings.TrySetVisibility(settings, GetColumnKey(column), visible: false))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            ApplyColumnVisibility();
            UpdateLastPlayedTimerState();
            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
        }

        private void OnPlaylistListViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            PlaylistSettings settings = Playlist.StaticSettings as PlaylistSettings;
            bool hasPersistedLayouts = settings?.ColumnLayouts != null && settings.ColumnLayouts.Count > 0;
            if (hasPersistedLayouts && TryGetGridView(out GridView gridView))
            {
                RestorePersistedColumnWidthsFromSettings(settings, gridView);
                EnforceMinimumIconColumnWidth();
                EnforceMinimumHowLongToBeatColumnWidth();
            }

            TryApplyDynamicColumnWidthsIfNeeded(
                fillWhenNoPersistedLayouts: false,
                preferredWidths: hasPersistedLayouts
                    ? GetPreferredWidthsFromSettings(settings)
                    : GetPreferredWidthsFromColumns());
            RefreshSortHeaderVisualState();
        }

        private void OnPlaylistListViewColumnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            TryHideColumnsCollapsedToZeroWidth();
            EnforceMinimumIconColumnWidth();
            EnforceMinimumHowLongToBeatColumnWidth();
            TryApplyDynamicColumnWidthsIfNeeded(
                fillWhenNoPersistedLayouts: false,
                preferredWidths: GetPreferredWidthsFromColumns());
            RefreshSortHeaderVisualState();
            RequestHeaderBodyOffsetSync();
            PersistLayoutState(persistColumnWidths: true);
        }

        private void OnPlaylistListViewColumnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            EnforceMinimumIconColumnWidth();
            RefreshSortHeaderVisualState();
        }

        /// <summary>
        /// Redistributes column widths to fill or fit the list. On first launch without saved
        /// layouts, distributes from column minimums so Name/HLTB expand to use remaining width.
        /// With saved layouts, runs only when configured widths would overflow horizontally.
        /// </summary>
        private void TryApplyDynamicColumnWidthsIfNeeded(
            bool fillWhenNoPersistedLayouts,
            IReadOnlyDictionary<string, double> preferredWidths = null)
        {
            double availableWidth = GetAvailableListWidth();
            if (availableWidth <= 0)
            {
                return;
            }

            if (!fillWhenNoPersistedLayouts && !WouldColumnWidthsOverflow(availableWidth))
            {
                return;
            }

            ApplyDynamicColumnWidths(preferredWidths);
        }

        /// <summary>
        /// Sizes visible columns to fill the list width: narrow columns stay near their preferred
        /// width with a small bonus, and Name/HLTB absorb the remaining slack.
        /// </summary>
        private void ApplyDynamicColumnWidths(IReadOnlyDictionary<string, double> preferredWidths = null)
        {
            if (playlistListView == null || isRestoringLayoutState)
            {
                return;
            }

            if (!TryGetGridView(out GridView gridView))
            {
                return;
            }

            List<string> visibleColumnKeys = gridView.Columns
                .Select(GetColumnKey)
                .Where(key => !string.IsNullOrEmpty(key))
                .ToList();
            if (visibleColumnKeys.Count == 0)
            {
                return;
            }

            double availableWidth = GetAvailableListWidth();
            if (availableWidth <= 0)
            {
                return;
            }

            IReadOnlyDictionary<string, double> preferred = preferredWidths ?? GetPreferredWidthsFromColumns();
            IReadOnlyDictionary<string, double> widths = PlaylistColumnWidthLayout.Distribute(
                availableWidth,
                visibleColumnKeys,
                preferred);

            foreach (KeyValuePair<string, double> entry in widths)
            {
                GridViewColumn column = GetColumnByKey(entry.Key);
                if (column != null)
                {
                    column.Width = entry.Value;
                }
            }

            EnforceMinimumIconColumnWidth();
        }

        private bool WouldColumnWidthsOverflow(double? availableWidth = null)
        {
            if (!TryGetGridView(out GridView gridView))
            {
                return false;
            }

            double width = availableWidth ?? GetAvailableListWidth();
            if (width <= 0)
            {
                return false;
            }

            return GetTotalVisibleColumnWidth(gridView) > width + 0.5;
        }

        private static double GetTotalVisibleColumnWidth(GridView gridView)
        {
            double total = 0;
            foreach (GridViewColumn column in gridView.Columns)
            {
                double width = column.Width;
                if (double.IsNaN(width) || width <= CollapsedColumnWidthThreshold)
                {
                    continue;
                }

                total += width;
            }

            return total;
        }

        private void RestorePersistedColumnWidthsFromSettings(PlaylistSettings settings, GridView gridView)
        {
            if (settings?.ColumnLayouts == null || settings.ColumnLayouts.Count == 0)
            {
                return;
            }

            foreach (PlaylistColumnLayoutState layout in settings.ColumnLayouts)
            {
                if (layout == null || string.IsNullOrWhiteSpace(layout.Key))
                {
                    continue;
                }

                GridViewColumn column = GetColumnByKey(layout.Key);
                if (column == null || !gridView.Columns.Contains(column))
                {
                    continue;
                }

                if (double.IsNaN(layout.Width) || layout.Width <= CollapsedColumnWidthThreshold)
                {
                    continue;
                }

                column.Width = layout.Key == IconColumnKey
                    ? PlaylistGridViewLayout.IconColumnWidth
                    : layout.Width;
            }
        }

        private void EnforceMinimumHowLongToBeatColumnWidth()
        {
            if (howLongToBeatGridViewColumn == null || !TryGetGridView(out GridView gridView))
            {
                return;
            }

            if (!gridView.Columns.Contains(howLongToBeatGridViewColumn))
            {
                return;
            }

            double minimumWidth = PlaylistColumnWidthLayout.GetMinimumWidth(HowLongToBeatColumnKey);
            if (howLongToBeatGridViewColumn.Width < minimumWidth)
            {
                howLongToBeatGridViewColumn.Width = minimumWidth;
            }
        }

        private double GetAvailableListWidth()
        {
            if (playlistListView == null)
            {
                return 0;
            }

            double width = playlistListView.ActualWidth;
            ScrollViewer scrollViewer = rowHighlightScrollViewer ?? PlaylistVisualTree.FindFirstVisualChild<ScrollViewer>(playlistListView);
            if (scrollViewer != null
                && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
            {
                width -= SystemParameters.VerticalScrollBarWidth;
            }

            return Math.Max(0, width);
        }

        private Dictionary<string, double> GetPreferredWidthsFromColumns()
        {
            var preferred = new Dictionary<string, double>();
            if (!TryGetGridView(out GridView gridView))
            {
                return preferred;
            }

            foreach (GridViewColumn column in gridView.Columns)
            {
                string columnKey = GetColumnKey(column);
                if (string.IsNullOrEmpty(columnKey))
                {
                    continue;
                }

                double width = column.Width;
                if (!double.IsNaN(width) && width > CollapsedColumnWidthThreshold)
                {
                    preferred[columnKey] = width;
                }
            }

            return preferred;
        }

        private static Dictionary<string, double> GetPreferredWidthsFromSettings(PlaylistSettings settings)
        {
            var preferred = new Dictionary<string, double>();
            if (settings?.ColumnLayouts == null)
            {
                return preferred;
            }

            foreach (PlaylistColumnLayoutState layout in settings.ColumnLayouts)
            {
                if (layout == null
                    || string.IsNullOrWhiteSpace(layout.Key)
                    || double.IsNaN(layout.Width)
                    || layout.Width <= CollapsedColumnWidthThreshold)
                {
                    continue;
                }

                preferred[layout.Key] = layout.Key == IconColumnKey
                    ? PlaylistGridViewLayout.IconColumnWidth
                    : layout.Width;
            }

            return preferred;
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
            SubscribeRowHighlightHooks();
            SubscribeDragReorderStatusPopup();
        }

        private void SubscribeDragReorderStatusPopup()
        {
            if (dragReorderStatusViewModel != null)
            {
                dragReorderStatusViewModel.PropertyChanged -= OnDragReorderStatusViewModelPropertyChanged;
                dragReorderStatusViewModel = null;
            }

            dragReorderStatusViewModel = DataContext as INotifyPropertyChanged;
            if (dragReorderStatusViewModel != null)
            {
                dragReorderStatusViewModel.PropertyChanged += OnDragReorderStatusViewModelPropertyChanged;
            }

            EnsureDragReorderGiveFeedbackHook();
            UpdateDragReorderStatusIndicator();
            UpdateGridViewAllowsColumnReorder();
        }

        private void EnsureDragReorderGiveFeedbackHook()
        {
            if (playlistListView == null)
            {
                return;
            }

            if (!dragReorderGiveFeedbackHooked)
            {
                playlistListView.GiveFeedback += OnPlaylistListViewGiveFeedback;
                dragReorderGiveFeedbackHooked = true;
            }

            if (!dragReorderQueryContinueDragHooked)
            {
                playlistListView.QueryContinueDrag += OnPlaylistListViewQueryContinueDrag;
                dragReorderQueryContinueDragHooked = true;
            }
        }

        private void OnPlaylistListViewQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (!dragReorderStatusIndicator.IsVisible)
            {
                return;
            }

            UpdateDragReorderStatusIndicatorPosition();
        }

        private void OnDragReorderStatusViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlaylistViewModel.DragReorderStatusText))
            {
                UpdateDragReorderStatusIndicator();
            }
            else if (e.PropertyName == nameof(PlaylistViewModel.IsPlaylistDragReorderActive))
            {
                UpdateGridViewAllowsColumnReorder();
            }
        }

        private void OnPlaylistListViewGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (!dragReorderStatusIndicator.IsVisible)
            {
                return;
            }

            UpdateDragReorderStatusIndicatorPosition();
            e.Handled = false;
        }

        private void PlaylistListView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragReorderStatusIndicator.IsVisible)
            {
                return;
            }

            UpdateDragReorderStatusIndicatorPosition();
        }

        private Point GetDragReorderStatusMousePosition()
        {
            return PlaylistCursorPosition.GetPositionRelativeTo(playlistListView);
        }

        private void UpdateDragReorderStatusIndicator()
        {
            if (!(DataContext is PlaylistViewModel viewModel)
                || string.IsNullOrWhiteSpace(viewModel.DragReorderStatusText))
            {
                dragReorderStatusIndicator.Hide();
                return;
            }

            dragReorderStatusIndicator.Show(viewModel.DragReorderStatusText, GetDragReorderStatusMousePosition());
        }

        private void UpdateDragReorderStatusIndicatorPosition(Point? positionInListView = null)
        {
            if (!dragReorderStatusIndicator.IsVisible)
            {
                return;
            }

            Point position = positionInListView ?? GetDragReorderStatusMousePosition();
            dragReorderStatusIndicator.UpdatePosition(position);
        }

        private void UpdateGridViewAllowsColumnReorder()
        {
            if (!(playlistListView.View is GridView gridView))
            {
                return;
            }

            PlaylistViewModel viewModel = DataContext as PlaylistViewModel;
            gridView.AllowsColumnReorder = viewModel == null || !viewModel.IsPlaylistDragReorderActive;
        }

        private void RequestHeaderBodyOffsetSync()
        {
            headerBodyOffsetLocked = false;
            syncedHeaderBodyOffsetPixels = int.MinValue;
            ScheduleGridViewHeaderBodyOffsetSync();
        }

        private void ScheduleGridViewHeaderBodyOffsetSync()
        {
            Dispatcher.BeginInvoke((Action)(() => SyncGridViewHeaderBodyOffset()), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Measure header vs body horizontal offset and apply as row-presenter left margin.
        /// Measured once per layout pass — remeasuring after applying margin reads ~0 and causes 0/1px oscillation.
        /// </summary>
        /// <returns>True when the row-presenter margin was changed.</returns>
        private bool SyncGridViewHeaderBodyOffset()
        {
            if (headerBodyOffsetLocked || playlistListView == null || rankGridViewColumn == null)
            {
                return false;
            }

            if (!TryMeasureHeaderBodyOffset(out double offset))
            {
                return false;
            }

            int offsetPixels = (int)Math.Round(Math.Max(0, offset), MidpointRounding.AwayFromZero);
            if (offsetPixels == syncedHeaderBodyOffsetPixels)
            {
                return false;
            }

            syncedHeaderBodyOffsetPixels = offsetPixels;
            Thickness target = new Thickness(offsetPixels, 0, 0, 0);
            if (GridViewRowPresenterMargin == target)
            {
                return false;
            }

            GridViewRowPresenterMargin = target;
            headerBodyOffsetLocked = true;
            return true;
        }

        /// <summary>
        /// How far column headers are right of body cells (positive = headers right). Measured at runtime.
        /// </summary>
        private bool TryMeasureHeaderBodyOffset(out double offset)
        {
            offset = 0;
            if (playlistListView == null || rankGridViewColumn == null)
            {
                return false;
            }

            GridViewColumnHeader rankHeader = null;
            foreach (GridViewColumnHeader header in PlaylistVisualTree.FindVisualChildren<GridViewColumnHeader>(playlistListView))
            {
                if (header.Column == rankGridViewColumn && header.Role != GridViewColumnHeaderRole.Padding)
                {
                    rankHeader = header;
                    break;
                }
            }

            if (rankHeader == null
                || playlistListView.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated
                || !(playlistListView.View is GridView gridView))
            {
                return false;
            }

            ListViewItem firstItem = PlaylistVisualTree.FindFirstVisualChild<ListViewItem>(playlistListView);
            if (firstItem == null)
            {
                return false;
            }

            int columnIndex = gridView.Columns.IndexOf(rankGridViewColumn);
            GridViewRowPresenter rowPresenter = PlaylistVisualTree.FindFirstVisualChild<GridViewRowPresenter>(firstItem);
            if (columnIndex < 0
                || rowPresenter == null
                || columnIndex >= VisualTreeHelper.GetChildrenCount(rowPresenter))
            {
                return false;
            }

            if (!(VisualTreeHelper.GetChild(rowPresenter, columnIndex) is ContentPresenter rankCell))
            {
                return false;
            }

            double headerLeft = Math.Round(rankHeader.TransformToAncestor(playlistListView).Transform(new Point(0, 0)).X);
            double cellLeft = Math.Round(rankCell.TransformToAncestor(playlistListView).Transform(new Point(0, 0)).X);
            offset = headerLeft - cellLeft;
            return true;
        }

        private void OnPlaylistViewUnloaded(object sender, RoutedEventArgs e)
        {
            DetachSortHeaderMouseHooks();
            UnsubscribeRowHighlightHooks();
            UnsubscribeGridColumnCollectionChanged();
            columnReorderDropIndicator?.Detach();
            dragReorderStatusIndicator?.Hide();
            if (playlistListView != null)
            {
                if (dragReorderGiveFeedbackHooked)
                {
                    playlistListView.GiveFeedback -= OnPlaylistListViewGiveFeedback;
                    dragReorderGiveFeedbackHooked = false;
                }

                if (dragReorderQueryContinueDragHooked)
                {
                    playlistListView.QueryContinueDrag -= OnPlaylistListViewQueryContinueDrag;
                    dragReorderQueryContinueDragHooked = false;
                }
            }
            headerBodyOffsetLocked = false;
            syncedHeaderBodyOffsetPixels = int.MinValue;
            rowHighlightRefreshPending = false;
            PersistLayoutState();
            lastPlayedRefreshTimer?.Stop();
        }

        public void ApplySettings()
        {
            PlaylistLocalizationOverride.ApplyFromSettings(Playlist.StaticSettings);
            PlaylistLocalizationOverride.MergeInto(this);
            ApplyColumnVisibility();
            (DataContext as PlaylistViewModel)?.RefreshHowLongToBeatHeaderText();
            (DataContext as PlaylistViewModel)?.RefreshCompletionStatusPresentation();
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
            bool hltbPluginEnabled = HowLongToBeatAddonNavigation.IsPluginEnabledInPlaynite(Playlist.StaticPlayniteApi);
            bool hltbIntegrationEnabled = settings?.EnableHowLongToBeatIntegration ?? true;
            bool showHowLongToBeatColumn = hltbPluginEnabled
                && hltbIntegrationEnabled
                && (settings?.ShowHowLongToBeatColumn ?? true);

            SetColumnVisible(gridView, rankGridViewColumn, settings?.ShowRankColumn ?? true);
            SetColumnVisible(gridView, playtimeGridViewColumn, settings?.ShowPlaytimeColumn ?? true);
            SetColumnVisible(gridView, completionStatusGridViewColumn, settings?.ShowCompletionStatusColumn ?? true);
            SetColumnVisible(gridView, lastPlayedGridViewColumn, settings?.ShowLastPlayedColumn ?? true);
            SetColumnVisible(gridView, lastActivityGridViewColumn, settings?.ShowLastActivityColumn ?? false);
            SetColumnVisible(gridView, howLongToBeatGridViewColumn, showHowLongToBeatColumn);

            EnforceMinimumIconColumnWidth();
            EnforceMinimumHowLongToBeatColumnWidth();

            if (settings == null)
            {
                return;
            }

            RestorePersistedColumnWidthsFromSettings(settings, gridView);
            bool hasPersistedLayouts = settings.ColumnLayouts != null && settings.ColumnLayouts.Count > 0;
            TryApplyDynamicColumnWidthsIfNeeded(
                fillWhenNoPersistedLayouts: !hasPersistedLayouts,
                preferredWidths: hasPersistedLayouts ? GetPreferredWidthsFromSettings(settings) : null);
        }

        /// <summary>
        /// Icon column width is fixed (not user-resizable meaningfully); snap to target so persisted
        /// 39/40px values from earlier layout iterations do not leave a 1px left gap.
        /// </summary>
        private void EnforceMinimumIconColumnWidth()
        {
            if (iconGridViewColumn == null)
            {
                return;
            }

            double targetWidth = PlaylistGridViewLayout.IconColumnWidth;
            double width = iconGridViewColumn.Width;
            if (!double.IsNaN(width) && Math.Abs(width - targetWidth) < 0.1)
            {
                return;
            }

            iconGridViewColumn.Width = targetWidth;
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

            gridView.Columns.Insert(ComputeSavedInsertIndex(gridView, column, Playlist.StaticSettings as PlaylistSettings), column);
            RestoreColumnWidthIfCollapsed(column, Playlist.StaticSettings as PlaylistSettings);
        }

        private static IReadOnlyList<string> GetAllColumnKeys()
        {
            return new[]
            {
                RankColumnKey,
                IconColumnKey,
                NameColumnKey,
                PlaytimeColumnKey,
                CompletionStatusColumnKey,
                LastPlayedColumnKey,
                LastActivityColumnKey,
                HowLongToBeatColumnKey,
            };
        }

        private int ComputeSavedInsertIndex(GridView gridView, GridViewColumn column, PlaylistSettings settings)
        {
            string columnKey = GetColumnKey(column);
            if (string.IsNullOrEmpty(columnKey)
                || settings?.ColumnLayouts == null
                || settings.ColumnLayouts.Count == 0)
            {
                return ComputeCanonicalInsertIndex(gridView, column);
            }

            List<PlaylistColumnLayoutState> ordered = settings.ColumnLayouts
                .Where(layout => layout != null && !string.IsNullOrWhiteSpace(layout.Key))
                .OrderBy(layout => layout.DisplayIndex)
                .ToList();

            int insertAt = 0;
            foreach (PlaylistColumnLayoutState layout in ordered)
            {
                if (layout.Key == columnKey)
                {
                    return insertAt;
                }

                GridViewColumn present = GetColumnByKey(layout.Key);
                if (present != null && gridView.Columns.Contains(present))
                {
                    insertAt++;
                }
            }

            return ComputeCanonicalInsertIndex(gridView, column);
        }

        private static Dictionary<string, PlaylistColumnLayoutState> GetColumnLayoutsByKey(PlaylistSettings settings)
        {
            if (settings?.ColumnLayouts == null)
            {
                return new Dictionary<string, PlaylistColumnLayoutState>();
            }

            return settings.ColumnLayouts
                .Where(layout => layout != null && !string.IsNullOrWhiteSpace(layout.Key))
                .GroupBy(layout => layout.Key)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private double GetPersistedWidthForColumnKey(
            string columnKey,
            GridView gridView,
            PlaylistSettings settings,
            IReadOnlyDictionary<string, PlaylistColumnLayoutState> previousByKey,
            bool persistColumnWidths)
        {
            GridViewColumn column = GetColumnByKey(columnKey);
            bool isVisible = column != null && gridView.Columns.Contains(column);
            double? visibleWidth = isVisible ? column.Width : (double?)null;
            return PlaylistColumnLayoutPersistence.ResolvePersistedWidthForColumnKey(
                columnKey,
                visibleWidth,
                isVisible,
                persistColumnWidths,
                previousByKey,
                settings?.ColumnLayouts);
        }

        private List<PlaylistColumnLayoutState> BuildColumnLayoutsForPersistence(
            GridView gridView,
            PlaylistSettings settings,
            bool persistColumnWidths)
        {
            // persistColumnWidths is true only after a user gripper drag; other callers keep saved widths.
            List<string> visibleKeysInOrder = gridView.Columns
                .Select(GetColumnKey)
                .Where(key => !string.IsNullOrEmpty(key))
                .ToList();
            var visibleSet = new HashSet<string>(visibleKeysInOrder);
            Dictionary<string, PlaylistColumnLayoutState> previousByKey = GetColumnLayoutsByKey(settings);
            IReadOnlyList<string> allColumnKeys = GetAllColumnKeys();

            List<string> hiddenKeys = allColumnKeys
                .Where(key => !visibleSet.Contains(key))
                .OrderBy(key =>
                {
                    if (previousByKey.TryGetValue(key, out PlaylistColumnLayoutState layout))
                    {
                        return layout.DisplayIndex;
                    }

                    return GetCanonicalColumnOrder(GetColumnByKey(key));
                })
                .ToList();

            List<string> fullOrder = PlaylistColumnLayoutPersistence.MergeVisibleAndHiddenColumnOrder(
                visibleKeysInOrder,
                hiddenKeys,
                previousByKey,
                allColumnKeys.Count);

            return fullOrder
                .Select((key, index) => new PlaylistColumnLayoutState
                {
                    Key = key,
                    DisplayIndex = index,
                    Width = GetPersistedWidthForColumnKey(key, gridView, settings, previousByKey, persistColumnWidths),
                })
                .ToList();
        }

        private static double? TryGetPersistedColumnWidth(PlaylistSettings settings, string columnKey)
        {
            return PlaylistColumnLayoutPersistence.TryGetPersistedColumnWidth(settings?.ColumnLayouts, columnKey);
        }

        private static double GetDefaultColumnWidth(string columnKey)
        {
            return PlaylistColumnWidthLayout.GetMinimumWidth(columnKey);
        }

        private void RestoreColumnWidthIfCollapsed(GridViewColumn column, PlaylistSettings settings)
        {
            if (column == null)
            {
                return;
            }

            double width = column.Width;
            if (!double.IsNaN(width) && width > CollapsedColumnWidthThreshold)
            {
                return;
            }

            string columnKey = GetColumnKey(column);
            if (string.IsNullOrEmpty(columnKey))
            {
                return;
            }

            double restoredWidth = TryGetPersistedColumnWidth(settings, columnKey)
                ?? GetDefaultColumnWidth(columnKey);
            if (columnKey == IconColumnKey)
            {
                restoredWidth = PlaylistGridViewLayout.IconColumnWidth;
            }

            if (double.IsNaN(restoredWidth) || restoredWidth <= CollapsedColumnWidthThreshold)
            {
                return;
            }

            column.Width = restoredWidth;
        }

        private static double GetWidthForLayoutPersistence(PlaylistSettings settings, string columnKey, double currentWidth)
        {
            return PlaylistColumnLayoutPersistence.GetWidthForLayoutPersistence(
                columnKey,
                currentWidth,
                settings?.ColumnLayouts);
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

            ContextMenu menu = PlaylistQuickAccessMenuBuilder.BuildColumnVisibilityContextMenu();
            if (menu == null)
            {
                return;
            }

            menu.PlacementTarget = header;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
            e.Handled = true;
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
        private const double RankHeaderRightInset = 2;
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

            if (sender is Grid container && BeginRankEdit(container))
            {
                e.Handled = true;
            }
        }

        private static bool IsUnderRankCell(DependencyObject source)
        {
            for (DependencyObject element = source; element != null; element = VisualTreeHelper.GetParent(element))
            {
                if (element is FrameworkElement fe && fe.Tag is string tag && tag == RankCellTag)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BeginRankEdit(Grid container)
        {
            if (container == null || container.Children.Count < 2)
            {
                return false;
            }

            if (!(container.Children[0] is TextBlock textBlock) || !(container.Children[1] is TextBox rankEditor))
            {
                return false;
            }

            // Already editing — still treat as handled so the row does not start the game.
            if (rankEditor.Visibility == Visibility.Visible)
            {
                return true;
            }

            rankEditor.Text = textBlock.Text;
            textBlock.Visibility = Visibility.Collapsed;
            rankEditor.Visibility = Visibility.Visible;
            rankEditor.Focus();
            rankEditor.SelectAll();
            return true;
        }

        private void OnRankTextBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Row listens for double-click to launch; swallow while editing rank.
            e.Handled = true;
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

        private void OnPlaylistSearchClearClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PlaylistViewModel model)
            {
                model.SearchQuery = string.Empty;
            }

            playlistSearchTextBox?.Focus();
            e.Handled = true;
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

        /// <summary>
        /// Persists sort and column order. Column widths are written only when
        /// <paramref name="persistColumnWidths"/> is true (user gripper drag).
        /// </summary>
        private void PersistLayoutState(bool persistColumnWidths = false)
        {
            if (isRestoringLayoutState)
            {
                return;
            }

            EnforceMinimumIconColumnWidth();

            if (!(DataContext is PlaylistViewModel model) || !(Playlist.StaticSettings is PlaylistSettings settings))
            {
                return;
            }

            if (!TryGetGridView(out GridView gridView))
            {
                return;
            }

            List<PlaylistColumnLayoutState> layouts = BuildColumnLayoutsForPersistence(
                gridView,
                settings,
                persistColumnWidths);

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
            EnforceMinimumIconColumnWidth();
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
            bool hasPersistedLayouts = settings.ColumnLayouts != null && settings.ColumnLayouts.Count > 0;
            try
            {
                if (hasPersistedLayouts)
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

                    RestorePersistedColumnWidthsFromSettings(settings, gridView);
                    EnforceMinimumIconColumnWidth();
                    EnforceMinimumHowLongToBeatColumnWidth();
                }
                else
                {
                    EnforceMinimumIconColumnWidth();
                    EnforceMinimumHowLongToBeatColumnWidth();
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

            IReadOnlyDictionary<string, double> preferredWidths = hasPersistedLayouts
                ? GetPreferredWidthsFromSettings(settings)
                : null;
            Dispatcher.BeginInvoke(
                (Action)(() => TryApplyDynamicColumnWidthsIfNeeded(
                    fillWhenNoPersistedLayouts: !hasPersistedLayouts,
                    preferredWidths: preferredWidths)),
                DispatcherPriority.Loaded);

            Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
            RequestHeaderBodyOffsetSync();
        }

        private bool? cachedUseLightHeaderText;
        private PlaylistThemeChrome.SortHeaderHighlightAppearance cachedSortHeaderHighlight;

        private GridViewColumnHeader FindSampleSortHeader(GridViewColumn activeColumn)
        {
            GridViewColumnHeader anyNonActive = null;
            foreach (GridViewColumnHeader header in PlaylistVisualTree.FindVisualChildren<GridViewColumnHeader>(playlistListView))
            {
                if (header == null || header.Role == GridViewColumnHeaderRole.Padding || !IsSortableColumn(header.Column))
                {
                    continue;
                }

                if (header.Column == activeColumn)
                {
                    continue;
                }

                anyNonActive = header;
                if (!header.IsMouseOver)
                {
                    return header;
                }
            }

            return anyNonActive;
        }

        private void RefreshSortHeaderVisualState()
        {
            if (!(DataContext is PlaylistViewModel model) || playlistListView == null)
            {
                return;
            }

            GridViewColumn activeColumn = GetColumnByKey(model.ActiveViewSortColumn);
            GridViewColumnHeader sampleHeader = FindSampleSortHeader(activeColumn);
            PlaylistThemeChrome.SortHeaderHighlightAppearance appearance =
                PlaylistThemeChrome.GetSortHeaderHighlightAppearance(
                    PlaylistThemeChrome.TryGetHeaderLabelColor(sampleHeader),
                    key => TryFindResource(key));
            if (cachedUseLightHeaderText != appearance.UseLightHeaderText
                || cachedSortHeaderHighlight.Background == null)
            {
                cachedUseLightHeaderText = appearance.UseLightHeaderText;
                cachedSortHeaderHighlight = appearance;
            }

            PlaylistThemeChrome.SortHeaderHighlightAppearance sortHeaderHighlight = cachedSortHeaderHighlight;
            bool foundHeader = false;
            foreach (GridViewColumnHeader header in PlaylistVisualTree.FindVisualChildren<GridViewColumnHeader>(playlistListView))
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

                if (header.Column == iconGridViewColumn)
                {
                    foundHeader = true;
                    ApplyIconHeaderVisualState(
                        header,
                        sampleHeader,
                        sortHeaderHighlight);
                    continue;
                }

                if (!IsSortableColumn(header.Column))
                {
                    continue;
                }

                foundHeader = true;
                EnsureSortHeaderMouseHook(header);

                // Ensure header content can consume full cell width for right-aligned glyphs.
                ContentPresenter presenter = PlaylistVisualTree.FindFirstVisualChild<ContentPresenter>(header);
                if (presenter != null)
                {
                    presenter.HorizontalAlignment = HorizontalAlignment.Stretch;
                    double availableWidth = PlaylistSortHeaderLayout.MeasurePresenterWidth(header, presenter, SortHeaderRightEdgeReserve);
                    if (availableWidth <= 0)
                    {
                        availableWidth = Math.Max(0, header.ActualWidth - header.Padding.Left - header.Padding.Right - SortHeaderRightEdgeReserve);
                    }

                    presenter.Width = availableWidth;
                    presenter.UpdateLayout();

                    double correctedWidth = PlaylistSortHeaderLayout.FineTunePresenterWidthForGlyph(header, presenter, availableWidth);
                    if (correctedWidth > 0 && Math.Abs(correctedWidth - availableWidth) > 0.5)
                    {
                        presenter.Width = correctedWidth;
                        availableWidth = correctedWidth;
                    }

                    if (header.Column == rankGridViewColumn)
                    {
                        UpdateRankHeaderCoupledLayout(header, presenter, availableWidth);
                    }
                }

                Border highlightBorder = PlaylistSortHeaderLayout.FindHeaderHighlightBorder(header);
                bool showSortHighlight = header.Column == activeColumn || header.IsMouseOver;
                if (showSortHighlight)
                {
                    PlaylistSortHeaderLayout.ApplyActiveSortHighlight(
                        highlightBorder,
                        sortHeaderHighlight.Background,
                        sortHeaderHighlight.Border,
                        sortHeaderHighlight.UseDarkeningOverlay);

                    if (sortHeaderHighlight.UseLightHeaderText)
                    {
                        ApplyActiveHeaderTextForeground(presenter, sortHeaderHighlight.Foreground);
                        header.BeginAnimation(Control.ForegroundProperty, null);
                        header.Foreground = sortHeaderHighlight.Foreground;
                    }
                    else
                    {
                        ClearActiveHeaderTextForeground(presenter);
                        header.BeginAnimation(Control.ForegroundProperty, null);
                        header.ClearValue(Control.ForegroundProperty);
                    }
                }
                else
                {
                    PlaylistSortHeaderLayout.ClearActiveSortHighlight(highlightBorder, sortHeaderHighlight.UseDarkeningOverlay);
                    PlaylistSortHeaderLayout.RestoreIdleHeaderBorderChrome(header, highlightBorder);
                    ClearActiveHeaderTextForeground(presenter);
                    header.BeginAnimation(Control.ForegroundProperty, null);
                    header.ClearValue(Control.ForegroundProperty);
                }
            }

            if (!foundHeader)
            {
                Dispatcher.BeginInvoke((Action)RefreshSortHeaderVisualState, DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Icon column is not sortable but uses the same header chrome as other columns.
        /// </summary>
        private void ApplyIconHeaderVisualState(
            GridViewColumnHeader header,
            GridViewColumnHeader sampleHeader,
            PlaylistThemeChrome.SortHeaderHighlightAppearance sortHeaderHighlight)
        {
            if (header == null)
            {
                return;
            }

            if (header.Tag == null && sampleHeader?.Tag != null)
            {
                header.Tag = sampleHeader.Tag;
            }

            EnsureSortHeaderMouseHook(header);

            HideColumnResizeGripper(header);

            Border highlightBorder = PlaylistSortHeaderLayout.FindHeaderHighlightBorder(header);
            if (header.IsMouseOver)
            {
                PlaylistSortHeaderLayout.ApplyActiveSortHighlight(
                    highlightBorder,
                    sortHeaderHighlight.Background,
                    sortHeaderHighlight.Border,
                    sortHeaderHighlight.UseDarkeningOverlay);
            }
            else
            {
                PlaylistSortHeaderLayout.ClearActiveSortHighlight(highlightBorder, sortHeaderHighlight.UseDarkeningOverlay);
                PlaylistSortHeaderLayout.RestoreIdleHeaderBorderChrome(header, highlightBorder);
            }
        }

        /// <summary>
        /// Icon column width is fixed; hide the template gripper so no resize affordance shows at the name boundary.
        /// </summary>
        private static void HideColumnResizeGripper(GridViewColumnHeader header)
        {
            if (header == null)
            {
                return;
            }

            header.ApplyTemplate();
            if (header.Template?.FindName("PART_HeaderGripper", header) is UIElement namedGripper)
            {
                namedGripper.Visibility = Visibility.Collapsed;
                namedGripper.IsHitTestVisible = false;
            }

            foreach (Thumb gripper in PlaylistVisualTree.FindVisualChildren<Thumb>(header))
            {
                if (gripper.Name != "PART_HeaderGripper")
                {
                    continue;
                }

                gripper.Visibility = Visibility.Collapsed;
                gripper.IsHitTestVisible = false;
                gripper.Width = 0;
                gripper.Height = 0;
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

        private static void ApplyActiveHeaderTextForeground(DependencyObject presenter, Brush foreground)
        {
            if (presenter == null || foreground == null)
            {
                return;
            }

            foreach (TextBlock textBlock in PlaylistVisualTree.FindVisualChildren<TextBlock>(presenter))
            {
                textBlock.BeginAnimation(TextBlock.ForegroundProperty, null);
                textBlock.Foreground = foreground;
            }
        }

        private static void ClearActiveHeaderTextForeground(DependencyObject presenter)
        {
            if (presenter == null)
            {
                return;
            }

            foreach (TextBlock textBlock in PlaylistVisualTree.FindVisualChildren<TextBlock>(presenter))
            {
                textBlock.BeginAnimation(TextBlock.ForegroundProperty, null);
                textBlock.ClearValue(TextBlock.ForegroundProperty);
            }
        }

        private static T FindChildByTag<T>(DependencyObject parent, string tag) where T : FrameworkElement
        {
            foreach (T child in PlaylistVisualTree.FindVisualChildren<T>(parent))
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
