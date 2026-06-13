using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// WPF <see cref="GridViewRowPresenter"/> hard-codes <c>Margin="6,0,6,0"</c> on every cell
    /// (see dotnet/wpf#249). Zero the margin whenever cells are created or arranged.
    /// </summary>
    internal class PlaylistGridViewRowPresenter : GridViewRowPresenter
    {
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            ClearCellMargins();
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            ClearCellMargins();
            Size result = base.ArrangeOverride(arrangeSize);
            CenterCellsVertically(arrangeSize);
            return result;
        }

        private void CenterCellsVertically(Size arrangeSize)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(this);
            for (int index = 0; index < childCount; index++)
            {
                if (VisualTreeHelper.GetChild(this, index) is FrameworkElement cell
                    && cell.ActualHeight > 0.1
                    && arrangeSize.Height - cell.ActualHeight >= 2.0)
                {
                    double offsetY = Math.Round((arrangeSize.Height - cell.ActualHeight) * 0.5);
                    Point topLeft = cell.TranslatePoint(new Point(0, 0), this);
                    cell.Arrange(new Rect(Math.Round(topLeft.X), offsetY, cell.ActualWidth, cell.ActualHeight));
                }
            }
        }
        private void ClearCellMargins()
        {
            int childCount = VisualTreeHelper.GetChildrenCount(this);
            for (int index = 0; index < childCount; index++)
            {
                if (VisualTreeHelper.GetChild(this, index) is FrameworkElement cell)
                {
                    cell.Margin = new Thickness(0);
                    cell.VerticalAlignment = VerticalAlignment.Center;
                }
            }
        }
    }
}
