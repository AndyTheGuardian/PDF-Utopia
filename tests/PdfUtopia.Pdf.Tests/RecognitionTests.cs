using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Text;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Ocr;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Texterkennung (A53): unsichtbare Textebene im gespeicherten PDF, Schwärzungen entfernen Wörter, Auswertung der
/// Tesseract-Ausgabe und – falls Tesseract installiert ist – eine echte Erkennung.
/// </summary>
public sealed class RecognitionTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public RecognitionTests() => _file = TestPdfFactory.Create(_temp.File("testdatei.pdf"));

    public void Dispose() => _temp.Dispose();

    private static RecognizedPage Words(PageRotation orientation = PageRotation.None, params (string Text, PdfRect Bounds)[] words) =>
        new(words.Select((word, i) => new RecognizedWord(word.Text, word.Bounds, 0, 0)).ToList(), orientation);

    [Fact]
    public void Save_RecognizedText_IsAnInvisibleTextLayerAtTheWordPosition()
    {
        var word = new PdfRect(100, 600, 120, 16);
        var saved = Save(document => document.SetRecognizedText(new Dictionary<int, RecognizedPage>
        {
            [document.Pages[1].Id] = Words(PageRotation.None, ("Erkannt", word), ("Zweites", new PdfRect(230, 600, 110, 16))),
        }));

        using var source = PdfSource.Open(saved);
        Assert.Contains("Erkannt", source.PageText(2));
        var hit = Assert.Single(source.FindText(2, "Erkannt"));
        Assert.InRange(hit.X + hit.Width / 2, word.X + word.Width / 2 - 6, word.X + word.Width / 2 + 6);
        Assert.InRange(hit.Y + hit.Height / 2, word.Y, word.Bottom);
        // Unsichtbar: die Seite sieht aus wie vorher
        using var original = PdfSource.Open(_file);
        Assert.Equal(original.Render(2, scale: 1).PixelAt(160, 608), source.Render(2, scale: 1).PixelAt(160, 608));
    }

    [Fact]
    public void Save_RecognizedTextOnRotatedPage_LandsOnTheTurnedPage()
    {
        // Erkannt auf der um 90° gedrehten Anzeige: Der Text läuft dort waagerecht, auf der ungedrehten Seite senkrecht
        var word = TesseractEngine.ToPage(new PdfRect(300, 200, 400, 60), 300 / 72.0, PageRotation.Clockwise90, TestPdfFactory.Width, TestPdfFactory.Height);
        var saved = Save(document =>
        {
            document.RotatePage(1);
            document.SetRecognizedText(new Dictionary<int, RecognizedPage> { [document.Pages[1].Id] = Words(PageRotation.Clockwise90, ("Quer", word)) });
        });

        using var source = PdfSource.Open(saved);
        // Buchstaben verschiedener Höhe kann die Suche in Teilrechtecke zerlegen; maßgeblich ist ihre Hülle
        var hits = source.FindText(2, "Quer");
        Assert.NotEmpty(hits);
        var hit = PdfRect.FromCorners(new PdfPoint(hits.Min(h => h.X), hits.Min(h => h.Y)), new PdfPoint(hits.Max(h => h.Right), hits.Max(h => h.Bottom)));
        // In der gespeicherten (quer gedrehten) Anzeige liegt das Wort bei (300…700, 200…260) / 300 dpi * 72
        Assert.InRange(hit.X + hit.Width / 2, 300 * 72 / 300.0 - 2, 700 * 72 / 300.0 + 2);
        Assert.InRange(hit.Y + hit.Height / 2, 200 * 72 / 300.0 - 2, 260 * 72 / 300.0 + 2);
        Assert.True(hit.Width > hit.Height, "Das Wort läuft in der Anzeige waagerecht.");
    }

    [Fact]
    public void Save_RecognizedLines_AreReadBackInReadingOrder()
    {
        // Wörter einer Zeile mit unterschiedlich hohen Boxen (Großbuchstaben, Unterlängen) und eine zweite Zeile
        RecognizedWord[] words =
        [
            new("Erste", new PdfRect(100, 600, 60, 18), 0, 0), new("gquälte", new PdfRect(166, 604, 70, 19), 0, 0),
            new("Zeile.", new PdfRect(242, 601, 55, 16), 0, 0), new("Zweite", new PdfRect(100, 630, 70, 17), 0, 1),
            new("Zeile", new PdfRect(176, 631, 50, 16), 0, 1),
        ];
        var saved = Save(document => document.SetRecognizedText(new Dictionary<int, RecognizedPage>
        {
            [document.Pages[1].Id] = new RecognizedPage(words, PageRotation.None),
        }));

        using var source = PdfSource.Open(saved);
        var text = string.Join(' ', source.PageText(2).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("Erste gquälte Zeile. Zweite Zeile", text);
    }

    [Fact]
    public void Save_RedactedWords_AreNotWritten()
    {
        var secret = new PdfRect(100, 600, 120, 16);
        var saved = Save(document =>
        {
            document.SetRecognizedText(new Dictionary<int, RecognizedPage>
            {
                [document.Pages[1].Id] = Words(PageRotation.None, ("Geheim", secret), ("Offen", new PdfRect(300, 600, 80, 16))),
            });
            document.AddAnnotation(document.Pages[1].Id, new RedactionAnnotation(Guid.NewGuid(), new PdfRect(110, 602, 20, 8)));
        });

        using var source = PdfSource.Open(saved);
        var text = source.PageText(2);
        Assert.DoesNotContain("Geheim", text);
        Assert.Contains("Offen", text);
    }

    [Fact]
    public void ParseVersionAndLanguages_ReadTesseractOutput()
    {
        Assert.Equal("5.3.4", TesseractEngine.ParseVersion("tesseract 5.3.4\n leptonica-1.82.0\n"));
        Assert.Equal(["deu", "eng"], TesseractEngine.ParseLanguages("List of available languages in \"/usr/share/tessdata/\" (3):\ndeu\neng\nosd\n"));
    }

    [Theory]
    [InlineData("Page number: 0\nOrientation in degrees: 90\nRotate: 270\nOrientation confidence: 7.80\n", 270)]
    [InlineData("Page number: 0\nOrientation in degrees: 0\nRotate: 0\nOrientation confidence: 9.1\n", 0)]
    [InlineData("Page number: 0\nOrientation in degrees: 180\nRotate: 180\nOrientation confidence: 0.4\n", 0)]
    [InlineData("Too few characters. Skipping this page\n", 0)]
    public void ParseRotation_ReadsOrientationDetection(string output, int expected)
    {
        Assert.Equal(expected, TesseractEngine.ParseRotation(output));
    }

    [Fact]
    public async Task Tesseract_RecognisesTextOfAnImageOnlyPage()
    {
        if (TesseractEngine.Find() is not { } engine || !engine.Languages.Contains("deu"))
        {
            Assert.Skip("Tesseract mit deutschen Sprachdaten ist nicht installiert.");
            return;
        }

        // Eine geschwärzte Seite ist ein reines Bild ohne Textebene
        var saved = Save(document => document.AddAnnotation(document.Pages[1].Id, new RedactionAnnotation(Guid.NewGuid(), new PdfRect(500, 800, 10, 10))));
        using var source = PdfSource.Open(saved);
        Assert.Equal(string.Empty, source.PageText(2).Trim());

        var page = await engine.RecognizeAsync(source, 2, PageRotation.None, "deu", TestContext.Current.CancellationToken);

        Assert.Contains("Seite", page.Text);
        Assert.Contains("Lorem", page.Text);
        using var original = PdfSource.Open(_file);
        var expected = original.FindText(2, "Seite")[0];
        var found = page.Find("Seite")[0];
        Assert.InRange(found.X, expected.X - 6, expected.X + 6);
        Assert.InRange(found.Y, expected.Y - 6, expected.Y + 6);
    }

    /// <summary>Öffnet die Testdatei, wendet Änderungen an und speichert.</summary>
    private string Save(Action<EditableDocument> edit)
    {
        using var source = PdfSource.Open(_file);
        var document = source.CreateDocument();
        edit(document);
        var target = _temp.File($"erkannt-{Guid.NewGuid():N}.pdf");
        PdfExporter.Save(source, document, target);
        return target;
    }
}
