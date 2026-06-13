using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Renders HLTB-style cumulative segments (main → main+extra → completionist / multi variants),
    /// matching HowLongToBeat plugin bar geometry: each milestone gets a horizontal slice from the previous end to its scaled position.
    /// </summary>
    public class HowLongToBeatCachedProgressBar : Grid
    {
        // Keep in sync with PlaylistGridViewLayout.HltbSegmentStripHeight (UiTests compile this file alone).
        private const double SegmentStripHeight = 22;

        private static readonly SolidColorBrush EmptyTrackFillNormal = CreateFrozenBrush(Color.FromArgb(70, 10, 20, 30));
        private static readonly SolidColorBrush EmptyTrackFillOnDarkRow = CreateFrozenBrush(Color.FromArgb(200, 225, 228, 232));
        private static readonly SolidColorBrush EmptyTrackBorderNormal = CreateFrozenBrush(Color.FromArgb(140, 80, 90, 100));
        private static readonly SolidColorBrush EmptyTrackBorderOnDarkRow = CreateFrozenBrush(Color.FromArgb(220, 120, 128, 138));

        private readonly Grid barOverlay;
        private readonly Border segmentStripHost;
        private readonly StackPanel segmentStrip;
        private readonly StackPanel topLabelStrip;
        private readonly StackPanel bottomLabelStrip;
        private readonly Border playtimeMarker;
        private readonly TextBlock emptyLabel;
        private bool rowHoverActive;

        public HowLongToBeatCachedProgressBar()
        {
            MinHeight = SegmentStripHeight;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Center;
            ClipToBounds = true;

            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            topLabelStrip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed,
            };

            segmentStrip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Height = SegmentStripHeight,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };

            segmentStripHost = new Border
            {
                Height = SegmentStripHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Background = EmptyTrackFillNormal,
                BorderBrush = EmptyTrackBorderNormal,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Child = segmentStrip,
            };

            playtimeMarker = new Border
            {
                Width = 12,
                Height = 18,
                Background = new SolidColorBrush(Color.FromArgb(240, 240, 248, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(170, 8, 12, 16)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                SnapsToDevicePixels = true,
            };

            barOverlay = new Grid
            {
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center,
            };
            barOverlay.Children.Add(segmentStripHost);
            barOverlay.Children.Add(playtimeMarker);
            Panel.SetZIndex(playtimeMarker, 2);
            Grid.SetRow(barOverlay, 1);

            bottomLabelStrip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
            };
            Grid.SetRow(bottomLabelStrip, 2);

            emptyLabel = new TextBlock
            {
                Text = "--",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                Visibility = Visibility.Collapsed,
            };
            Grid.SetRowSpan(emptyLabel, 3);

            Children.Add(topLabelStrip);
            Children.Add(barOverlay);
            Children.Add(bottomLabelStrip);
            Children.Add(emptyLabel);
            Panel.SetZIndex(emptyLabel, 3);
            Loaded += OnLoadedRefresh;
            SizeChanged += (_, __) => Refresh();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == DataContextProperty)
            {
                Refresh();
            }
        }

        private void OnLoadedRefresh(object sender, RoutedEventArgs e)
        {
            ApplyResourceForeground(emptyLabel);
            Refresh();
        }

        private void Refresh()
        {
            SynchronizeTextMetricsForListRow();

            if (!(DataContext is Game game))
            {
                SetUnknown();
                return;
            }

            if (!(Playlist.StaticSettings?.EnableHowLongToBeatIntegration ?? true))
            {
                Visibility = Visibility.Collapsed;
                SetUnknown();
                return;
            }

            HltbRenderSettings settings = HowLongToBeatCache.GetRenderSettings(Playlist.StaticPlayniteApi);
            if (!settings.EnableIntegrationViewItem || !settings.EnableIntegrationProgressBar)
            {
                Visibility = Visibility.Collapsed;
                SetUnknown();
                return;
            }

            Visibility = Visibility.Visible;
            if (!HowLongToBeatCache.TryGetCachedTimes(Playlist.StaticPlayniteApi, game, out HltbCachedTimes times))
            {
                SetUnknown();
                return;
            }

            List<Segment> segments = BuildSegments(times, settings);
            if (segments.Count == 0)
            {
                SetUnknown();
                return;
            }

            long maxHltb = segments.Max(s => s.ValueSeconds);
            if (maxHltb <= 0)
            {
                SetUnknown();
                return;
            }

            long playedSeconds = (long)game.Playtime;
            long scaleMax = ComputeScaleMax(maxHltb, playedSeconds);
            RenderSegments(segments, scaleMax, settings);
            RenderPlaytimeMarker(playedSeconds, scaleMax, settings);
            if (settings.ProgressBarShowToolTip && !string.IsNullOrEmpty(times.Url))
            {
                ToolTip = times.Url;
            }
            else
            {
                ToolTip = null;
            }
        }

        private void SetUnknown()
        {
            segmentStrip.Children.Clear();
            topLabelStrip.Children.Clear();
            bottomLabelStrip.Children.Clear();
            topLabelStrip.Visibility = Visibility.Collapsed;
            bottomLabelStrip.Visibility = Visibility.Collapsed;
            barOverlay.Visibility = Visibility.Visible;
            playtimeMarker.Visibility = Visibility.Collapsed;
            emptyLabel.Visibility = Visibility.Visible;
            ApplyEmptyStateVisuals();
            ToolTip = null;
        }

        private void ResetSegmentStripHostForData()
        {
            segmentStripHost.Background = EmptyTrackFillNormal;
            segmentStripHost.BorderBrush = EmptyTrackBorderNormal;
        }

        private void ApplyEmptyStateVisuals()
        {
            bool onDarkRow = IsEmptyStateOnDarkRowHighlight();
            if (onDarkRow)
            {
                segmentStripHost.Background = EmptyTrackFillOnDarkRow;
                segmentStripHost.BorderBrush = EmptyTrackBorderOnDarkRow;
            }
            else
            {
                segmentStripHost.Background = EmptyTrackFillNormal;
                segmentStripHost.BorderBrush = EmptyTrackBorderNormal;
            }

            ApplyResourceForeground(emptyLabel);
        }

        /// <summary>
        /// True when the row shows a dark mouseover fill (HoverBrush), not a light selection fill.
        /// Mirrors <see cref="PlaylistSortHeaderLayout.ListRowEmbeddedChromeStyle.LightPanelDarkGlyph"/> without
        /// referencing internal layout types (UiTests compile this file standalone).
        /// </summary>
        private bool IsEmptyStateOnDarkRowHighlight()
        {
            ListViewItem row = FindListViewItemAncestor(this);
            if (row == null || row.IsSelected || !rowHoverActive)
            {
                return false;
            }

            object hoverObject = TryFindResource("HoverBrush") ?? ResourceProvider.GetResource("HoverBrush");
            object glyphObject = TryFindResource("GlyphBrush") ?? ResourceProvider.GetResource("GlyphBrush");
            if (!(hoverObject is SolidColorBrush hoverBrush)
                || !(glyphObject is SolidColorBrush glyphBrush))
            {
                return false;
            }

            Color hoverColor = hoverBrush.Color;
            Color glyphColor = glyphBrush.Color;
            if (hoverColor.A < 16 || glyphColor.A < 16)
            {
                return false;
            }

            double hoverLuminance = (0.299 * hoverColor.R) + (0.587 * hoverColor.G) + (0.114 * hoverColor.B);
            double glyphLuminance = (0.299 * glyphColor.R) + (0.587 * glyphColor.G) + (0.114 * glyphColor.B);
            int hoverChroma = Math.Max(hoverColor.R, Math.Max(hoverColor.G, hoverColor.B))
                - Math.Min(hoverColor.R, Math.Min(hoverColor.G, hoverColor.B));
            int glyphChroma = Math.Max(glyphColor.R, Math.Max(glyphColor.G, glyphColor.B))
                - Math.Min(glyphColor.R, Math.Min(glyphColor.G, glyphColor.B));

            return hoverLuminance < 128
                && glyphLuminance >= 128
                && hoverChroma < 45
                && glyphChroma < 45;
        }

        /// <summary>
        /// Same cap as HLTB PluginProgressBar: extend scale for playtime, cap at maxHltb + 10%.
        /// </summary>
        private static long ComputeScaleMax(long maxHltbSeconds, long playtimeSeconds)
        {
            long maxValue = maxHltbSeconds;
            if (playtimeSeconds > maxValue)
            {
                maxValue = playtimeSeconds;
            }

            long tenPct = (long)Math.Ceiling(10.0 * maxHltbSeconds / 100.0);
            if (maxValue > maxHltbSeconds + tenPct)
            {
                maxValue = maxHltbSeconds + tenPct;
            }

            return Math.Max(maxValue, 1);
        }

        private void RenderSegments(IReadOnlyList<Segment> segments, long scaleMax, HltbRenderSettings settings)
        {
            emptyLabel.Visibility = Visibility.Collapsed;
            barOverlay.Visibility = Visibility.Visible;
            ResetSegmentStripHostForData();
            segmentStrip.Children.Clear();
            topLabelStrip.Children.Clear();
            bottomLabelStrip.Children.Clear();

            double width = Math.Max(barOverlay.ActualWidth, ActualWidth) - 4;
            if (width <= 0)
            {
                return;
            }

            bool showLabels = settings.ProgressBarShowTime;
            bool showInside = showLabels && settings.ProgressBarShowTimeInterior;
            bool showAbove = showLabels && settings.ProgressBarShowTimeAbove;
            bool showBelow = showLabels && settings.ProgressBarShowTimeBelow;
            topLabelStrip.Visibility = showAbove ? Visibility.Visible : Visibility.Collapsed;
            bottomLabelStrip.Visibility = showBelow ? Visibility.Visible : Visibility.Collapsed;

            double scale = scaleMax;
            var cumulativeEnds = new double[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                double end = width * Math.Min(1.0, segments[i].ValueSeconds / scale);
                if (i > 0 && end < cumulativeEnds[i - 1])
                {
                    end = cumulativeEnds[i - 1];
                }

                cumulativeEnds[i] = end;
            }

            double prev = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                double slice = cumulativeEnds[i] - prev;
                if (slice < 0.25)
                {
                    prev = cumulativeEnds[i];
                    continue;
                }

                string fullLabel = HltbPlaytimeFormat.FormatSeconds(
                    segments[i].ValueSeconds,
                    settings.IntegrationViewItemOnlyHour,
                    this);
                string displayLabel = showLabels ? FitTimeLabel(fullLabel, slice) : string.Empty;
                Color textColor = GetReadableTextColor(segments[i].Color);
                Brush fillBrush = CloneBrush(segments[i].FillBrush, segments[i].Color);

                var text = new TextBlock
                {
                    Text = showInside ? displayLabel : string.Empty,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
                    Visibility = showInside ? Visibility.Visible : Visibility.Collapsed,
                    Foreground = new SolidColorBrush(textColor),
                };

                var border = new Border
                {
                    Width = slice,
                    MinWidth = 1,
                    Height = 20,
                    Background = fillBrush,
                    BorderBrush = new SolidColorBrush(GetSegmentOutlineColor(segments[i].Color)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = GetSegmentCornerRadius(i, segments.Count),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = text,
                };

                if (settings.ProgressBarShowToolTip && !string.IsNullOrEmpty(fullLabel))
                {
                    border.ToolTip = fullLabel;
                }

                segmentStrip.Children.Add(border);
                if (showAbove)
                {
                    topLabelStrip.Children.Add(CreateExternalLabelSlice(slice, displayLabel, textColor));
                }

                if (showBelow)
                {
                    bottomLabelStrip.Children.Add(CreateExternalLabelSlice(slice, displayLabel, textColor));
                }
                prev = cumulativeEnds[i];
            }

            if (segmentStrip.Children.Count == 0)
            {
                SetUnknown();
            }
        }

        private void RenderPlaytimeMarker(long playtimeSeconds, long scaleMax, HltbRenderSettings settings)
        {
            if (scaleMax <= 0 || playtimeSeconds <= 0)
            {
                playtimeMarker.Visibility = Visibility.Collapsed;
                return;
            }

            double width = Math.Max(barOverlay.ActualWidth, ActualWidth) - 4;
            if (width <= 0)
            {
                playtimeMarker.Visibility = Visibility.Collapsed;
                return;
            }

            double x = width * Math.Min(1.0, playtimeSeconds / (double)scaleMax);
            double markerWidth = playtimeMarker.Width;
            double left = Math.Max(0, Math.Min(width - markerWidth, x - (markerWidth / 2.0)));
            playtimeMarker.Margin = new Thickness(left, 0, 0, 0);
            playtimeMarker.Visibility = Visibility.Visible;

            if (settings.ThumbPlaytimeBrush != null)
            {
                playtimeMarker.Background = CloneBrush(settings.ThumbPlaytimeBrush, Color.FromRgb(240, 248, 255));
            }
            else if (settings.ThumbPlaytimeColor.HasValue)
            {
                playtimeMarker.Background = new SolidColorBrush(settings.ThumbPlaytimeColor.Value);
            }
            else
            {
                object brush = TryFindResource("NormalBrush") ?? ResourceProvider.GetResource("NormalBrush");
                playtimeMarker.Background = brush as Brush ?? new SolidColorBrush(Color.FromRgb(240, 248, 255));
            }
        }

        /// <summary>
        /// Use <c>TextBrush</c> (or inherited foreground) for the empty state only. Do not apply
        /// <c>BaseTextBlockStyle</c> (or other full
        /// text styles) to list-cell content: those styles target different visual contexts and can produce wrong
        /// font metrics or interfere with GridView row rendering.
        /// </summary>
        private void ApplyResourceForeground(TextBlock tb)
        {
            if (tb == null)
            {
                return;
            }

            ListViewItem row = FindListViewItemAncestor(this);
            if (row?.Foreground is Brush rowForeground)
            {
                tb.Foreground = rowForeground;
                return;
            }

            object brush = TryFindResource("TextBrush") ?? ResourceProvider.GetResource("TextBrush");
            if (brush is Brush b)
            {
                tb.Foreground = b;
            }
        }

        internal void SyncRowForegroundFromListViewItem(bool isHoverActive)
        {
            rowHoverActive = isHoverActive;
            ApplyResourceForeground(emptyLabel);
            if (emptyLabel.Visibility == Visibility.Visible)
            {
                ApplyEmptyStateVisuals();
            }
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// GridView cell content does not always inherit the same <see cref="TextElement"/> metrics as
        /// <c>DisplayMemberBinding</c> text; copy the list row’s effective typeface so the HLTB column matches Name / Time.
        /// </summary>
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
                TextElement.SetFontSize(this, size);
            }

            TextElement.SetFontFamily(this, TextElement.GetFontFamily(row));
            TextElement.SetFontStyle(this, TextElement.GetFontStyle(row));
            TextElement.SetFontWeight(this, TextElement.GetFontWeight(row));
            TextElement.SetFontStretch(this, TextElement.GetFontStretch(row));
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

        private static double GetEffectiveTextFontSize(FrameworkElement from)
        {
            double s = TextElement.GetFontSize(from);
            if (!double.IsNaN(s) && s > 0)
            {
                return s;
            }

            for (var p = (DependencyObject)from; p != null; p = VisualTreeHelper.GetParent(p))
            {
                if (p is FrameworkElement fe)
                {
                    s = TextElement.GetFontSize(fe);
                    if (!double.IsNaN(s) && s > 0)
                    {
                        return s;
                    }
                }
            }

            return SystemFonts.MessageFontSize;
        }

        private static Color GetReadableTextColor(Color background)
        {
            // Relative luminance approximation for quick contrast choice.
            double luminance = (0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B);
            return luminance >= 150 ? Color.FromRgb(20, 20, 20) : Color.FromRgb(245, 245, 245);
        }

        private static Color GetSegmentOutlineColor(Color fill)
        {
            byte r = (byte)Math.Max(0, fill.R - 30);
            byte g = (byte)Math.Max(0, fill.G - 30);
            byte b = (byte)Math.Max(0, fill.B - 30);
            return Color.FromArgb(210, r, g, b);
        }

        private static CornerRadius GetSegmentCornerRadius(int index, int count)
        {
            bool first = index == 0;
            bool last = index == count - 1;
            return new CornerRadius(first ? 2 : 0, last ? 2 : 0, last ? 2 : 0, first ? 2 : 0);
        }

        private static Border CreateExternalLabelSlice(double width, string text, Color color)
        {
            return new Border
            {
                Width = width,
                MinWidth = 1,
                Height = 12,
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = text,
                    Margin = new Thickness(2, 0, 2, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(color),
                },
            };
        }

        private static Brush CloneBrush(Brush source, Color fallback)
        {
            if (source == null)
            {
                return new SolidColorBrush(fallback);
            }

            Brush clone = source.Clone();
            if (clone.CanFreeze)
            {
                clone.Freeze();
            }

            return clone;
        }

        /// <summary>Same heuristic as HowLongToBeat <c>PluginProgressBar.FitTimeLabel</c>.</summary>
        private static string FitTimeLabel(string label, double availableWidthPx)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            if (availableWidthPx < 32)
            {
                return string.Empty;
            }

            if (availableWidthPx < 55)
            {
                string[] parts = label.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? parts[0] : label;
            }

            return label;
        }

        private static List<Segment> BuildSegments(HltbCachedTimes times, HltbRenderSettings settings)
        {
            var segments = new List<Segment>();
            // HowLongToBeat.Models.Enumerations.GameType: Game = 0, Multi = 1, Compil = 2
            bool isMulti = times.GameType == 1;

            if (isMulti)
            {
                AddSegmentIfVisible(segments, ChooseValue(times.Solo, settings), settings.ShowSoloTime, settings.FirstMultiColor, settings.FirstMultiBrush);
                AddSegmentIfVisible(segments, ChooseValue(times.CoOp, settings), settings.ShowCoOpTime, settings.SecondMultiColor, settings.SecondMultiBrush);
                AddSegmentIfVisible(segments, ChooseValue(times.Vs, settings), settings.ShowVsTime, settings.ThirdMultiColor, settings.ThirdMultiBrush);
            }
            else
            {
                AddSegmentIfVisible(segments, ChooseValue(times.MainStory, settings), settings.ShowMainTime, settings.FirstColor, settings.FirstBrush);
                AddSegmentIfVisible(segments, ChooseValue(times.MainExtra, settings), settings.ShowExtraTime, settings.SecondColor, settings.SecondBrush);
                AddSegmentIfVisible(segments, ChooseValue(times.Completionist, settings), settings.ShowCompletionistTime, settings.ThirdColor, settings.ThirdBrush);
            }

            return segments;
        }

        private static void AddSegmentIfVisible(List<Segment> segments, long value, bool isVisible, Color color, Brush fillBrush)
        {
            if (!isVisible || value <= 0)
            {
                return;
            }

            segments.Add(new Segment
            {
                ValueSeconds = value,
                Color = color,
                FillBrush = fillBrush ?? new SolidColorBrush(color),
            });
        }

        private static long ChooseValue(HltbTimeVariants v, HltbRenderSettings s)
        {
            if (v == null)
            {
                return 0;
            }

            if (s.UseMedian && v.Median > 0)
            {
                return v.Median;
            }

            if (s.UseAverage && v.Average > 0)
            {
                return v.Average;
            }

            if (s.UseRushed && v.Rushed > 0)
            {
                return v.Rushed;
            }

            if (s.UseLeisure && v.Leisure > 0)
            {
                return v.Leisure;
            }

            if (s.UseClassic && v.Classic > 0)
            {
                return v.Classic;
            }

            if (v.Classic > 0)
            {
                return v.Classic;
            }

            if (v.Median > 0)
            {
                return v.Median;
            }

            if (v.Average > 0)
            {
                return v.Average;
            }

            if (v.Rushed > 0)
            {
                return v.Rushed;
            }

            if (v.Leisure > 0)
            {
                return v.Leisure;
            }

            return 0;
        }

        private sealed class Segment
        {
            public long ValueSeconds { get; set; }
            public Color Color { get; set; }
            public Brush FillBrush { get; set; }
        }
    }
}
