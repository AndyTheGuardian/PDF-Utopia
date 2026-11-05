using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Tests.Annotations;

/// <summary>
/// Tests der Anmerkungsübersicht (A45): Reihenfolge, Arten, Kurztexte, Filter, Umrisse.
/// </summary>
public sealed class AnnotationOverviewTests
{
    private static CommentAnnotation Comment(string text) => new(Guid.NewGuid(), "#4F46E5", new PdfRect(50, 100, 400, 60), text);

    private static HighlightAnnotation Highlight() => new(Guid.NewGuid(), "#FDE047", new PdfRect(70, 60, 300, 14));

    private static LineAnnotation Line(ArrowStyle arrow) => new(Guid.NewGuid(), "#2F6FDF", 2, new PdfPoint(10, 10), new PdfPoint(100, 10), arrow);

    [Fact]
    public void Build_OrdersByPage_CommentsFirstInFootnoteOrder()
    {
        var document = new EditableDocument(3);
        var rectangle = new RectangleAnnotation(Guid.NewGuid(), "#2F6FDF", 2, new PdfRect(1, 1, 50, 50));
        document.AddAnnotation(3, Highlight());
        document.AddAnnotation(1, rectangle);
        document.AddAnnotation(1, Comment("erster"));
        document.AddAnnotation(1, Comment("zweiter"));

        var entries = AnnotationOverview.Build(document);

        Assert.Equal([0, 0, 0, 2], entries.Select(entry => entry.PageIndex));
        Assert.Equal([1, 2, (int?)null, null], entries.Select(entry => entry.FootnoteNumber));
        Assert.Equal(["erster", "zweiter", "", ""], entries.Select(entry => entry.Text));
        Assert.Same(rectangle, entries[2].Annotation);
    }

    [Fact]
    public void Build_FollowsCurrentPageOrder()
    {
        var document = new EditableDocument(2);
        document.AddAnnotation(2, Comment("auf Seite 2"));
        document.MovePage(1, 0);

        var entry = Assert.Single(AnnotationOverview.Build(document));
        Assert.Equal(0, entry.PageIndex);
        Assert.Equal(2, entry.Page.Id);
    }

    [Fact]
    public void Build_SkipsDeletedPages()
    {
        var document = new EditableDocument(2);
        document.AddAnnotation(2, Comment("weg"));
        document.DeletePage(1);

        Assert.Empty(AnnotationOverview.Build(document));
    }

    [Fact]
    public void Build_LabelsAllKinds()
    {
        var document = new EditableDocument(1);
        document.AddAnnotation(1, new InkAnnotation(Guid.NewGuid(), "#000000", 2, [new(new PdfPoint(1, 1), 0.5f)], 0, false));
        document.AddAnnotation(1, new EllipseAnnotation(Guid.NewGuid(), "#000000", 2, new PdfRect(1, 1, 5, 5)));
        document.AddAnnotation(1, Line(ArrowStyle.None));
        document.AddAnnotation(1, Line(ArrowStyle.Closed));
        document.AddAnnotation(1, new TextAnnotation(Guid.NewGuid(), "#000000", new PdfPoint(1, 1), 100, 12, "Hallo\nWelt"));

        var entries = AnnotationOverview.Build(document);

        Assert.Equal([AnnotationLabel.Freehand, AnnotationLabel.Ellipse, AnnotationLabel.Line, AnnotationLabel.Arrow, AnnotationLabel.Text],
            entries.Select(entry => entry.Label));
        Assert.Equal([AnnotationCategory.Freehand, AnnotationCategory.Shape, AnnotationCategory.Shape, AnnotationCategory.Shape, AnnotationCategory.Text],
            entries.Select(entry => entry.Category));
        Assert.Equal("Hallo Welt", entries[^1].Text);
    }

    [Fact]
    public void Build_HighlightShowsTextUnderneath()
    {
        var document = new EditableDocument(1);
        var highlight = Highlight();
        document.AddAnnotation(1, highlight);

        var entry = Assert.Single(AnnotationOverview.Build(document, (page, area) => area == highlight.Bounds ? "markierter  Text" : "?"));

        Assert.Equal("markierter Text", entry.Text);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(AnnotationCategory.Comment, 1)]
    [InlineData(AnnotationCategory.Shape, 2)]
    [InlineData(AnnotationCategory.Text, 0)]
    public void Matches_FiltersByCategory(AnnotationCategory? filter, int expected)
    {
        var document = new EditableDocument(1);
        document.AddAnnotation(1, Comment("k"));
        document.AddAnnotation(1, Line(ArrowStyle.Open));
        document.AddAnnotation(1, Line(ArrowStyle.None));

        Assert.Equal(expected, AnnotationOverview.Build(document).Count(entry => AnnotationOverview.Matches(entry, filter)));
    }

    [Theory]
    [InlineData("kurz", 10, "kurz")]
    [InlineData("eins\n zwei\tdrei", 50, "eins zwei drei")]
    [InlineData("abcdefghijklmnop", 8, "abcdefg…")]
    public void Shorten(string text, int max, string expected) => Assert.Equal(expected, AnnotationOverview.Shorten(text, max));

    [Fact]
    public void Bounds_IncludeStrokeWidth()
    {
        var ink = new InkAnnotation(Guid.NewGuid(), "#000000", 4, [new(new PdfPoint(10, 20), 0.5f), new(new PdfPoint(30, 50), 0.5f)], 0, false);
        Assert.Equal(new PdfRect(8, 18, 24, 34), AnnotationBounds.Of(ink));
        Assert.Equal(new PdfRect(9, 9, 92, 2), AnnotationBounds.Of(Line(ArrowStyle.None)));
    }
}
