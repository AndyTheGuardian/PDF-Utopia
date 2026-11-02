using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Tests.Drawing;

/// <summary>
/// Tests des Objekt-Radierers (A27): Treffer auf Marker, Freihand und Rechteck, keine Kommentare,
/// ein Radierstrich als ein Rückgängig-Schritt.
/// </summary>
public sealed class EraserHitTestTests
{
    private static readonly InkAnnotation Stroke =
        new(Guid.NewGuid(), "#C93434", 2, [new(new PdfPoint(0, 0), 0.5f), new(new PdfPoint(100, 0), 0.5f)], 0.5, false);

    private static readonly RectangleAnnotation Rectangle = new(Guid.NewGuid(), "#2F6FDF", 2, new PdfRect(100, 100, 200, 100));
    private static readonly HighlightAnnotation Marker = new(Guid.NewGuid(), "#FDE047", new PdfRect(0, 300, 200, 14));
    private static readonly CommentAnnotation Comment = new(Guid.NewGuid(), "#4F46E5", new PdfRect(0, 400, 200, 40), "Text");

    [Theory]
    [InlineData(50, 5, true)]
    [InlineData(50, 20, false)]
    [InlineData(-8, 0, true)]
    [InlineData(-20, 0, false)]
    public void Stroke_HitWithinRadiusPlusHalfWidth(double x, double y, bool expected) =>
        Assert.Equal(expected, EraserHitTest.Hits(Stroke, new PdfPoint(x, y), radius: 10));

    [Theory]
    [InlineData(100, 150, true)]
    [InlineData(95, 150, true)]
    [InlineData(200, 150, false)]
    [InlineData(80, 150, false)]
    public void Rectangle_HitOnlyNearOutline(double x, double y, bool expected) =>
        Assert.Equal(expected, EraserHitTest.Hits(Rectangle, new PdfPoint(x, y), radius: 5));

    [Theory]
    [InlineData(100, 307, true)]
    [InlineData(100, 290, true)]
    [InlineData(100, 280, false)]
    public void Marker_HitInsideOrNearArea(double x, double y, bool expected) =>
        Assert.Equal(expected, EraserHitTest.Hits(Marker, new PdfPoint(x, y), radius: 10));

    [Fact]
    public void Comments_AreNeverErased()
    {
        Assert.False(EraserHitTest.IsErasable(Comment));
        Assert.Empty(EraserHitTest.HitsAlong([Comment], [new PdfPoint(100, 420)], 20));
    }

    [Fact]
    public void HitsAlong_ReturnsEveryTouchedShapeOnce()
    {
        PdfPoint[] path = [new(50, 0), new(100, 150), new(100, 160), new(500, 500)];

        var hits = EraserHitTest.HitsAlong([Stroke, Rectangle, Marker, Comment], path, 5);

        Assert.Equal([Stroke.Id, Rectangle.Id], hits.Select(hit => hit.Id));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(12, 12)]
    [InlineData(99, 40)]
    public void ClampRadius_LimitsRange(double requested, double expected) =>
        Assert.Equal(expected, EraserHitTest.ClampRadius(requested));

    [Fact]
    public void RemoveAnnotations_IsOneUndoStepAndRestoresOrder()
    {
        var document = new EditableDocument(1);
        foreach (var annotation in new Annotation[] { Stroke, Comment, Rectangle, Marker })
        {
            document.AddAnnotation(1, annotation);
        }

        var undoBefore = document.History.UndoCount;
        document.RemoveAnnotations(1, [Marker.Id, Stroke.Id]);

        Assert.Equal([Comment.Id, Rectangle.Id], document.Annotations.For(1).Select(annotation => annotation.Id));
        Assert.Equal(undoBefore + 1, document.History.UndoCount);

        document.History.Undo();
        Assert.Equal([Stroke.Id, Comment.Id, Rectangle.Id, Marker.Id], document.Annotations.For(1).Select(annotation => annotation.Id));

        document.History.Redo();
        Assert.Equal(2, document.Annotations.For(1).Count);
    }

    [Fact]
    public void RemoveAnnotations_WithoutIds_CreatesNoStep()
    {
        var document = new EditableDocument(1);

        document.RemoveAnnotations(1, []);

        Assert.False(document.History.CanUndo);
    }
}
