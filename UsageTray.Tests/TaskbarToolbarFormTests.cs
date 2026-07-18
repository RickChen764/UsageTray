using System.Drawing;
using UsageTray;
using Xunit;

namespace UsageTray.Tests;

public sealed class TaskbarToolbarFormTests
{
    [Fact]
    public void CalculateToolTipBounds_BottomTaskbar_PlacesCardInsideWorkArea()
    {
        var result = TaskbarToolbarForm.CalculateToolTipBounds(
            new Size(630, 528),
            new Rectangle(2100, 1408, 160, 24),
            new Rectangle(0, 0, 2560, 1400),
            new Rectangle(0, 0, 2560, 1440),
            new Rectangle(0, 1400, 2560, 40),
            9);

        Assert.Equal(1391, result.Bottom);
        Assert.InRange(result.Left, 9, 1921);
    }

    [Fact]
    public void CalculateToolTipBounds_LeftTaskbar_HandlesNegativeMonitorCoordinates()
    {
        var result = TaskbarToolbarForm.CalculateToolTipBounds(
            new Size(420, 352),
            new Rectangle(-1900, 800, 48, 120),
            new Rectangle(-1872, 0, 1872, 1080),
            new Rectangle(-1920, 0, 1920, 1080),
            new Rectangle(-1920, 0, 48, 1080),
            6);

        Assert.Equal(-1866, result.Left);
        Assert.InRange(result.Top, 6, 722);
        Assert.True(result.Right <= 0);
    }
}
