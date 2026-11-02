using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Geometry;

/// <summary>
/// Tests der Umrechnung zwischen ungedrehter Seite und gedrehter Anzeige.
/// </summary>
public sealed class PageTransformTests
{
    private const double Width = 200;
    private const double Height = 100;

    [Theory]
    [InlineData(PageRotation.None, 0, 0, 0, 0)]
    [InlineData(PageRotation.Clockwise90, 0, 0, 100, 0)]
    [InlineData(PageRotation.Clockwise90, 0, 100, 0, 0)]
    [InlineData(PageRotation.Rotate180, 0, 0, 200, 100)]
    [InlineData(PageRotation.Clockwise270, 0, 0, 0, 200)]
    [InlineData(PageRotation.Clockwise270, 200, 0, 0, 0)]
    public void Rotate_MapsCornersClockwise(PageRotation rotation, double x, double y, double expectedX, double expectedY) =>
        Assert.Equal(new PdfPoint(expectedX, expectedY), PageTransform.Rotate(new PdfPoint(x, y), rotation, Width, Height));

    [Theory]
    [InlineData(PageRotation.None)]
    [InlineData(PageRotation.Clockwise90)]
    [InlineData(PageRotation.Rotate180)]
    [InlineData(PageRotation.Clockwise270)]
    public void Unrotate_ReversesRotate(PageRotation rotation)
    {
        var point = new PdfPoint(37, 81);

        var back = PageTransform.Unrotate(PageTransform.Rotate(point, rotation, Width, Height), rotation, Width, Height);

        Assert.Equal(point, back);
    }

    [Theory]
    [InlineData(PageRotation.Clockwise90, 100, 200)]
    [InlineData(PageRotation.Rotate180, 200, 100)]
    public void RotatedSize_SwapsForQuarterTurns(PageRotation rotation, double width, double height) =>
        Assert.Equal((width, height), PageTransform.RotatedSize(Width, Height, rotation));

    [Fact]
    public void RotateRect_StaysInsideRotatedPage()
    {
        var rect = PageTransform.Rotate(new PdfRect(10, 20, 50, 30), PageRotation.Clockwise90, Width, Height);

        Assert.Equal(new PdfRect(50, 10, 30, 50), rect);
    }

    [Fact]
    public void RotateAnnotation_KeepsIdAndRotatesGeometry()
    {
        var ink = new InkAnnotation(Guid.NewGuid(), "#C93434", 3, [new(new PdfPoint(0, 0), 0.5f), new(new PdfPoint(10, 0), 0.7f)], 0.5, true);

        var rotated = (InkAnnotation)PageTransform.Rotate(ink, PageRotation.Rotate180, Width, Height);

        Assert.Equal(ink.Id, rotated.Id);
        Assert.Equal([new PdfPoint(200, 100), new PdfPoint(190, 100)], rotated.Points.Select(point => point.Position));
        Assert.Equal([0.5f, 0.7f], rotated.Points.Select(point => point.Pressure));
    }

    [Fact]
    public void RotateAnnotation_WithoutRotation_ReturnsSameInstance()
    {
        var comment = new CommentAnnotation(Guid.NewGuid(), "#4F46E5", new PdfRect(1, 2, 3, 4), "x");

        Assert.Same(comment, PageTransform.Rotate(comment, PageRotation.None, Width, Height));
    }
}
