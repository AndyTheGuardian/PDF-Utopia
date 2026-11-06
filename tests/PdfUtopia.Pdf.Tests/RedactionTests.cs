using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Schwärzen (A52, F38): Seiten mit Schwärzungen werden als Bild gespeichert; unter den Bereichen bleibt nichts übrig,
/// Text und alter Seiteninhalt sind nicht mehr in der Datei, andere Seiten bleiben unverändert.
/// </summary>
public sealed class RedactionTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public RedactionTests() => _file = TestPdfFactory.Create(_temp.File("testdatei.pdf"));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Save_RedactedPage_IsBlackUnderTheArea_AndHasNoText()
    {
        var heading = HitOf(_file, 2, "Seite 2");
        var saved = Save(_file, document => document.AddAnnotation(document.Pages[1].Id, RedactionAnnotation.ForSearchHit(heading)));

        using var source = PdfSource.Open(saved);
        Assert.Equal(string.Empty, source.PageText(2).Trim());
        Assert.Contains("Seite 3", source.PageText(3));
        Assert.Equal((TestPdfFactory.Width, TestPdfFactory.Height), source.PageSize(2));
        var page = source.Render(2, scale: 1);
        Assert.Equal((0, 0, 0), page.PixelAt((int)(heading.X + heading.Width / 2), (int)(heading.Y + heading.Height / 2)));
        Assert.Empty(source.CreateDocument().Annotations.For(2));
    }

    [Fact]
    public void Save_RedactedPage_KeepsTheRestOfThePageVisible()
    {
        var heading = HitOf(_file, 2, "Seite 2");
        var saved = Save(_file, document => document.AddAnnotation(document.Pages[1].Id, RedactionAnnotation.ForSearchHit(heading)));

        using var original = PdfSource.Open(_file);
        using var source = PdfSource.Open(saved);
        // Ein Absatz weiter unten ist noch zu sehen (dunkle Pixel), obwohl er nur noch Bild ist
        var paragraph = original.FindText(2, "Lorem")[0];
        var pixels = source.Render(2, scale: 2);
        var dark = Enumerable.Range((int)(paragraph.X * 2), (int)(paragraph.Width * 2))
            .Any(x => pixels.PixelAt(x, (int)((paragraph.Y + paragraph.Height / 2) * 2)).R < 128);
        Assert.True(dark, "Der Absatz neben der Schwärzung muss sichtbar bleiben.");
    }

    [Fact]
    public void Extract_OnlyRedactedPage_LeavesNoFontsOrTextInTheFile()
    {
        var heading = HitOf(_file, 2, "Seite 2");
        using var source = PdfSource.Open(_file);
        var document = new EditableDocument(source.PageCount);
        document.AddAnnotation(document.Pages[1].Id, RedactionAnnotation.ForSearchHit(heading));
        var target = _temp.File("auszug.pdf");
        PdfExporter.Extract(source, document, [2], target);

        using var pdf = PdfReader.Open(target, PdfDocumentOpenMode.Import);
        var objects = pdf.Internals.GetAllObjects();
        Assert.DoesNotContain(objects.OfType<PdfDictionary>(), dictionary => dictionary.Elements.GetName("/Type") == "/Font");
        var streams = objects.OfType<PdfDictionary>().Where(dictionary => dictionary.Stream is not null).ToList();
        Assert.All(streams, stream =>
            Assert.True(stream.Elements.GetName("/Subtype") == "/Image" || !System.Text.Encoding.Latin1.GetString(stream.Stream.UnfilteredValue).Contains("Tj"),
                "Kein Textoperator darf übrig bleiben."));
    }

    [Fact]
    public void Save_RotatedAndExtendedPage_PlacesBlackAreaOnTheTurnedPage()
    {
        var area = new PdfRect(60, 60, 120, 30);
        var saved = Save(_file, document =>
        {
            document.AddAnnotation(document.Pages[1].Id, new RedactionAnnotation(Guid.NewGuid(), area));
            document.AddAnnotation(document.Pages[1].Id, new CommentAnnotation(Guid.NewGuid(), "#4F46E5", new PdfRect(50, 400, 300, 40), "Hinweis"));
            document.RotatePage(1);
        });

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var (width, height) = source.PageSize(2);
        Assert.True(width > height, "Die Seite bleibt quer gedreht.");
        // Nach 90° im Uhrzeigersinn liegt die linke obere Ecke oben rechts: x = Breite − y
        var center = PageTransform.Rotate(new PdfPoint(area.X + area.Width / 2, area.Y + area.Height / 2), Core.Pages.PageRotation.Clockwise90, height, width);
        Assert.Equal((0, 0, 0), source.Render(2, scale: 1).PixelAt((int)center.X, (int)center.Y));
    }

    [Fact]
    public void Save_RedactedFormPage_RemovesItsFieldsFromTheForm()
    {
        var formFile = TestPdfFactory.CreateWithForm(_temp.File("formular.pdf"));
        var saved = Save(formFile, document => document.AddAnnotation(document.Pages[0].Id, new RedactionAnnotation(Guid.NewGuid(), new PdfRect(100, 122, 200, 20))));

        using var pdf = PdfReader.Open(saved, PdfDocumentOpenMode.Import);
        var fields = pdf.Internals.Catalog.Elements.GetDictionary("/AcroForm")!.Elements.GetArray("/Fields")!;
        var names = fields.Elements.OfType<PdfSharp.Pdf.Advanced.PdfReference>().Select(item => ((PdfDictionary)item.Value).Elements.GetString("/T")).ToList();
        Assert.DoesNotContain("Name", names);
        Assert.Contains("Newsletter", names);
        Assert.Null(pdf.Pages[0].Elements.GetArray("/Annots"));
    }

    [Fact]
    public void AddAnnotations_OnSeveralPages_IsOneUndoStep()
    {
        var document = new EditableDocument(3);
        document.AddAnnotations([(1, RedactionAnnotation.ForSearchHit(new PdfRect(10, 10, 50, 10))), (3, RedactionAnnotation.ForSearchHit(new PdfRect(10, 10, 50, 10)))]);

        Assert.Single(document.Annotations.For(1));
        Assert.Single(document.Annotations.For(3));
        document.History.Undo();
        Assert.Empty(document.Annotations.For(1));
        Assert.Empty(document.Annotations.For(3));
    }

    /// <summary>Erste Fundstelle eines Begriffs auf einer Seite.</summary>
    private static PdfRect HitOf(string file, int page, string term)
    {
        using var source = PdfSource.Open(file);
        return source.FindText(page, term)[0];
    }

    /// <summary>Öffnet eine Datei, wendet Änderungen an und speichert.</summary>
    private string Save(string file, Action<EditableDocument> edit)
    {
        using var source = PdfSource.Open(file);
        var document = source.CreateDocument();
        edit(document);
        var target = _temp.File("geschwaerzt.pdf");
        PdfExporter.Save(source, document, target);
        return target;
    }
}
