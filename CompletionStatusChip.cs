using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Pill label for a game's completion status in the playlist grid. All statuses use this layout;
    /// HLTB-syncable tiers get accent chrome, others use muted empty-track styling.
    /// </summary>
    public class CompletionStatusChip : Border
    {
        private readonly TextBlock label;
        private bool rowHoverActive;

        public CompletionStatusChip()
        {
            CornerRadius = new CornerRadius(PlaylistGridViewLayout.CompletionStatusChipCornerRadius);
            Padding = PlaylistGridViewLayout.CompletionStatusChipPadding;
            BorderThickness = new Thickness(1);
            SnapsToDevicePixels = true;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;

            label = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Child = label;

            Loaded += OnLoaded;
            DataContextChanged += (_, __) => RefreshContent();
        }

        internal void SyncRowHighlightFromListViewItem(bool isHoverActive)
        {
            rowHoverActive = isHoverActive;
            ApplyAppearance();
        }

        internal void RefreshAppearance()
        {
            RefreshContent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SynchronizeTextMetricsForListRow();
            RefreshContent();
        }

        private void RefreshContent()
        {
            label.Text = ResolveDisplayText();
            ApplyAppearance();
        }

        private string ResolveDisplayText()
        {
            if (DataContext is Game game)
            {
                return CompletionStatusLocalization.LocalizeDisplayName(game.CompletionStatus?.Name);
            }

            if (DataContext is CompletionStatus status)
            {
                return CompletionStatusLocalization.LocalizeDisplayName(status?.Name);
            }

            return string.Empty;
        }

        private void ApplyAppearance()
        {
            bool isSyncableTier = IsSyncableTier();
            PlaylistThemeChrome.CompletionStatusChipAppearance appearance =
                PlaylistThemeChrome.GetCompletionStatusChipAppearance(
                    isSyncableTier,
                    FindListViewItemAncestor(this),
                    rowHoverActive,
                    TryGetThemeResource);

            Background = appearance.Background;
            BorderBrush = appearance.Border;
            if (appearance.Foreground != null)
            {
                label.Foreground = appearance.Foreground;
            }
            else
            {
                ApplyInheritedForeground(label);
            }

            label.Opacity = appearance.ForegroundOpacity;
        }

        private bool IsSyncableTier()
        {
            Guid statusId = Guid.Empty;
            if (DataContext is Game game)
            {
                statusId = game.CompletionStatusId;
            }
            else if (DataContext is CompletionStatus status && status != null)
            {
                statusId = status.Id;
            }

            if (statusId == Guid.Empty)
            {
                return false;
            }

            return CompletionStatusSyncTier.IsSyncableTier(
                statusId,
                Playlist.StaticPlayniteApi?.Database?.CompletionStatuses,
                Playlist.StaticSettings as PlaylistSettings);
        }

        private void ApplyInheritedForeground(TextBlock textBlock)
        {
            if (textBlock == null)
            {
                return;
            }

            ListViewItem row = FindListViewItemAncestor(this);
            if (row?.Foreground is Brush rowForeground)
            {
                textBlock.Foreground = rowForeground;
                return;
            }

            object brush = TryFindResource("TextBrush") ?? ResourceProvider.GetResource("TextBrush");
            if (brush is Brush resourceForeground)
            {
                textBlock.Foreground = resourceForeground;
            }
        }

        private object TryGetThemeResource(string key)
        {
            return TryFindResource(key) ?? ResourceProvider.GetResource(key);
        }

        private void SynchronizeTextMetricsForListRow()
        {
            ListViewItem row = FindListViewItemAncestor(this);
            if (row == null)
            {
                return;
            }

            double size = GetEffectiveTextFontSize(row);
            if (size > 0)
            {
                TextElement.SetFontSize(label, size);
            }

            TextElement.SetFontFamily(label, TextElement.GetFontFamily(row));
            TextElement.SetFontStyle(label, TextElement.GetFontStyle(row));
            TextElement.SetFontWeight(label, TextElement.GetFontWeight(row));
            TextElement.SetFontStretch(label, TextElement.GetFontStretch(row));
        }

        private static double GetEffectiveTextFontSize(ListViewItem row)
        {
            double size = TextElement.GetFontSize(row);
            if (size > 0 && !double.IsNaN(size))
            {
                return size;
            }

            return 0;
        }

        private static ListViewItem FindListViewItemAncestor(DependencyObject element)
        {
            for (DependencyObject parent = element; parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ListViewItem listViewItem)
                {
                    return listViewItem;
                }
            }

            return null;
        }
    }
}
