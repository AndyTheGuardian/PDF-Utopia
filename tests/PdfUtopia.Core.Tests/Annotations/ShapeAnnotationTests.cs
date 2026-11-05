using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Annotations;

/// <summary>
/// Tests der Formen Linie, Pfeil und Ellipse (A44).
/// </summary>
public sealed class ShapeAnnotationTests
{
    private static LineAnnotation Line(bool arrow = false, double width = 2, ArrowStyle? style = null) =>
        new(Guid.NewGuid(), "#2F6FDF", width, new PdfPoint(100, 100), new PdfPoint(300, 100), style ?? (arrow ? ArrowStyle.Open : ArrowStyle.None));

    private static EllipseAnnotation Ellipse() => new(Guid.NewGuid(), "#2F6FDF", 2, new PdfRect(100, 100, 200, 100));

    [Fact]
    public void ArrowHead_PointsBackFromEnd_OnBothSides()
    {
        var (_, left, right) = Assert.Single(Line(arrow: true).Heads);

        Assert.True(left.X < 300 && right.X < 300);
        Assert.True((left.Y - 100) * (right.Y - 100) < 0, "Schenkel liegen auf beiden Seiten der Linie");
        Assert.Equal(Line().ArrowLength, left.DistanceTo(new PdfPoint(300, 100)), 6);
        Assert.Equal(100 - left.Y, right.Y - 100, 6);
    }

    [Fact]
    public void ArrowHead_GrowsWithWidth() => Assert.True(Line(width: 8).ArrowLength > Line(width: 1).ArrowLength);

    [Fact]
    public void Bounds_IncludeArrowHead()
    {
        Assert.Equal(new PdfRect(100, 100, 200, 0), Line().Bounds);
        var arrow = Line(arrow: true);
        Assert.True(arrow.Bounds.Height > 0);
        Assert.Equal(300, arrow.Bounds.Right, 6);
    }

    [Theory]
    [InlineData(100, 3, 100, 0)]
    [InlineData(100, 100, 70.711, 70.711)]
    [InlineData(5, 100, 0, 100)]
    [InlineData(-100, 30, -96.593, 25.882)]
    public void SnapAngle_RoundsTo15DegreesAndKeepsLength(double dx, double dy, double expectedX, double expectedY)
    {
        var start = new PdfPoint(10, 20);
        var snapped = ShapeConstraint.SnapAngle(start, new PdfPoint(10 + dx, 20 + dy));

        var length = Math.Sqrt(dx * dx + dy * dy);
        Assert.Equal(expectedX / Math.Sqrt(expectedX * expectedX + expectedY * expectedY) * length, snapped.X - 10, 2);
        Assert.Equal(expectedY / Math.Sqrt(expectedX * expectedX + expectedY * expectedY) * length, snapped.Y - 20, 2);
    }

    [Theory]
    [InlineData(50, 20, 50, 50)]
    [InlineData(-20, 60, -60, 60)]
    [InlineData(-30, -10, -30, -30)]
    public void Square_UsesLargerSideAndDragDirection(double dx, double dy, double expectedX, double expectedY)
    {
        var square = ShapeConstraint.Square(new PdfPoint(0, 0), new PdfPoint(dx, dy));
        Assert.Equal(new PdfPoint(expectedX, expectedY), square);
    }

    [Fact]
    public void Eraser_HitsLineAndArrowHead()
    {
        var arrow = Line(arrow: true);
        var (_, left, _) = Assert.Single(arrow.Heads);

        Assert.True(EraserHitTest.Hits(arrow, new PdfPoint(200, 103), 2));
        Assert.False(EraserHitTest.Hits(arrow, new PdfPoint(200, 120), 2));
        Assert.True(EraserHitTest.Hits(arrow, left, 1));
        Assert.False(EraserHitTest.Hits(Line(), left, 1));
    }

    [Fact]
    public void ClosedArrow_LineStopsAtBaseOfHead()
    {
        var closed = Line(style: ArrowStyle.Closed);
        var (_, left, right) = Assert.Single(closed.Heads);

        Assert.Equal((left.X + right.X) / 2, closed.StrokeEnd.X, 6);
        Assert.True(closed.StrokeEnd.X < closed.End.X);
        Assert.Equal(closed.End, Line(arrow: true).StrokeEnd);
    }

    [Fact]
    public void Eraser_HitsInsideClosedHeadOnly()
    {
        var (_, left, right) = Assert.Single(Line(style: ArrowStyle.Closed).Heads);
        var inside = new PdfPoint((left.X + right.X + 300) / 3, (left.Y + right.Y + 100) / 3 + 1);

        Assert.True(EraserHitTest.Hits(Line(style: ArrowStyle.Closed, width: 1), inside, 0.1));
        Assert.False(EraserHitTest.Hits(Line(arrow: true, width: 1), inside, 0.1));
    }

    [Theory]
    [InlineData(ArrowEnds.End, false, true)]
    [InlineData(ArrowEnds.Start, true, false)]
    [InlineData(ArrowEnds.Both, true, true)]
    public void Ends_PlaceHeadsAtStartEndOrBoth(ArrowEnds ends, bool atStart, bool atEnd)
    {
        var line = Line(style: ArrowStyle.Closed) with { Ends = ends };

        Assert.Equal(atStart, line.Heads.Any(head => head.Tip == line.Start));
        Assert.Equal(atEnd, line.Heads.Any(head => head.Tip == line.End));
        Assert.Equal(atStart, line.StrokeStart != line.Start);
        Assert.Equal(atEnd, line.StrokeEnd != line.End);
        // Spitze am Anfang zeigt nach außen (links), ihre Schenkel liegen rechts davon
        Assert.All(line.Heads.Where(head => head.Tip == line.Start), head => Assert.True(head.Left.X > line.Start.X));
    }

    [Fact]
    public void Ends_WithoutArrowStyle_HasNoHeads() =>
        Assert.Empty((Line() with { Ends = ArrowEnds.Both }).Heads);

    [Fact]
    public void Eraser_HitsEllipseOutlineOnly()
    {
        var ellipse = Ellipse();

        Assert.True(EraserHitTest.IsErasable(ellipse));
        Assert.True(EraserHitTest.Hits(ellipse, new PdfPoint(300, 150), 2));
        Assert.True(EraserHitTest.Hits(ellipse, new PdfPoint(200, 101), 2));
        Assert.False(EraserHitTest.Hits(ellipse, new PdfPoint(200, 150), 2));
        Assert.False(EraserHitTest.Hits(ellipse, new PdfPoint(110, 110), 2));
    }

    [Fact]
    public void Rotate_MovesPointsAndBounds()
    {
        var line = (LineAnnotation)PageTransform.Rotate(Line(), PageRotation.Clockwise90, 600, 800);
        Assert.Equal(PageTransform.Rotate(new PdfPoint(100, 100), PageRotation.Clockwise90, 600, 800), line.Start);
        Assert.Equal(PageTransform.Rotate(new PdfPoint(300, 100), PageRotation.Clockwise90, 600, 800), line.End);

        var ellipse = (EllipseAnnotation)PageTransform.Rotate(Ellipse(), PageRotation.Rotate180, 600, 800);
        Assert.Equal(PageTransform.Rotate(Ellipse().Bounds, PageRotation.Rotate180, 600, 800), ellipse.Bounds);
    }
}
