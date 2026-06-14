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
        private const double PlaytimeMarkerWidth = 12;
        private const double PlaytimeMarkerVerticalOverhang = 4;
        private static readonly double PlaytimeMarkerHeight = SegmentStripHeight + (PlaytimeMarkerVerticalOverhang * 2);

        private static readonly PlaylistThemeChrome.HltbEmptyTrackAppearance DefaultEmptyTrackAppearance =
            PlaylistThemeChrome.GetHltbEmptyTrackAppearance(row: null, isRowHoverActive: false, tryFindResource: null);

        private readonly Grid barOverlay;
        private readonly Border segmentStripHost;
        private readonly StackPanel segmentStrip;
        private readonly Canvas interiorLabelStrip;
        private readonly StackPanel topLabelStrip;
        private readonly StackPanel bottomLabelStrip;
        private readonly Border playtimeMarker;
        private readonly TextBlock emptyLabel;
        private bool rowHoverActive;

        public HowLongToBeatCachedProgressBar()
        {
            MinHeight = PlaytimeMarkerHeight;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Center;
            ClipToBounds = false;

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
                Background = DefaultEmptyTrackAppearance.Fill,
                BorderBrush = DefaultEmptyTrackAppearance.Border,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Child = segmentStrip,
            };

            interiorLabelStrip = new Canvas
            {
                Height = SegmentStripHeight,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };

            playtimeMarker = new Border
            {
                Width = PlaytimeMarkerWidth,
                Height = PlaytimeMarkerHeight,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                SnapsToDevicePixels = true,
            };

            barOverlay = new Grid
            {
                ClipToBounds = false,
                VerticalAlignment = VerticalAlignment.Center,
            };
            barOverlay.Children.Add(segmentStripHost);
            barOverlay.Children.Add(playtimeMarker);
            barOverlay.Children.Add(interiorLabelStrip);
            Panel.SetZIndex(segmentStripHost, 0);
            Panel.SetZIndex(playtimeMarker, 1);
            Panel.SetZIndex(interiorLabelStrip, 2);
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
                Text = PlaylistLocalization.GetString("LOCPlaylist_HLTB_EmptyTime"),
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
            interiorLabelStrip.Children.Clear();
            topLabelStrip.Children.Clear();
            bottomLabelStrip.Children.Clear();
            interiorLabelStrip.Visibility = Visibility.Collapsed;
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
            ApplyHltbEmptyTrackAppearance(PlaylistThemeChrome.GetHltbEmptyTrackAppearance(
                FindListViewItemAncestor(this),
                rowHoverActive,
                TryGetThemeResource));
        }

        private void ApplyEmptyStateVisuals()
        {
            ApplyHltbEmptyTrackAppearance(PlaylistThemeChrome.GetHltbEmptyTrackAppearance(
                FindListViewItemAncestor(this),
                rowHoverActive,
                TryGetThemeResource));
            ApplyResourceForeground(emptyLabel);
        }

        private void ApplyHltbEmptyTrackAppearance(PlaylistThemeChrome.HltbEmptyTrackAppearance appearance)
        {
            segmentStripHost.Background = appearance.Fill;
            segmentStripHost.BorderBrush = appearance.Border;
        }

        private object TryGetThemeResource(string key)
        {
            return TryFindResource(key) ?? ResourceProvider.GetResource(key);
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

        private static bool TryGetPlaytimeMarkerSpan(
            double barWidth,
            long playtimeSeconds,
            long scaleMax,
            double markerWidth,
            out double markerStart,
            out double markerEnd)
        {
            markerStart = 0;
            markerEnd = 0;
            if (barWidth <= 0 || scaleMax <= 0 || playtimeSeconds <= 0)
            {
                return false;
            }

            double x = barWidth * Math.Min(1.0, playtimeSeconds / (double)scaleMax);
            markerStart = Math.Max(0, Math.Min(barWidth - markerWidth, x - (markerWidth / 2.0)));
            markerEnd = markerStart + markerWidth;
            return true;
        }

        private void RenderSegments(IReadOnlyList<Segment> segments, long scaleMax, HltbRenderSettings settings)
        {
            emptyLabel.Visibility = Visibility.Collapsed;
            barOverlay.Visibility = Visibility.Visible;
            ResetSegmentStripHostForData();
            segmentStrip.Children.Clear();
            interiorLabelStrip.Children.Clear();
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
            interiorLabelStrip.Visibility = showInside ? Visibility.Visible : Visibility.Collapsed;

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
            var interiorPlans = new List<InteriorLabelPlan>();
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
                string interiorLabel = showLabels ? fullLabel : string.Empty;
                Color textColor = PlaylistThemeColors.GetContrastTextColorFromByteLuminance(segments[i].Color);
                Brush fillBrush = CloneBrush(segments[i].FillBrush, segments[i].Color);

                var border = new Border
                {
                    Width = slice,
                    MinWidth = 1,
                    Height = 20,
                    Background = fillBrush,
                    BorderBrush = new SolidColorBrush(PlaylistThemeColors.GetSegmentOutlineColor(segments[i].Color)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = GetSegmentCornerRadius(i, segments.Count),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                if (settings.ProgressBarShowToolTip && !string.IsNullOrEmpty(fullLabel))
                {
                    border.ToolTip = fullLabel;
                }

                segmentStrip.Children.Add(border);
                if (showInside && !string.IsNullOrEmpty(interiorLabel))
                {
                    interiorPlans.Add(new InteriorLabelPlan
                    {
                        CenterX = prev + (slice / 2.0),
                        SliceWidth = slice,
                        DisplayLabel = interiorLabel,
                        TextColor = textColor,
                    });
                }

                if (showAbove)
                {
                    topLabelStrip.Children.Add(CreateLabelSlice(slice, displayLabel, textColor, 12));
                }

                if (showBelow)
                {
                    bottomLabelStrip.Children.Add(CreateLabelSlice(slice, displayLabel, textColor, 12));
                }

                prev = cumulativeEnds[i];
            }

            if (showInside)
            {
                RenderInteriorLabels(interiorPlans, width);
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

            if (!TryGetPlaytimeMarkerSpan(width, playtimeSeconds, scaleMax, playtimeMarker.Width, out double left, out _))
            {
                playtimeMarker.Visibility = Visibility.Collapsed;
                return;
            }

            playtimeMarker.Margin = new Thickness(left, -PlaytimeMarkerVerticalOverhang, 0, -PlaytimeMarkerVerticalOverhang);
            playtimeMarker.Visibility = Visibility.Visible;

            if (settings.ThumbPlaytimeBrush != null)
            {
                playtimeMarker.Background = CloneBrush(settings.ThumbPlaytimeBrush, settings.ThumbPlaytimeColor);
            }
            else if (settings.ThumbPlaytimeColor.HasValue)
            {
                playtimeMarker.Background = new SolidColorBrush(settings.ThumbPlaytimeColor.Value);
            }
            else
            {
                object brush = TryFindResource("NormalBrush") ?? ResourceProvider.GetResource("NormalBrush");
                playtimeMarker.Background = brush as Brush ?? Brushes.Transparent;
            }

            Color markerFill = settings.ThumbPlaytimeColor
                ?? (playtimeMarker.Background is SolidColorBrush solidMarker ? solidMarker.Color : default);
            if (markerFill.A != 0)
            {
                playtimeMarker.BorderBrush = new SolidColorBrush(PlaylistThemeColors.GetSegmentOutlineColor(markerFill));
            }
            else
            {
                playtimeMarker.BorderBrush = Brushes.Transparent;
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

        private static CornerRadius GetSegmentCornerRadius(int index, int count)
        {
            bool first = index == 0;
            bool last = index == count - 1;
            return new CornerRadius(first ? 2 : 0, last ? 2 : 0, last ? 2 : 0, first ? 2 : 0);
        }

        private static Border CreateLabelSlice(double width, string text, Color color, double height)
        {
            return new Border
            {
                Width = width,
                MinWidth = 1,
                Height = height,
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

        private static Brush CloneBrush(Brush source, Color? fallbackColor)
        {
            if (source == null)
            {
                return fallbackColor.HasValue ? new SolidColorBrush(fallbackColor.Value) : Brushes.Transparent;
            }

            Brush clone = source.Clone();
            if (clone.CanFreeze)
            {
                clone.Freeze();
            }

            return clone;
        }

        private void ApplyInheritedTextMetrics(TextBlock text)
        {
            double fontSize = TextElement.GetFontSize(this);
            if (double.IsNaN(fontSize) || fontSize <= 0)
            {
                fontSize = GetEffectiveTextFontSize(this);
            }

            text.FontSize = fontSize;
            text.FontFamily = TextElement.GetFontFamily(this);
            text.FontStyle = TextElement.GetFontStyle(this);
            text.FontWeight = TextElement.GetFontWeight(this);
            text.FontStretch = TextElement.GetFontStretch(this);
        }

        private void RenderInteriorLabels(IReadOnlyList<InteriorLabelPlan> plans, double barWidth)
        {
            interiorLabelStrip.Children.Clear();
            if (plans.Count == 0)
            {
                return;
            }

            interiorLabelStrip.Width = barWidth;
            var labelWidths = plans.Select(plan => MeasureLabelTextWidth(plan.DisplayLabel)).ToArray();
            var show = plans.Select(_ => true).ToArray();
            var overlapPlans = plans
                .Select(plan => new HltbInteriorLabelOverlap.LabelPlan(plan.CenterX, plan.SliceWidth))
                .ToArray();
            HltbInteriorLabelOverlap.SuppressOverlapping(overlapPlans, labelWidths, show);

            for (int i = 0; i < plans.Count; i++)
            {
                if (!show[i])
                {
                    continue;
                }

                InteriorLabelPlan plan = plans[i];
                var text = new TextBlock
                {
                    Text = plan.DisplayLabel,
                    IsHitTestVisible = false,
                    TextTrimming = TextTrimming.None,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(plan.TextColor),
                };
                ApplyInheritedTextMetrics(text);
                text.Measure(new Size(double.PositiveInfinity, SegmentStripHeight));
                Canvas.SetLeft(text, plan.CenterX - (text.DesiredSize.Width / 2.0));
                Canvas.SetTop(text, Math.Max(0, (SegmentStripHeight - text.DesiredSize.Height) / 2.0));
                interiorLabelStrip.Children.Add(text);
            }
        }

        private double MeasureLabelTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var probe = new TextBlock { Text = text };
            ApplyInheritedTextMetrics(probe);
            probe.Measure(new Size(double.PositiveInfinity, SegmentStripHeight));
            return probe.DesiredSize.Width;
        }

        /// <summary>Same heuristic as HowLongToBeat <c>PluginProgressBar.FitTimeLabel</c> for above/below strips.</summary>
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
            if (!isVisible || value <= 0 || fillBrush == null)
            {
                return;
            }

            segments.Add(new Segment
            {
                ValueSeconds = value,
                Color = color,
                FillBrush = fillBrush,
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

        private sealed class InteriorLabelPlan
        {
            public double CenterX { get; set; }

            public double SliceWidth { get; set; }

            public string DisplayLabel { get; set; }

            public Color TextColor { get; set; }
        }

        private sealed class Segment
        {
            public long ValueSeconds { get; set; }
            public Color Color { get; set; }
            public Brush FillBrush { get; set; }
        }
    }
}
