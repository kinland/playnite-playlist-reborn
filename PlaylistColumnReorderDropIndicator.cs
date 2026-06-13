using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Playlist
{
    /// <summary>
    /// Shows a full-height column insert guide while the user drags a GridView header to reorder columns.
    /// </summary>
    internal sealed class PlaylistColumnReorderDropIndicator
    {
        private const double SlotWidth = 14;
        private const double GlyphFontSize = 13;
        private const double GlyphGapAboveList = 2;
        private const double HeaderBorderInset = 1;
        private readonly ListView listView;
        private GridViewHeaderRowPresenter headerRowPresenter;
        private ColumnReorderFullHeightDropAdorner dropLineAdorner;

        public PlaylistColumnReorderDropIndicator(ListView listView)
        {
            this.listView = listView ?? throw new ArgumentNullException(nameof(listView));
        }

        public void Attach()
        {
            listView.Loaded += OnListViewLoaded;
            listView.Unloaded += OnListViewUnloaded;
            listView.PreviewMouseMove += OnListViewPreviewMouseMove;
            listView.PreviewMouseLeftButtonUp += OnListViewPreviewMouseLeftButtonUp;
            if (listView.IsLoaded)
            {
                EnsureHeaderRowPresenterHook();
            }
        }

        public void Detach()
        {
            listView.Loaded -= OnListViewLoaded;
            listView.Unloaded -= OnListViewUnloaded;
            listView.PreviewMouseMove -= OnListViewPreviewMouseMove;
            listView.PreviewMouseLeftButtonUp -= OnListViewPreviewMouseLeftButtonUp;
            UnhookHeaderRowPresenter();
            HideDropLine();
        }

        private void OnListViewLoaded(object sender, RoutedEventArgs e)
        {
            EnsureHeaderRowPresenterHook();
        }

        private void OnListViewUnloaded(object sender, RoutedEventArgs e)
        {
            UnhookHeaderRowPresenter();
            HideDropLine();
        }

        private void EnsureHeaderRowPresenterHook()
        {
            if (headerRowPresenter != null)
            {
                return;
            }

            headerRowPresenter = FindVisualChildren<GridViewHeaderRowPresenter>(listView).FirstOrDefault();
            if (headerRowPresenter == null)
            {
                listView.Dispatcher.BeginInvoke(
                    (Action)EnsureHeaderRowPresenterHook,
                    DispatcherPriority.Loaded);
            }
        }

        private void UnhookHeaderRowPresenter()
        {
            headerRowPresenter = null;
        }

        private void OnListViewPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsColumnReorderDragActive(e))
            {
                ClearDropIndicator();
                return;
            }

            if (headerRowPresenter == null)
            {
                EnsureHeaderRowPresenterHook();
            }

            if (headerRowPresenter == null)
            {
                return;
            }

            SuppressBuiltInDropSeparators(headerRowPresenter);

            Point mouseInPresenter = e.GetPosition(headerRowPresenter);
            List<GridViewColumnHeader> headers = GetReorderableHeaders(headerRowPresenter);
            List<(double Left, double Right)> headerBounds = headers
                .Select(header =>
                {
                    Point left = header.TranslatePoint(new Point(0, 0), headerRowPresenter);
                    Point right = header.TranslatePoint(new Point(header.ActualWidth, 0), headerRowPresenter);
                    return (left.X, right.X);
                })
                .ToList();

            int dropIndex = PlaylistColumnReorderDropLayout.GetDropIndex(headerBounds, mouseInPresenter.X);
            double lineXInPresenter = PlaylistColumnReorderDropLayout.GetDropLineX(headerBounds, dropIndex);
            Point lineInListView = headerRowPresenter.TranslatePoint(new Point(lineXInPresenter, 0), listView);
            ShowDropLine(lineInListView.X);
        }

        private void OnListViewPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ClearDropIndicator();
        }

        private void ClearDropIndicator()
        {
            HideDropLine();
            if (headerRowPresenter != null)
            {
                SuppressBuiltInDropSeparators(headerRowPresenter);
            }
        }

        private bool IsColumnReorderDragActive(MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return false;
            }

            if (Mouse.Captured is Thumb)
            {
                return false;
            }

            return FindVisualChildren<GridViewColumnHeader>(listView)
                .Any(header => header.Role == GridViewColumnHeaderRole.Floating);
        }

        private static List<GridViewColumnHeader> GetReorderableHeaders(GridViewHeaderRowPresenter presenter)
        {
            return FindVisualChildren<GridViewColumnHeader>(presenter)
                .Where(header => header.Role != GridViewColumnHeaderRole.Padding
                    && header.Role != GridViewColumnHeaderRole.Floating
                    && header.IsVisible)
                .OrderBy(header => header.TranslatePoint(new Point(0, 0), presenter).X)
                .ToList();
        }

        private void ShowDropLine(double lineXInListView)
        {
            if (headerRowPresenter == null)
            {
                return;
            }

            UIElement adornerTarget = GetAdornerTarget();
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(adornerTarget);
            if (layer == null)
            {
                return;
            }

            Point lineInTarget = listView.TranslatePoint(new Point(lineXInListView, 0), adornerTarget);
            (double slotTop, double slotBottom, double glyphAnchorTop, double topCornerCutoutRadius) = GetSlotBoundsInTarget(adornerTarget);

            if (dropLineAdorner == null)
            {
                DropMarkerPalette palette = CreateDropMarkerPalette();
                dropLineAdorner = new ColumnReorderFullHeightDropAdorner(adornerTarget, palette);
                layer.Add(dropLineAdorner);
            }

            dropLineAdorner.LineX = lineInTarget.X;
            dropLineAdorner.SlotTopY = slotTop;
            dropLineAdorner.SlotBottomY = slotBottom;
            dropLineAdorner.GlyphAnchorTopY = glyphAnchorTop;
            dropLineAdorner.TopCornerCutoutRadius = topCornerCutoutRadius;
        }

        private (double SlotTop, double SlotBottom, double GlyphAnchorTop, double TopCornerCutoutRadius) GetSlotBoundsInTarget(UIElement adornerTarget)
        {
            Point listBottomInTarget = listView.TranslatePoint(new Point(0, listView.ActualHeight), adornerTarget);
            GridViewColumnHeader anchorHeader = GetReorderableHeaders(headerRowPresenter).FirstOrDefault()
                ?? FindVisualChildren<GridViewColumnHeader>(listView)
                    .FirstOrDefault(header => header.Role != GridViewColumnHeaderRole.Padding);
            if (anchorHeader != null)
            {
                Point headerTopLeft = anchorHeader.TranslatePoint(new Point(0, 0), adornerTarget);
                double visibleHeaderTop = headerTopLeft.Y + HeaderBorderInset;
                double topCornerCutoutRadius = PlaylistSortHeaderLayout.GetRoundedHeaderSlotTopInset(anchorHeader);
                return (visibleHeaderTop, listBottomInTarget.Y, headerTopLeft.Y, topCornerCutoutRadius);
            }

            Point presenterTop = headerRowPresenter.TranslatePoint(new Point(0, 0), adornerTarget);
            double visiblePresenterTop = presenterTop.Y + HeaderBorderInset;
            return (visiblePresenterTop, listBottomInTarget.Y, presenterTop.Y, 0);
        }

        private UIElement GetAdornerTarget()
        {
            return VisualTreeHelper.GetParent(listView) as UIElement ?? listView;
        }

        private void HideDropLine()
        {
            if (dropLineAdorner == null)
            {
                return;
            }

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(dropLineAdorner.AdornedElement);
            layer?.Remove(dropLineAdorner);
            dropLineAdorner = null;
        }

        private static void SuppressBuiltInDropSeparators(GridViewHeaderRowPresenter presenter)
        {
            foreach (Separator separator in FindVisualChildren<Separator>(presenter))
            {
                separator.Visibility = Visibility.Collapsed;
                separator.Opacity = 0;
                separator.Width = 0;
            }
        }

        private DropMarkerPalette CreateDropMarkerPalette()
        {
            GridViewColumnHeader sampleHeader = FindVisualChildren<GridViewColumnHeader>(listView)
                .FirstOrDefault(header => header.Role != GridViewColumnHeaderRole.Padding);
            Color? labelColor = PlaylistSortHeaderLayout.TryGetHeaderLabelColor(sampleHeader);
            bool lightTheme = labelColor.HasValue
                && PlaylistSortHeaderLayout.GetRelativeLuminance(labelColor.Value) < 0.45;

            Color slotCenterColor = lightTheme
                ? Color.FromArgb(190, 0, 102, 204)
                : Color.FromArgb(200, 80, 200, 255);
            Color glyphColor = lightTheme
                ? Color.FromRgb(0, 102, 204)
                : Color.FromRgb(120, 220, 255);

            return new DropMarkerPalette(slotCenterColor, glyphColor);
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

        private readonly struct DropMarkerPalette
        {
            public DropMarkerPalette(Color slotCenter, Color glyph)
            {
                SlotCenter = slotCenter;
                Glyph = glyph;
            }

            public Color SlotCenter { get; }

            public Color Glyph { get; }
        }

        private sealed class ColumnReorderFullHeightDropAdorner : Adorner
        {
            private readonly DropMarkerPalette palette;
            private double lineX;
            private double slotTopY;
            private double slotBottomY;
            private double glyphAnchorTopY;
            private double topCornerCutoutRadius;

            public ColumnReorderFullHeightDropAdorner(UIElement adornedElement, DropMarkerPalette palette)
                : base(adornedElement)
            {
                IsHitTestVisible = false;
                this.palette = palette;
            }

            public double LineX
            {
                get => lineX;
                set => SetVisualProperty(ref lineX, value);
            }

            public double SlotTopY
            {
                get => slotTopY;
                set => SetVisualProperty(ref slotTopY, value);
            }

            public double SlotBottomY
            {
                get => slotBottomY;
                set => SetVisualProperty(ref slotBottomY, value);
            }

            public double GlyphAnchorTopY
            {
                get => glyphAnchorTopY;
                set => SetVisualProperty(ref glyphAnchorTopY, value);
            }

            public double TopCornerCutoutRadius
            {
                get => topCornerCutoutRadius;
                set => SetVisualProperty(ref topCornerCutoutRadius, value);
            }

            private void SetVisualProperty(ref double field, double value)
            {
                if (field == value)
                {
                    return;
                }

                field = value;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                if (lineX < 0 || slotBottomY <= slotTopY)
                {
                    return;
                }

                double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                double clipTop = slotTopY;
                double clipBottom = slotBottomY;
                double clipHeight = clipBottom - clipTop;
                if (clipHeight <= 0)
                {
                    return;
                }

                // Draw slightly above the clip line so anti-aliasing cannot bleed past the header border.
                double drawTop = clipTop - (1.0 / pixelsPerDip);
                double drawHeight = clipBottom - drawTop;
                Rect slotRect = new Rect(lineX - (SlotWidth / 2), drawTop, SlotWidth, drawHeight);
                Geometry clipGeometry = CreateSlotClipGeometry(
                    AdornedElement.RenderSize.Width,
                    clipTop,
                    clipBottom,
                    lineX,
                    topCornerCutoutRadius);

                drawingContext.PushClip(clipGeometry);
                drawingContext.DrawRectangle(CreateSlotGradientBrush(palette.SlotCenter), null, slotRect);
                drawingContext.Pop();

                DrawFloatingGlyph(drawingContext, glyphAnchorTopY);
            }

            private static Geometry CreateSlotClipGeometry(
                double width,
                double top,
                double bottom,
                double centerX,
                double cutoutRadius)
            {
                if (cutoutRadius < 1)
                {
                    return new RectangleGeometry(new Rect(0, top, width, bottom - top));
                }

                // Dip the top-center of the clip (\/) so glow does not fill the rounded-header seam gap.
                PathFigure figure = new PathFigure
                {
                    StartPoint = new Point(0, top),
                    IsClosed = true,
                };
                figure.Segments.Add(new LineSegment(new Point(centerX - cutoutRadius, top), true));
                figure.Segments.Add(new LineSegment(new Point(centerX, top + cutoutRadius), true));
                figure.Segments.Add(new LineSegment(new Point(centerX + cutoutRadius, top), true));
                figure.Segments.Add(new LineSegment(new Point(width, top), true));
                figure.Segments.Add(new LineSegment(new Point(width, bottom), true));
                figure.Segments.Add(new LineSegment(new Point(0, bottom), true));

                PathGeometry geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                geometry.Freeze();
                return geometry;
            }

            private void DrawFloatingGlyph(DrawingContext drawingContext, double glyphAnchorTop)
            {
                double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                Brush glyphBrush = new SolidColorBrush(palette.Glyph);
                glyphBrush.Freeze();

                FormattedText glyphText = new FormattedText(
                    "🡇",
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    GlyphFontSize,
                    glyphBrush,
                    pixelsPerDip);

                double glyphX = lineX - (glyphText.Width / 2);
                double glyphY = glyphAnchorTop - GlyphGapAboveList - glyphText.Height;
                drawingContext.DrawText(glyphText, new Point(glyphX, glyphY));
            }

            private static LinearGradientBrush CreateSlotGradientBrush(Color centerColor)
            {
                Color edgeColor = Color.FromArgb(0, centerColor.R, centerColor.G, centerColor.B);
                LinearGradientBrush brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    MappingMode = BrushMappingMode.RelativeToBoundingBox,
                };
                brush.GradientStops.Add(new GradientStop(edgeColor, 0));
                brush.GradientStops.Add(new GradientStop(centerColor, 0.5));
                brush.GradientStops.Add(new GradientStop(edgeColor, 1));
                brush.Freeze();
                return brush;
            }
        }
    }

    internal static class PlaylistColumnReorderDropLayout
    {
        internal static int GetDropIndex(IReadOnlyList<(double Left, double Right)> headerBounds, double mouseX)
        {
            if (headerBounds == null || headerBounds.Count == 0)
            {
                return 0;
            }

            for (int index = 0; index < headerBounds.Count; index++)
            {
                (double left, double right) = headerBounds[index];
                double center = (left + right) / 2;
                if (mouseX < center)
                {
                    return index;
                }
            }

            return headerBounds.Count;
        }

        internal static double GetDropLineX(IReadOnlyList<(double Left, double Right)> headerBounds, int dropIndex)
        {
            if (headerBounds == null || headerBounds.Count == 0)
            {
                return 0;
            }

            if (dropIndex <= 0)
            {
                return headerBounds[0].Left;
            }

            if (dropIndex >= headerBounds.Count)
            {
                return headerBounds[headerBounds.Count - 1].Right;
            }

            return headerBounds[dropIndex].Left;
        }
    }
}
