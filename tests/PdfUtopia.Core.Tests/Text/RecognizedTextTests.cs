using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Text;

namespace PdfUtopia.Core.Tests.Text;

/// <summary>
/// Erkannter Text (A53): Auswertung der Tesseract-Ausgabe, Suche, Absätze, Text unter Bereichen, Schwärzung,
/// Rückgängig.
/// </summary>
public sealed class RecognizedTextTests
{
    private const string Tsv =
        "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
        "1\t1\t0\t0\t0\t0\t0\t0\t2480\t3508\t-1\t\n" +
        "5\t1\t1\t1\t1\t1\t300\t400\t200\t50\t96.5\tMax\n" +
        "5\t1\t1\t1\t1\t2\t520\t400\t500\t50\t91\tMustermann,\n" +
        "5\t1\t1\t1\t2\t1\t300\t480\t300\t50\t12\t~~\n" +
        "5\t1\t2\t1\t1\t1\t300\t900\t400\t50\t88\tZweiter\n";

    private static RecognizedPage Parse(PageRotation rotation = PageRotation.None) =>
        TesseractTsv.Parse(Tsv, rect => new PdfRect(rect.X / 10, rect.Y / 10, rect.Width / 10, rect.Height / 10), rotation);

    [Fact]
    public void Parse_ReadsWords_SkipsUnsureOnes_AndNumbersParagraphsAndLines()
    {
        var page = Parse();

        Assert.Equal(["Max", "Mustermann,", "Zweiter"], page.Words.Select(word => word.Text));
        Assert.Equal(new PdfRect(30, 40, 20, 5), page.Words[0].Bounds);
        Assert.Equal([0, 0, 1], page.Words.Select(word => word.Paragraph));
        Assert.Equal([0, 0, 1], page.Words.Select(word => word.Line));
        Assert.Equal("Max Mustermann,\nZweiter", page.Text);
    }

    [Fact]
    public void Find_CoversWordsAndShortensPartialWords()
    {
        var page = Parse();

        Assert.Equal([new PdfRect(30, 40, 20, 5), new PdfRect(52, 40, 50 * 10 / 11.0, 5)], page.Find("max mustermann"));
        var part = Assert.Single(page.Find("muster"));
        Assert.Equal(52, part.X, 6);
        Assert.Equal(50 * 6 / 11.0, part.Width, 6);
        Assert.Empty(page.Find("fehlt"));
    }

    [Fact]
    public void Find_PartialWordOnTurnedText_ShortensAlongTheText()
    {
        var page = new RecognizedPage([new RecognizedWord("abcd", new PdfRect(10, 100, 10, 40), 0, 0)], PageRotation.Clockwise90);

        // Bei 90° läuft der Text auf der ungedrehten Seite von unten nach oben
        Assert.Equal(new PdfRect(10, 120, 10, 20), Assert.Single(page.Find("ab")));
    }

    [Fact]
    public void ParagraphsAndTextIn_UseWordPositions()
    {
        var page = Parse();

        Assert.Equal([new PdfRect(30, 40, 72, 5), new PdfRect(30, 90, 40, 5)], page.Paragraphs);
        Assert.Equal("Max Mustermann,", page.TextIn(new PdfRect(0, 30, 200, 20)));
    }

    [Fact]
    public void Without_DropsWordsTouchingAnArea()
    {
        var page = Parse().Without([new PdfRect(60, 41, 5, 2)]);

        Assert.Equal(["Max", "Zweiter"], page.Words.Select(word => word.Text));
    }

    [Fact]
    public void SetRecognizedText_IsOneUndoStep()
    {
        var document = new EditableDocument(2);
        document.SetRecognizedText(new Dictionary<int, RecognizedPage> { [1] = Parse(), [2] = Parse() });

        Assert.Equal(2, document.RecognizedText.Count);
        Assert.True(document.IsModified);
        document.History.Undo();
        Assert.Empty(document.RecognizedText);
    }
}
