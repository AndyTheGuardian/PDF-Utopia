using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Tests.Geometry;

/// <summary>
/// Tests der Anpassung an den Anzeigebereich: volle Höhe, volle Breite, ganze Seite.
/// </summary>
public sealed class ZoomFitTests
{
    // A4 hochkant in einem breiten Bereich 1200 × 800
    [Theory]
    [InlineData(FitMode.Height, 0.95)]
    [InlineData(FitMode.Width, 2.017)]
    [InlineData(FitMode.Page, 0.95)]
    public void Calculate_A4InWideViewport(FitMode mode, double expected) =>
        Assert.Equal(expected, ZoomFit.Calculate(mode, 1200, 800, 595, 842));

    [Fact]
    public void Page_UsesSmallerFactor_ForLandscapePage() =>
        Assert.Equal(1132 / 842.0, ZoomFit.Calculate(FitMode.Page, 1132, 1000, 842, 595)!.Value, precision: 3);

    [Theory]
    [InlineData(10, 10, 0.3)]
    [InlineData(100000, 100000, 4)]
    public void Calculate_IsClamped(double width, double height, double expected) =>
        Assert.Equal(expected, ZoomFit.Calculate(FitMode.Page, width, height, 595, 842));

    [Theory]
    [InlineData(FitMode.None, 800, 600)]
    [InlineData(FitMode.Page, 0, 600)]
    [InlineData(FitMode.Width, 800, -1)]
    public void Calculate_NoneOrInvalid_ReturnsNull(FitMode mode, double width, double height) =>
        Assert.Null(ZoomFit.Calculate(mode, width, height, 595, 842));
}
