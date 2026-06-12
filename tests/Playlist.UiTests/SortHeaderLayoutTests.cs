using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Playlist.UiTests;

public class SortHeaderLayoutTests
{
    [Theory]
    [InlineData(180, 0, 180)]
    [InlineData(0, 190, 190)]
    [InlineData(180, 190, 180)]
    [InlineData(0, 0, 0)]
    public void ComputeClipRight_UsesTighterOfGripperAndHeaderRight(
        double gripperLeft,
        double headerContentRight,
        double expected)
    {
        Assert.Equal(expected, PlaylistSortHeaderLayout.ComputeClipRight(gripperLeft, headerContentRight));
    }

    [Theory]
    [InlineData(10, 190, 180)]
    [InlineData(0, 0, 0)]
    [InlineData(25, 200, 175)]
    public void ComputePresenterWidth_SubtractsContentLeftFromClipRight(double contentLeft, double clipRight, double expected)
    {
        Assert.Equal(expected, PlaylistSortHeaderLayout.ComputePresenterWidth(contentLeft, clipRight));
    }

    [StaFact]
    public void GetGlyphUniformInset_ReadsGlyphMarginRight()
    {
        var glyph = new TextBlock { Margin = new Thickness(0, 2, 2, 2) };
        Assert.Equal(2, PlaylistSortHeaderLayout.GetGlyphUniformInset(glyph));
    }

    [StaTheory]
    [InlineData(8)]
    [InlineData(1)]
    public void GetParentRightInset_UsesTextBlockMargin(double marginRight)
    {
        var textBlock = new TextBlock { Margin = new Thickness(0, 0, marginRight, 0) };
        Assert.Equal(marginRight, PlaylistSortHeaderLayout.GetParentRightInset(textBlock));
    }

    [StaFact]
    public void GetParentRightInset_UsesBorderChrome()
    {
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 2, 0),
            Padding = new Thickness(0, 0, 6, 0)
        };

        Assert.Equal(8, PlaylistSortHeaderLayout.GetParentRightInset(border));
    }

    [StaFact]
    public void MeasurePresenterWidth_IsPositiveForThemedHeader()
    {
        EnsureApplication();

        var header = CreateMeasuredHeader();
        ContentPresenter presenter = FindFirstVisualChild<ContentPresenter>(header);
        Assert.NotNull(presenter);

        double width = PlaylistSortHeaderLayout.MeasurePresenterWidth(header, presenter, PlaylistSortHeaderLayout.HeadRightEdgeReserve);
        Assert.True(width > 0);
        Assert.True(width <= header.ActualWidth);
    }

    private static GridViewColumnHeader CreateMeasuredHeader()
    {
        var header = new GridViewColumnHeader
        {
            Width = 200,
            Height = 28,
            Padding = new Thickness(0),
            Content = new TextBlock { Text = "Name" }
        };

        if (Application.Current.TryFindResource(typeof(GridViewColumnHeader)) is Style themeStyle)
        {
            header.Style = themeStyle;
        }

        var host = new Grid { Width = 220, Height = 40 };
        host.Children.Add(header);
        PrepareLayout(host);
        return header;
    }

    private static T FindFirstVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
        {
            return null;
        }

        int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            T nested = FindFirstVisualChild<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void EnsureApplication()
    {
        if (Application.Current == null)
        {
            new Application();
        }
    }

    private static void PrepareLayout(FrameworkElement element)
    {
        var window = new Window
        {
            Width = 400,
            Height = 200,
            Content = element,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Visibility = Visibility.Hidden
        };

        window.Show();
        element.UpdateLayout();
        window.Close();
    }
}
