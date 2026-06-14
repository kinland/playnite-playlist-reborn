using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Theme-agnostic sort-header presenter sizing from measured visual-tree positions.
    /// </summary>
    internal static class PlaylistSortHeaderLayout
    {
        internal const double HeadRightEdgeReserve = 12;

        /// <summary>
        /// Uniform inset above, below, and to the right of the sort glyph. Must match <c>sortHeaderGlyphStyle</c> margin.
        /// </summary>
        internal const double SortHeaderGlyphInset = 2;

        /// <summary>
        /// Width for the header content presenter: chrome-right minus presenter-left in header space.
        /// </summary>
        internal static double MeasurePresenterWidth(
            GridViewColumnHeader header,
            ContentPresenter presenter,
            double fallbackReserve)
        {
            if (header == null || presenter == null || header.ActualWidth <= 0)
            {
                return 0;
            }

            double contentLeft;
            double targetClipRight;
            double hardClipRight;
            if (!TryGetContentBounds(header, presenter, out contentLeft, out targetClipRight, out hardClipRight))
            {
                return GetFallbackWidth(header, fallbackReserve);
            }

            return ComputePresenterWidth(contentLeft, targetClipRight);
        }

        /// <summary>
        /// After layout, widen or narrow the presenter so the glyph has uniform inset on top, bottom, and right.
        /// </summary>
        internal static double FineTunePresenterWidthForGlyph(
            GridViewColumnHeader header,
            ContentPresenter presenter,
            double presenterWidth)
        {
            if (header == null || presenter == null || presenterWidth <= 0)
            {
                return presenterWidth;
            }

            TextBlock glyph = FindVisibleSortGlyph(presenter);
            if (glyph == null || glyph.ActualWidth <= 0)
            {
                return presenterWidth;
            }

            double contentLeft;
            double targetClipRight;
            double hardClipRight;
            if (!TryGetContentBounds(header, presenter, out contentLeft, out targetClipRight, out hardClipRight))
            {
                return presenterWidth;
            }

            double inset = GetGlyphUniformInset(glyph);

            try
            {
                GeneralTransform glyphToHeader = glyph.TransformToAncestor(header);
                if (glyphToHeader == null)
                {
                    return presenterWidth;
                }

                double glyphRight = glyphToHeader.Transform(new Point(glyph.ActualWidth, 0)).X;
                double targetGlyphRight = Math.Min(targetClipRight, hardClipRight) - inset;

                if (glyphRight > hardClipRight + 0.5)
                {
                    return Math.Max(0, presenterWidth - (glyphRight - hardClipRight));
                }

                if (glyphRight < targetGlyphRight - 0.5)
                {
                    return presenterWidth + (targetGlyphRight - glyphRight);
                }

                return presenterWidth;
            }
            catch (InvalidOperationException)
            {
                return presenterWidth;
            }
        }

        internal static double GetGlyphUniformInset(TextBlock glyph)
        {
            if (glyph == null)
            {
                return SortHeaderGlyphInset;
            }

            Thickness margin = glyph.Margin;
            return margin.Right > 0 ? margin.Right : SortHeaderGlyphInset;
        }

        internal static double ComputeClipRight(double gripperLeft, double headerContentRight)
        {
            if (gripperLeft <= 0 && headerContentRight <= 0)
            {
                return 0;
            }

            if (gripperLeft <= 0)
            {
                return headerContentRight;
            }

            if (headerContentRight <= 0)
            {
                return gripperLeft;
            }

            return Math.Min(gripperLeft, headerContentRight);
        }

        internal static double ComputePresenterWidth(double contentLeft, double clipRight)
        {
            return Math.Max(0, clipRight - contentLeft);
        }

        internal static double GetParentRightInset(FrameworkElement parent)
        {
            if (parent == null)
            {
                return 0;
            }

            Border border = parent as Border;
            if (border != null)
            {
                return border.BorderThickness.Right + border.Padding.Right;
            }

            return parent.Margin.Right;
        }

        internal static bool TryGetGripperLeftEdge(GridViewColumnHeader header, out double gripperLeft)
        {
            gripperLeft = double.NaN;
            if (header == null)
            {
                return false;
            }

            Thumb gripper = FindColumnResizeGripper(header);
            if (gripper == null)
            {
                return false;
            }

            return TryTransformToHeader(gripper, header, new Point(0, 0), out gripperLeft) && gripperLeft > 0;
        }

        private static bool TryGetContentBounds(
            GridViewColumnHeader header,
            ContentPresenter presenter,
            out double contentLeft,
            out double targetClipRight,
            out double hardClipRight)
        {
            contentLeft = 0;
            targetClipRight = 0;
            hardClipRight = 0;

            if (!TryTransformToHeader(presenter, header, new Point(0, 0), out contentLeft))
            {
                return false;
            }

            double gripperLeft;
            if (!TryGetGripperLeftEdge(header, out gripperLeft))
            {
                return false;
            }

            double headerContentRight = header.ActualWidth - header.Padding.Right;
            targetClipRight = ComputeClipRight(gripperLeft, headerContentRight);

            double parentInteriorRight = GetParentInteriorRightInHeader(header, presenter);
            hardClipRight = parentInteriorRight > 0
                ? Math.Min(targetClipRight, parentInteriorRight)
                : targetClipRight;

            return targetClipRight > contentLeft;
        }

        private static double GetParentInteriorRightInHeader(GridViewColumnHeader header, ContentPresenter presenter)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(presenter);
            FrameworkElement parentElement = parent as FrameworkElement;
            if (parentElement == null || parentElement.ActualWidth <= 0)
            {
                return 0;
            }

            try
            {
                GeneralTransform parentToHeader = parentElement.TransformToAncestor(header);
                if (parentToHeader == null)
                {
                    return 0;
                }

                double outerRight = parentToHeader.Transform(new Point(parentElement.ActualWidth, 0)).X;
                return Math.Max(0, outerRight - GetParentRightInset(parentElement));
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }

        private static double GetFallbackWidth(GridViewColumnHeader header, double fallbackReserve)
        {
            return Math.Max(
                0,
                header.ActualWidth - header.Padding.Left - header.Padding.Right - fallbackReserve);
        }

        private static bool TryTransformToHeader(
            FrameworkElement element,
            GridViewColumnHeader header,
            Point point,
            out double headerX)
        {
            headerX = 0;
            if (element == null || header == null)
            {
                return false;
            }

            try
            {
                GeneralTransform transform = element.TransformToAncestor(header);
                if (transform == null)
                {
                    return false;
                }

                headerX = transform.Transform(point).X;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Chooses a darkening overlay when header text reads dark, otherwise a lightening overlay.
        /// </summary>
        internal static void ApplyActiveSortHighlight(
            Border border,
            SolidColorBrush background,
            SolidColorBrush borderBrush,
            bool forceFullOpacity)
        {
            if (border == null)
            {
                return;
            }

            border.BeginAnimation(Border.BackgroundProperty, null);
            border.BeginAnimation(Border.BorderBrushProperty, null);
            border.Background = background;
            border.BorderBrush = borderBrush;

            if (forceFullOpacity)
            {
                border.BeginAnimation(UIElement.OpacityProperty, null);
                border.Opacity = 1.0;
            }
        }

        internal static void ClearActiveSortHighlight(Border border, bool clearForcedOpacity)
        {
            if (border == null)
            {
                return;
            }

            border.BeginAnimation(Border.BackgroundProperty, null);
            border.BeginAnimation(Border.BorderBrushProperty, null);
            border.ClearValue(Border.BackgroundProperty);
            border.ClearValue(Border.BorderBrushProperty);

            if (clearForcedOpacity)
            {
                border.BeginAnimation(UIElement.OpacityProperty, null);
                border.ClearValue(UIElement.OpacityProperty);
            }
        }

        /// <summary>
        /// Reapplies idle border brushes from resources after clearing a code-applied hover/active highlight.
        /// </summary>
        internal static void RestoreIdleHeaderBorderChrome(GridViewColumnHeader header, Border border)
        {
            if (header == null || border == null)
            {
                return;
            }

            border.BeginAnimation(Border.BorderBrushProperty, null);
            Brush idleBorder = PlaylistThemeChrome.ResolveIdleHeaderBorderBrush(header);
            if (idleBorder != null)
            {
                border.BorderBrush = idleBorder;
            }
            else
            {
                border.ClearValue(Border.BorderBrushProperty);
            }
        }

        /// <summary>
        /// Finds the header chrome border used for hover and active-sort highlighting (<c>HoverBg</c>).
        /// </summary>
        internal static Border FindHeaderHighlightBorder(GridViewColumnHeader header)
        {
            if (header == null)
            {
                return null;
            }

            header.ApplyTemplate();
            if (header.Template != null)
            {
                if (header.Template.FindName("HoverBg", header) is Border hoverBg)
                {
                    return hoverBg;
                }

                if (header.Template.FindName("SelectedBg", header) is Border selectedBg)
                {
                    return selectedBg;
                }
            }

            return FindFirstBorderChild(header);
        }

        private const double RoundedHeaderCutoutMinRadius = 4;

        /// <summary>
        /// Corner radius used to cut the top-center V gap out of the column reorder slot when header corners are rounded.
        /// </summary>
        internal static double GetRoundedHeaderSlotTopInset(GridViewColumnHeader header)
        {
            if (header == null)
            {
                return 0;
            }

            double topRadius = 0;
            Border hoverBg = FindHeaderHighlightBorder(header);
            if (hoverBg != null)
            {
                topRadius = Math.Max(hoverBg.CornerRadius.TopLeft, hoverBg.CornerRadius.TopRight);
            }

            if (topRadius < 1
                && header.TryFindResource("ControlCornerRadius") is CornerRadius resourceRadius)
            {
                topRadius = Math.Max(resourceRadius.TopLeft, resourceRadius.TopRight);
            }

            return topRadius >= RoundedHeaderCutoutMinRadius ? topRadius : 0;
        }

        private static Border FindFirstBorderChild(DependencyObject parent)
        {
            foreach (Border border in PlaylistVisualTree.FindVisualChildren<Border>(parent))
            {
                return border;
            }

            return null;
        }

        private static TextBlock FindVisibleSortGlyph(DependencyObject root)
        {
            foreach (TextBlock textBlock in PlaylistVisualTree.FindVisualChildren<TextBlock>(root))
            {
                if (textBlock.Visibility != Visibility.Visible)
                {
                    continue;
                }

                if (textBlock.Tag as string == "RankHeaderGlyph")
                {
                    return textBlock;
                }

                if (textBlock.Text == "▲" || textBlock.Text == "▼")
                {
                    return textBlock;
                }
            }

            return null;
        }

        private static Thumb FindColumnResizeGripper(GridViewColumnHeader header)
        {
            foreach (Thumb thumb in PlaylistVisualTree.FindVisualChildren<Thumb>(header))
            {
                if (thumb.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                if (thumb.Name == "PART_HeaderGripper")
                {
                    return thumb;
                }
            }

            return null;
        }
    }
}
