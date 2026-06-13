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

        internal static double GetRelativeLuminance(Color color)
        {
            return ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255.0;
        }

        private static bool IsLowChromaHighlightColor(Color color)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));
            return max - min < 45;
        }

        /// <summary>
        /// Chooses a darkening overlay when header text reads dark, otherwise a lightening overlay.
        /// </summary>
        internal static bool UseDarkeningOverlay(Color? headerTextColor, Func<string, object> tryFindResource = null)
        {
            if (headerTextColor.HasValue)
            {
                return GetRelativeLuminance(headerTextColor.Value) < 0.5;
            }

            if (tryFindResource != null && tryFindResource("TextBrush") is SolidColorBrush textBrush)
            {
                return GetRelativeLuminance(textBrush.Color) < 0.5;
            }

            return false;
        }

        internal static Color? TryGetHeaderLabelColor(GridViewColumnHeader header)
        {
            if (header == null)
            {
                return null;
            }

            // Ignore code-applied hover/active foreground; sample resource-dictionary or style-inherited text color only.
            if (header.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
            {
                Brush headerForeground = (Brush)header.GetValue(Control.ForegroundProperty);
                if (TryGetColor(headerForeground, out Color headerColor) && headerColor.A > 16)
                {
                    return headerColor;
                }
            }

            header.ApplyTemplate();
            foreach (TextBlock textBlock in FindVisualChildren<TextBlock>(header))
            {
                if (textBlock.Visibility != Visibility.Visible)
                {
                    continue;
                }

                if (textBlock.Tag as string == "RankHeaderGlyph")
                {
                    continue;
                }

                if (textBlock.Text == "▲" || textBlock.Text == "▼")
                {
                    continue;
                }

                if (textBlock.ReadLocalValue(TextBlock.ForegroundProperty) != DependencyProperty.UnsetValue)
                {
                    continue;
                }

                if (TryGetColor(textBlock.Foreground as Brush, out Color labelColor) && labelColor.A > 16)
                {
                    return labelColor;
                }

                return null;
            }

            return null;
        }

        internal static (SolidColorBrush Background, SolidColorBrush Border, SolidColorBrush Foreground) CreateActiveSortHighlightBrushes(bool useDarkeningOverlay)
        {
            Color backgroundColor = useDarkeningOverlay
                ? Color.FromArgb(0x99, 0x00, 0x00, 0x00)
                : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
            Color borderColor = useDarkeningOverlay
                ? Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A)
                : Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

            var background = new SolidColorBrush(backgroundColor);
            var border = new SolidColorBrush(borderColor);
            var foreground = new SolidColorBrush(Colors.White);
            background.Freeze();
            border.Freeze();
            foreground.Freeze();
            return (background, border, foreground);
        }

        internal static string GetVisibleRowHighlightBrushKey(ListViewItem item, bool isHoverActive)
        {
            if (item == null)
            {
                return null;
            }

            // Match row template: selection fill wins over hover fill.
            if (item.IsSelected)
            {
                return "GlyphBrush";
            }

            if (isHoverActive)
            {
                return "HoverBrush";
            }

            return null;
        }

        internal static bool TryGetVisibleRowHighlightColor(
            ListViewItem item,
            bool isHoverActive,
            Func<string, object> tryFindResource,
            out Color color)
        {
            color = default;
            string brushKey = GetVisibleRowHighlightBrushKey(item, isHoverActive);
            if (brushKey == null)
            {
                return false;
            }

            if (!(tryFindResource?.Invoke(brushKey) is SolidColorBrush brush)
                || !TryGetColor(brush, out color))
            {
                return false;
            }

            return true;
        }

        internal enum ListRowEmbeddedChromeStyle
        {
            None,
            /// <summary>Light control panel + dark glyph — active dark HoverBrush mouseover when managed chrome applies.</summary>
            LightPanelDarkGlyph,
            /// <summary>Dark control panel + light glyph — active GlyphBrush selection when managed chrome applies.</summary>
            DarkPanelLightGlyph,
        }

        /// <summary>
        /// True when <c>HoverBrush</c> is dark and low-chroma and <c>GlyphBrush</c> is light and low-chroma.
        /// When false, embedded controls keep resource-dictionary styling.
        /// </summary>
        internal static bool UsesInvertedRowHighlightChrome(Func<string, object> tryFindResource)
        {
            if (!(tryFindResource?.Invoke("HoverBrush") is SolidColorBrush hoverBrush)
                || !(tryFindResource?.Invoke("GlyphBrush") is SolidColorBrush glyphBrush)
                || !TryGetColor(hoverBrush, out Color hoverColor)
                || !TryGetColor(glyphBrush, out Color glyphColor))
            {
                return false;
            }

            return GetRelativeLuminance(hoverColor) < 0.5
                && GetRelativeLuminance(glyphColor) >= 0.5
                && IsLowChromaHighlightColor(hoverColor)
                && IsLowChromaHighlightColor(glyphColor);
        }

        internal static ListRowEmbeddedChromeStyle GetEmbeddedChromeStyle(
            ListViewItem item,
            bool isHoverActive,
            Func<string, object> tryFindResource)
        {
            return GetEmbeddedChromeStyle(item, isHoverActive, isDirectHover: false, tryFindResource);
        }

        internal static ListRowEmbeddedChromeStyle GetEmbeddedChromeStyle(
            ListViewItem item,
            bool isHoverActive,
            bool isDirectHover,
            Func<string, object> tryFindResource)
        {
            if (item == null
                || !UsesInvertedRowHighlightChrome(tryFindResource)
                || !TryGetVisibleRowHighlightColor(item, isHoverActive, tryFindResource, out _))
            {
                return ListRowEmbeddedChromeStyle.None;
            }

            if (item.IsSelected)
            {
                return ListRowEmbeddedChromeStyle.DarkPanelLightGlyph;
            }

            if (isHoverActive)
            {
                return ListRowEmbeddedChromeStyle.LightPanelDarkGlyph;
            }

            return ListRowEmbeddedChromeStyle.None;
        }

        /// <summary>
        /// Row text: light foreground when managed chrome applies on a dark HoverBrush mouseover row.
        /// </summary>
        internal static void ApplyListRowHighlightForeground(
            ListViewItem item,
            Func<string, object> tryFindResource,
            bool isHoverActive)
        {
            if (item == null
                || !UsesInvertedRowHighlightChrome(tryFindResource)
                || item.IsSelected
                || !isHoverActive
                || !TryGetVisibleRowHighlightColor(item, isHoverActive, tryFindResource, out Color rowColor)
                || GetRelativeLuminance(rowColor) >= 0.5)
            {
                item.ClearValue(Control.ForegroundProperty);
                return;
            }

            item.Foreground = CreateActiveSortHighlightBrushes(useDarkeningOverlay: true).Foreground;
        }

        /// <summary>
        /// Managed play-button chrome when <see cref="UsesInvertedRowHighlightChrome"/> is true
        /// (see docs/row-highlight-chrome-investigation.md). Otherwise use
        /// <c>playlistIconPlayButtonStyleThemed</c> and do not call this.
        /// </summary>
        internal static void ApplyListRowPlayButtonChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            Func<string, object> tryFindResource)
        {
            ApplyListRowPlayButtonChrome(control, row, isHoverActive, isDirectHover: false, tryFindResource);
        }

        internal static void ApplyListRowPlayButtonChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            bool isDirectHover,
            Func<string, object> tryFindResource)
        {
            if (control == null || !UsesInvertedRowHighlightChrome(tryFindResource))
            {
                return;
            }

            ListRowEmbeddedChromeStyle baseStyle = GetEmbeddedChromeStyle(row, isHoverActive, isDirectHover, tryFindResource);
            switch (baseStyle)
            {
                case ListRowEmbeddedChromeStyle.LightPanelDarkGlyph:
                case ListRowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    ApplyManagedEmbeddedChrome(tryFindResource, control, baseStyle, isDirectHover, useBaseBorder: true);
                    break;
                default:
                    ClearListRowControlChrome(control);
                    break;
            }
        }

        internal static void ClearListRowControlChrome(Control control)
        {
            ClearEmbeddedChrome(control);
        }

        /// <summary>
        /// Row-level chrome from <paramref name="baseStyle"/>; direct control hover inverts panel/glyph for clickability.
        /// </summary>
        private static void ApplyManagedEmbeddedChrome(
            Func<string, object> tryFindResource,
            Control control,
            ListRowEmbeddedChromeStyle baseStyle,
            bool isDirectHover,
            bool useBaseBorder)
        {
            ListRowEmbeddedChromeStyle appliedStyle = isDirectHover ? InvertEmbeddedChromeStyle(baseStyle) : baseStyle;
            bool showBorder = isDirectHover || useBaseBorder;
            ApplyEmbeddedChromeStyle(tryFindResource, control, appliedStyle, showBorder, isDirectHover);
        }

        private static ListRowEmbeddedChromeStyle InvertEmbeddedChromeStyle(ListRowEmbeddedChromeStyle style)
        {
            switch (style)
            {
                case ListRowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    return ListRowEmbeddedChromeStyle.LightPanelDarkGlyph;
                case ListRowEmbeddedChromeStyle.LightPanelDarkGlyph:
                    return ListRowEmbeddedChromeStyle.DarkPanelLightGlyph;
                default:
                    return ListRowEmbeddedChromeStyle.None;
            }
        }

        private static void ApplyEmbeddedChromeStyle(
            Func<string, object> tryFindResource,
            Control control,
            ListRowEmbeddedChromeStyle style,
            bool showBorder,
            bool directHoverBorder)
        {
            Brush borderBrush = showBorder
                ? ResolveEmbeddedBorderBrush(tryFindResource, style, directHoverBorder)
                : null;

            switch (style)
            {
                case ListRowEmbeddedChromeStyle.LightPanelDarkGlyph:
                    ApplyEmbeddedChrome(
                        control,
                        ResolveResourceBrush(tryFindResource, "PopupBackgroundBrush", "ControlBackgroundBrush"),
                        ResolveDarkGlyphBrush(tryFindResource),
                        borderBrush);
                    break;
                case ListRowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    ApplyEmbeddedChrome(
                        control,
                        ResolveDarkPanelBackground(tryFindResource),
                        CreateActiveSortHighlightBrushes(useDarkeningOverlay: true).Foreground,
                        borderBrush);
                    break;
                default:
                    ClearEmbeddedChrome(control);
                    break;
            }
        }

        private static Brush ResolveDarkGlyphBrush(Func<string, object> tryFindResource)
        {
            Brush textBrush = ResolveResourceBrush(tryFindResource, "TextBrush");
            if (textBrush is SolidColorBrush solidText
                && TryGetColor(solidText, out Color textColor)
                && GetRelativeLuminance(textColor) < 0.5)
            {
                return textBrush;
            }

            var fallback = new SolidColorBrush(Color.FromRgb(24, 24, 24));
            fallback.Freeze();
            return fallback;
        }

        /// <summary>
        /// HLTB plugin button host: managed embedded chrome when <see cref="UsesInvertedRowHighlightChrome"/> is true.
        /// </summary>
        internal static void ApplyListRowEmbeddedControlChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            Func<string, object> tryFindResource)
        {
            ApplyListRowEmbeddedControlChrome(control, row, isHoverActive, isDirectHover: false, tryFindResource);
        }

        internal static void ApplyListRowEmbeddedControlChrome(
            Control control,
            ListViewItem row,
            bool isHoverActive,
            bool isDirectHover,
            Func<string, object> tryFindResource)
        {
            if (control == null)
            {
                return;
            }

            if (!UsesInvertedRowHighlightChrome(tryFindResource))
            {
                ClearEmbeddedChrome(control);
                return;
            }

            ListRowEmbeddedChromeStyle baseStyle = GetEmbeddedChromeStyle(row, isHoverActive, isDirectHover, tryFindResource);
            switch (baseStyle)
            {
                case ListRowEmbeddedChromeStyle.LightPanelDarkGlyph:
                case ListRowEmbeddedChromeStyle.DarkPanelLightGlyph:
                    ApplyManagedEmbeddedChrome(tryFindResource, control, baseStyle, isDirectHover, useBaseBorder: false);
                    break;
                default:
                    ClearEmbeddedChrome(control);
                    break;
            }
        }

        private static Brush ResolveEmbeddedBorderBrush(
            Func<string, object> tryFindResource,
            ListRowEmbeddedChromeStyle style,
            bool directHoverBorder)
        {
            if (directHoverBorder)
            {
                if (style == ListRowEmbeddedChromeStyle.LightPanelDarkGlyph)
                {
                    return ResolveResourceBrush(tryFindResource, "HoverBrush", "ControlBorderBrush", "DarkControlBorderBrush");
                }

                // Dark panel over dark row highlight — light outer border for contrast.
                return ResolveResourceBrush(tryFindResource, "GlyphBrush", "PopupBackgroundBrush", "ControlBackgroundBrush", "ControlBorderBrush");
            }

            if (style == ListRowEmbeddedChromeStyle.LightPanelDarkGlyph)
            {
                return ResolveResourceBrush(tryFindResource, "ControlBorderBrush", "DarkControlBorderBrush");
            }

            return ResolveResourceBrush(tryFindResource, "DarkControlBorderBrush", "ControlBorderBrush");
        }

        private static readonly Thickness StableEmbeddedBorderThickness = new Thickness(1);

        private static void ApplyEmbeddedChrome(Control control, Brush background, Brush foreground, Brush borderBrush)
        {
            if (background != null)
            {
                control.Background = background;
            }

            if (foreground != null)
            {
                control.Foreground = foreground;
            }

            control.BorderThickness = StableEmbeddedBorderThickness;
            control.BorderBrush = borderBrush ?? System.Windows.Media.Brushes.Transparent;
        }

        private static void ClearEmbeddedChrome(Control control)
        {
            control.ClearValue(Control.BackgroundProperty);
            control.ClearValue(Control.ForegroundProperty);
            control.BorderBrush = System.Windows.Media.Brushes.Transparent;
            control.BorderThickness = new Thickness(0);
        }

        private static Brush ResolveResourceBrush(Func<string, object> tryFindResource, params string[] keys)
        {
            if (tryFindResource == null)
            {
                return null;
            }

            foreach (string key in keys)
            {
                if (tryFindResource.Invoke(key) is Brush brush)
                {
                    return brush;
                }
            }

            return null;
        }

        private static Brush ResolveDarkPanelBackground(Func<string, object> tryFindResource)
        {
            Brush hoverBrush = ResolveResourceBrush(tryFindResource, "HoverBrush");
            if (hoverBrush is SolidColorBrush solidHover
                && TryGetColor(solidHover, out Color hoverColor)
                && GetRelativeLuminance(hoverColor) < 0.5)
            {
                return hoverBrush;
            }

            var fallback = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x30));
            fallback.Freeze();
            return fallback;
        }

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
            if (header.Tag is bool useDarkStyle)
            {
                string brushKey = useDarkStyle ? "DarkControlBorderBrush" : "ControlBorderBrush";
                if (header.TryFindResource(brushKey) is Brush resourceBorder)
                {
                    border.BorderBrush = resourceBorder;
                    return;
                }
            }

            if (header.TryFindResource("NormalBorderBrush") is Brush normalBorder)
            {
                border.BorderBrush = normalBorder;
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
            foreach (Border border in FindVisualChildren<Border>(parent))
            {
                return border;
            }

            return null;
        }

        private static bool TryGetColor(Brush brush, out Color color)
        {
            color = default;
            if (brush is SolidColorBrush solid)
            {
                color = solid.Color;
                return true;
            }

            return false;
        }

        private static TextBlock FindVisibleSortGlyph(DependencyObject root)
        {
            foreach (TextBlock textBlock in FindVisualChildren<TextBlock>(root))
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
            foreach (Thumb thumb in FindVisualChildren<Thumb>(header))
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

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
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
    }
}
