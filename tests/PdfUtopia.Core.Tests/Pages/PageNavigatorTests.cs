using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Pages;

/// <summary>
/// Tests des Blätterns in der Seitenansicht mit Mausrad und Tasten (A38).
/// </summary>
public sealed class PageNavigatorTests
{
    private static readonly ScrollPosition Fits = new(AtTop: true, AtBottom: true);
    private static readonly ScrollPosition Middle = new(AtTop: false, AtBottom: false);
    private static readonly ScrollPosition Top = new(AtTop: true, AtBottom: false);
    private static readonly ScrollPosition Bottom = new(AtTop: false, AtBottom: true);

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    [Fact]
    public void Wheel_PageFits_FlipsForwardAndBackward()
    {
        var navigator = new PageNavigator();
        Assert.Equal(new NavigationStep(NavigationKind.GoTo, 3), navigator.Wheel(-1, Fits, 2, 5, Ms(0)));
        Assert.Equal(new NavigationStep(NavigationKind.GoTo, 1, LandAtBottom: true), navigator.Wheel(1, Fits, 2, 5, Ms(1000)));
    }

    [Fact]
    public void Wheel_InsidePage_Scrolls()
    {
        var navigator = new PageNavigator();
        Assert.Equal(NavigationStep.Scroll, navigator.Wheel(-1, Middle, 2, 5, Ms(0)));
        Assert.Equal(NavigationStep.Scroll, navigator.Wheel(1, Middle, 2, 5, Ms(0)));
    }

    [Fact]
    public void Wheel_ScrollsTowardsOtherEdge()
    {
        var navigator = new PageNavigator();
        Assert.Equal(NavigationStep.Scroll, navigator.Wheel(-1, Top, 2, 5, Ms(0)));
        Assert.Equal(NavigationStep.Scroll, navigator.Wheel(1, Bottom, 2, 5, Ms(0)));
    }

    [Fact]
    public void Wheel_FirstAndLastPage_DoNotFlip()
    {
        var navigator = new PageNavigator();
        Assert.Equal(NavigationStep.Scroll, navigator.Wheel(1, Fits, 0, 5, Ms(0)));
        Assert.Equal(NavigationStep.Scroll, navigator.Wheel(-1, Fits, 4, 5, Ms(0)));
    }

    [Fact]
    public void Wheel_IsIgnored_ShortlyAfterFlip()
    {
        var navigator = new PageNavigator();
        Assert.Equal(NavigationKind.GoTo, navigator.Wheel(-1, Fits, 0, 5, Ms(1000)).Kind);
        Assert.Equal(NavigationStep.Ignore, navigator.Wheel(-1, Fits, 1, 5, Ms(1100)));
        Assert.Equal(NavigationStep.Ignore, navigator.Wheel(-1, Middle, 1, 5, Ms(1299)));
        Assert.Equal(new NavigationStep(NavigationKind.GoTo, 2), navigator.Wheel(-1, Fits, 1, 5, Ms(1300)));
    }

    [Fact]
    public void Wheel_SmallTouchpadDeltas_AccumulateToOneNotch()
    {
        var navigator = new PageNavigator();
        Assert.Equal(NavigationStep.Ignore, navigator.Wheel(-0.4, Bottom, 0, 5, Ms(0)));
        Assert.Equal(NavigationStep.Ignore, navigator.Wheel(-0.4, Bottom, 0, 5, Ms(10)));
        Assert.Equal(new NavigationStep(NavigationKind.GoTo, 1), navigator.Wheel(-0.4, Bottom, 0, 5, Ms(20)));
    }

    [Fact]
    public void Wheel_DirectionChange_ResetsAccumulation()
    {
        var navigator = new PageNavigator();
        Assert.Equal(NavigationStep.Ignore, navigator.Wheel(-0.6, Fits, 2, 5, Ms(0)));
        Assert.Equal(NavigationStep.Ignore, navigator.Wheel(0.6, Fits, 2, 5, Ms(10)));
        Assert.Equal(NavigationStep.Ignore, navigator.Wheel(-0.6, Fits, 2, 5, Ms(20)));
    }

    [Fact]
    public void Wheel_Horizontal_Scrolls() =>
        Assert.Equal(NavigationStep.Scroll, new PageNavigator().Wheel(0, Fits, 2, 5, Ms(0)));

    [Theory]
    [InlineData(NavigationKey.Left, 1)]
    [InlineData(NavigationKey.PageUp, 1)]
    [InlineData(NavigationKey.Right, 3)]
    [InlineData(NavigationKey.PageDown, 3)]
    [InlineData(NavigationKey.Home, 0)]
    [InlineData(NavigationKey.End, 4)]
    public void Key_FlipsDirectly_EvenInsidePage(NavigationKey key, int target) =>
        Assert.Equal(new NavigationStep(NavigationKind.GoTo, target), PageNavigator.Key(key, Middle, 2, 5));

    [Theory]
    [InlineData(NavigationKey.Up)]
    [InlineData(NavigationKey.Down)]
    public void Key_UpDown_ScrollInsidePage(NavigationKey key) =>
        Assert.Equal(NavigationStep.Scroll, PageNavigator.Key(key, Middle, 2, 5));

    [Fact]
    public void Key_UpAtTop_GoesToBottomOfPreviousPage() =>
        Assert.Equal(new NavigationStep(NavigationKind.GoTo, 1, LandAtBottom: true), PageNavigator.Key(NavigationKey.Up, Top, 2, 5));

    [Fact]
    public void Key_DownAtBottom_GoesToTopOfNextPage() =>
        Assert.Equal(new NavigationStep(NavigationKind.GoTo, 3), PageNavigator.Key(NavigationKey.Down, Bottom, 2, 5));

    [Theory]
    [InlineData(NavigationKey.Left, 0)]
    [InlineData(NavigationKey.Up, 0)]
    [InlineData(NavigationKey.Right, 4)]
    [InlineData(NavigationKey.Down, 4)]
    public void Key_BeyondFirstOrLastPage_IsIgnored(NavigationKey key, int current) =>
        Assert.Equal(NavigationStep.Ignore, PageNavigator.Key(key, Fits, current, 5));

    [Theory]
    [InlineData(0, 1000, 400, true, false)]
    [InlineData(300, 1000, 400, false, false)]
    [InlineData(600, 1000, 400, false, true)]
    [InlineData(599.5, 1000, 400, false, true)]
    [InlineData(0, 300, 400, true, true)]
    public void ScrollPosition_From(double offset, double extent, double viewport, bool atTop, bool atBottom) =>
        Assert.Equal(new ScrollPosition(atTop, atBottom), ScrollPosition.From(offset, extent, viewport));
}
