using PdfUtopia.Core.Geometry;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Tests der Absatzerkennung für den Kommentar-Vorschlag (F21).
/// </summary>
public sealed class TextLayoutTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>Zeichen einer Zeile: 10 Zeichen à 6 Punkte, Höhe 10.</summary>
    private static IEnumerable<PdfRect> Line(double x, double y, int count = 10) =>
        Enumerable.Range(0, count).Select(i => new PdfRect(x + i * 6, y, 5, 10));

    [Fact]
    public void Lines_GroupCharactersOnSameHeight()
    {
        var lines = TextLayout.Lines(Line(50, 100).Concat(Line(50, 114)));

        Assert.Equal(2, lines.Count);
        Assert.Equal(new PdfRect(50, 100, 59, 10), lines[0]);
    }

    [Fact]
    public void Paragraphs_CloseLinesFormOneParagraph()
    {
        var paragraphs = TextLayout.Paragraphs(Line(50, 100).Concat(Line(50, 113)).Concat(Line(50, 126)));

        var paragraph = Assert.Single(paragraphs);
        var p = TextLayout.Padding;
        Assert.Equal(new PdfRect(50 - p, 100 - p, 59 + 2 * p, 36 + 2 * p), paragraph);
    }

    [Fact]
    public void Paragraphs_LargeGapStartsNewParagraph()
    {
        var paragraphs = TextLayout.Paragraphs(Line(50, 100).Concat(Line(50, 113)).Concat(Line(50, 160)));

        Assert.Equal(2, paragraphs.Count);
    }

    [Fact]
    public void Paragraphs_DifferentColumnStartsNewParagraph()
    {
        var paragraphs = TextLayout.Paragraphs(Line(50, 100).Concat(Line(300, 113)));

        Assert.Equal(2, paragraphs.Count);
    }

    [Fact]
    public void Paragraphs_NoText_GivesNoParagraphs() => Assert.Empty(TextLayout.Paragraphs([]));

    [Fact]
    public void TextBlocks_TestFile_FindsHeadingAndFourLines()
    {
        using var source = PdfSource.Open(TestPdfFactory.Create(_temp.File("testdatei.pdf")));

        var blocks = source.TextBlocks(1);

        Assert.Equal(5, blocks.Count);
        // Überschrift „Seite 1“ bei Grundlinie 770 (Benutzerraum) → Anzeige etwa 842 − 770 = 72 von oben
        // Grenzen ohne Rand, der Abstand des Kommentarrahmens (TextLayout.Padding) kommt hinzu
        Assert.InRange(blocks[0].Bottom, 67 + TextLayout.Padding, 77 + TextLayout.Padding);
        Assert.InRange(blocks[0].X, 63 - TextLayout.Padding, 75 - TextLayout.Padding);
        // Erste Textzeile bei Grundlinie 720 → Anzeige etwa 122
        Assert.InRange(blocks[1].Bottom, 117 + TextLayout.Padding, 127 + TextLayout.Padding);
    }

    [Fact]
    public void TextBlocks_PageRotatedInFile_UsesDisplayCoordinates()
    {
        using var source = PdfSource.Open(TestPdfFactory.Create(_temp.File("testdatei.pdf")));

        var blocks = source.TextBlocks(TestPdfFactory.RotatedPage);

        Assert.Equal(5, blocks.Count);
        Assert.All(blocks, block => Assert.True(block.Height > block.Width, $"Gedrehte Zeilen laufen senkrecht: {block}"));
        Assert.All(blocks, block => Assert.InRange(block.Right, 0, TestPdfFactory.Height));
    }
}
