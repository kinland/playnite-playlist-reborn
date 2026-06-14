using Playnite.SDK;
using Playnite.SDK.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Pill header for a completion status entry in the Set Completion Status submenu.
    /// Syncable HLTB tiers use accent chrome; other statuses use muted empty-track styling.
    /// </summary>
    public class CompletionStatusMenuHeader : Border
    {
        public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
            nameof(Status),
            typeof(CompletionStatus),
            typeof(CompletionStatusMenuHeader),
            new PropertyMetadata(null, OnPresentationPropertyChanged));

        public static readonly DependencyProperty IsSyncableTierProperty = DependencyProperty.Register(
            nameof(IsSyncableTier),
            typeof(bool),
            typeof(CompletionStatusMenuHeader),
            new PropertyMetadata(false, OnPresentationPropertyChanged));

        private readonly TextBlock label;

        public CompletionStatusMenuHeader()
        {
            CornerRadius = new CornerRadius(PlaylistGridViewLayout.CompletionStatusMenuHeaderCornerRadius);
            Padding = PlaylistGridViewLayout.CompletionStatusMenuHeaderPadding;
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

            Loaded += (_, __) => RefreshPresentation();
        }

        public CompletionStatus Status
        {
            get => (CompletionStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public bool IsSyncableTier
        {
            get => (bool)GetValue(IsSyncableTierProperty);
            set => SetValue(IsSyncableTierProperty, value);
        }

        private static void OnPresentationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CompletionStatusMenuHeader)?.RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            label.Text = CompletionStatusLocalization.LocalizeDisplayName(Status?.Name);

            PlaylistThemeChrome.CompletionStatusChipAppearance appearance =
                PlaylistThemeChrome.GetCompletionStatusChipAppearance(
                    IsSyncableTier,
                    row: null,
                    isRowHoverActive: false,
                    TryGetThemeResource);

            Background = appearance.Background;
            BorderBrush = appearance.Border;
            if (appearance.Foreground != null)
            {
                label.Foreground = appearance.Foreground;
            }
            else
            {
                label.ClearValue(TextBlock.ForegroundProperty);
                object brush = TryFindResource("TextBrush") ?? ResourceProvider.GetResource("TextBrush");
                if (brush is Brush resourceForeground)
                {
                    label.Foreground = resourceForeground;
                }
            }

            label.Opacity = appearance.ForegroundOpacity;
        }

        private object TryGetThemeResource(string key)
        {
            return TryFindResource(key) ?? ResourceProvider.GetResource(key);
        }
    }
}
