using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Pdf.Export;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Tests der Bausteine des Speicherns: Koordinatenumrechnung, Fußnotensatz und Schriftkodierung.
/// </summary>
public sealed class ExportBuildingBlocksTests
{
    // Seite 200 × 100 (Benutzerraum), Ursprung bei (10, 20)
    private static PageGeometry Geometry(int rotation) => new(10, 20, 210, 120, rotation);

    [Theory]
    [InlineData(0, 0, 0, 10, 120)]
    [InlineData(0, 200, 100, 210, 20)]
    [InlineData(90, 0, 0, 10, 20)]
    [InlineData(90, 100, 200, 210, 120)]
    [InlineData(180, 0, 0, 210, 20)]
    [InlineData(180, 200, 100, 10, 120)]
    [InlineData(270, 0, 0, 210, 120)]
    [InlineData(270, 100, 200, 10, 20)]
    public void ToUser_MapsDisplayCornersForEachRotation(int rotation, double x, double y, double expectedX, double expectedY) =>
        Assert.Equal(new PdfPoint(expectedX, expectedY), Geometry(rotation).ToUser(new PdfPoint(x, y)));

    [Theory]
    [InlineData(0, 200, 100)]
    [InlineData(90, 100, 200)]
    [InlineData(180, 200, 100)]
    [InlineData(270, 100, 200)]
    public void DisplaySize_SwapsForQuarterTurns(int rotation, double width, double height)
    {
        var geometry = Geometry(rotation);

        Assert.Equal((width, height), (geometry.DisplayWidth, geometry.DisplayHeight));
    }

    [Theory]
    [InlineData(0, 10, -10, 210, 120)]
    [InlineData(90, 10, 20, 240, 120)]
    [InlineData(180, 10, 20, 210, 150)]
    [InlineData(270, -20, 20, 210, 120)]
    public void ExtendedBox_GrowsAtDisplayBottom(int rotation, double left, double bottom, double right, double top)
    {
        var extension = rotation is 0 ? 30 : 30;

        Assert.Equal((left, bottom, right, top), Geometry(rotation).ExtendedBox(extension));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void ExtendedArea_ContainsMappedFootnotePoints(int rotation)
    {
        var geometry = Geometry(rotation);
        var (left, bottom, right, top) = geometry.ExtendedBox(30);

        var point = geometry.ToUser(new PdfPoint(geometry.DisplayWidth / 2, geometry.DisplayHeight + 15));

        Assert.InRange(point.X, left, right);
        Assert.InRange(point.Y, bottom, top);
    }

    [Fact]
    public void QuadPoints_OrderIsTopLeftTopRightBottomLeftBottomRight() =>
        Assert.Equal([10d, 120, 50, 120, 10, 100, 50, 100], Geometry(0).QuadPoints(new PdfRect(0, 0, 40, 20)));

    [Fact]
    public void Footnotes_NoComments_NoExtension() =>
        Assert.Equal(0, FootnoteLayout.Layout([], 595, 842).Extension);

    [Fact]
    public void Footnotes_AreStackedBelowPage()
    {
        CommentAnnotation[] comments = [Comment("Eins"), Comment("Zwei")];

        var block = FootnoteLayout.Layout(comments, 595, 842);

        Assert.Equal([1, 2], block.Notes.Select(note => note.Number));
        Assert.True(block.Heading.Y >= 842);
        Assert.True(block.Notes[0].Bounds.Y >= block.Heading.Bottom);
        Assert.True(block.Notes[1].Bounds.Y >= block.Notes[0].Bounds.Bottom);
        Assert.True(842 + block.Extension >= block.Notes[1].Bounds.Bottom);
    }

    [Fact]
    public void Wrap_BreaksAtWordsAndKeepsLineBreaks()
    {
        var lines = FootnoteLayout.Wrap("Ein kurzer Satz\nzweite Zeile", 500);

        Assert.Equal(["Ein kurzer Satz", "zweite Zeile"], lines);
    }

    [Fact]
    public void Wrap_LongText_FitsWidth()
    {
        var lines = FootnoteLayout.Wrap(string.Join(' ', Enumerable.Repeat("Wort", 60)), 200);

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(StandardFont.Width(line, FootnoteLayout.FontSize) <= 200));
    }

    [Fact]
    public void Wrap_OverlongWord_IsSplit()
    {
        var lines = FootnoteLayout.Wrap(new string('W', 200), 100);

        Assert.True(lines.Count > 1);
        Assert.Equal(200, lines.Sum(line => line.Length));
    }

    [Fact]
    public void Wrap_EmptyText_GivesOneLine() => Assert.Equal([""], FootnoteLayout.Wrap("", 100));

    [Fact]
    public void StandardFont_EncodesGermanAndEscapes()
    {
        Assert.Equal(@"(Gr\366\337e \(\344\) \200 \226 ?)", StandardFont.ToPdfString("Größe (ä) € – 😀"[..^1]));
        Assert.True(StandardFont.CanEncode('ß'));
        Assert.False(StandardFont.CanEncode('中'));
    }

    [Fact]
    public void StandardFont_WidthUsesMetrics() =>
        Assert.Equal(0.556 * 10 * 2, StandardFont.Width("ab", 10), precision: 6);

    private static CommentAnnotation Comment(string text) => new(Guid.NewGuid(), "#4F46E5", new PdfRect(50, 100, 400, 60), text);
}
