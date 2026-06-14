using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Mouse-following feedback while a playlist row reorder is blocked. Uses an adorner instead of
    /// <see cref="System.Windows.Controls.Primitives.Popup"/> because popups do not reliably appear
    /// during an active WPF drag-and-drop operation.
    /// </summary>
    internal sealed class PlaylistDragReorderStatusIndicator
    {
        private const double Offset = 16;
        private const double MaxTextWidth = 360;
        private const double PaddingX = 8;
        private const double PaddingY = 4;
        private const double CornerRadius = 4;

        private readonly UIElement adornerTarget;
        private DragReorderStatusAdorner adorner;

        public PlaylistDragReorderStatusIndicator(UIElement adornerTarget)
        {
            this.adornerTarget = adornerTarget ?? throw new ArgumentNullException(nameof(adornerTarget));
        }

        public bool IsVisible => adorner != null;

        public void Show(string text, Point positionInAdornerTarget)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Hide();
                return;
            }

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(adornerTarget);
            if (layer == null)
            {
                return;
            }

            if (adorner == null)
            {
                adorner = new DragReorderStatusAdorner(adornerTarget);
                layer.Add(adorner);
            }

            adorner.Text = text;
            adorner.Position = new Point(positionInAdornerTarget.X + Offset, positionInAdornerTarget.Y + Offset);
        }

        public void UpdatePosition(Point positionInAdornerTarget)
        {
            if (adorner == null)
            {
                return;
            }

            adorner.Position = new Point(positionInAdornerTarget.X + Offset, positionInAdornerTarget.Y + Offset);
        }

        public void Hide()
        {
            if (adorner == null)
            {
                return;
            }

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(adornerTarget);
            layer?.Remove(adorner);
            adorner = null;
        }

        private sealed class DragReorderStatusAdorner : Adorner
        {
            private static readonly Brush BackgroundBrush = CreateFrozenBrush(Color.FromArgb(0xE6, 0, 0, 0));
            private static readonly Brush ForegroundBrush = CreateFrozenBrush(Colors.White);
            private string text = string.Empty;
            private Point position;

            public DragReorderStatusAdorner(UIElement adornedElement)
                : base(adornedElement)
            {
                IsHitTestVisible = false;
            }

            public string Text
            {
                get => text;
                set
                {
                    if (string.Equals(text, value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    text = value ?? string.Empty;
                    InvalidateVisual();
                }
            }

            public Point Position
            {
                get => position;
                set
                {
                    if (position == value)
                    {
                        return;
                    }

                    position = value;
                    InvalidateVisual();
                }
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                FormattedText formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    ForegroundBrush,
                    pixelsPerDip)
                {
                    MaxTextWidth = MaxTextWidth,
                };

                double width = formattedText.Width + (PaddingX * 2);
                double height = formattedText.Height + (PaddingY * 2);
                Rect backgroundRect = new Rect(position.X, position.Y, width, height);
                drawingContext.DrawRoundedRectangle(BackgroundBrush, null, backgroundRect, CornerRadius, CornerRadius);
                drawingContext.DrawText(
                    formattedText,
                    new Point(position.X + PaddingX, position.Y + PaddingY));
            }

            private static SolidColorBrush CreateFrozenBrush(Color color)
            {
                SolidColorBrush brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
    }
}
