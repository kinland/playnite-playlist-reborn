using Xunit;

namespace Playlist.UnitTests;

public class ColumnReorderDropLayoutTests
{
    [Fact]
    public void GetDropIndex_UsesHeaderMidpoints()
    {
        var bounds = new (double Left, double Right)[]
        {
            (0, 100),
            (100, 200),
            (200, 300),
        };

        Assert.Equal(0, PlaylistColumnReorderDropLayout.GetDropIndex(bounds, 10));
        Assert.Equal(1, PlaylistColumnReorderDropLayout.GetDropIndex(bounds, 120));
        Assert.Equal(2, PlaylistColumnReorderDropLayout.GetDropIndex(bounds, 220));
        Assert.Equal(3, PlaylistColumnReorderDropLayout.GetDropIndex(bounds, 290));
    }

    [Fact]
    public void GetDropLineX_MapsIndexToBoundary()
    {
        var bounds = new (double Left, double Right)[]
        {
            (0, 100),
            (100, 200),
            (200, 300),
        };

        Assert.Equal(0, PlaylistColumnReorderDropLayout.GetDropLineX(bounds, 0));
        Assert.Equal(100, PlaylistColumnReorderDropLayout.GetDropLineX(bounds, 1));
        Assert.Equal(200, PlaylistColumnReorderDropLayout.GetDropLineX(bounds, 2));
        Assert.Equal(300, PlaylistColumnReorderDropLayout.GetDropLineX(bounds, 3));
    }
}
