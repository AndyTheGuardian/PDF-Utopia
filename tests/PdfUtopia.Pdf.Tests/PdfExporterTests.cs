using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Prüft alle Verfahren an der Testdatei (P1): Speichern mit Reihenfolge, Drehung und Löschen, Extrahieren,
/// Annotationen als echte PDF-Annotationen, Seitenverlängerung für Fußnoten, Passwortschutz.
/// Geprüft wird das gespeicherte Ergebnis – über ausgelesenen Text, die PDF-Struktur und gerenderte Pixel.
/// </summary>
public sealed class PdfExporterTests : IDisposable
{
    private const string Blue = "#2F6FDF";

    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public PdfExporterTests() => _file = TestPdfFactory.Create(_temp.File("testdatei.pdf"));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Save_Unchanged_KeepsAllPagesInOrder()
    {
        var saved = SaveWith(_ => { });

        Assert.Equal(Enumerable.Range(1, 14), HeadingsOf(saved));
    }

    [Fact]
    public void Save_AfterReorder_WritesNewOrder()
    {
        var saved = SaveWith(document =>
        {
            document.MovePage(0, 3);
            document.MovePage(13, 0);
        });

        Assert.Equal([14, 2, 3, 4, 1, 5, 6, 7, 8, 9, 10, 11, 12, 13], HeadingsOf(saved));
    }

    [Fact]
    public void Save_AfterDelete_OmitsPage()
    {
        var saved = SaveWith(document => document.DeletePage(4));

        Assert.Equal([1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14], HeadingsOf(saved));
    }

    [Fact]
    public void Save_UndoneChange_IsNotSaved()
    {
        var saved = SaveWith(document =>
        {
            document.DeletePage(0);
            document.History.Undo();
        });

        Assert.Equal(14, HeadingsOf(saved).Count);
    }

    [Fact]
    public void Save_Rotation_AddsToRotationInFile()
    {
        var saved = SaveWith(document =>
        {
            document.RotatePage(0);
            document.RotatePage(TestPdfFactory.RotatedPage - 1);
            document.RotatePage(TestPdfFactory.RotatedPage - 1);
        });

        using var pdf = PdfReader.Open(saved, PdfDocumentOpenMode.Import);
        Assert.Equal(90, pdf.Pages[0].Rotate);
        Assert.Equal(0, pdf.Pages[1].Rotate);
        Assert.Equal(270, pdf.Pages[TestPdfFactory.RotatedPage - 1].Rotate);
    }

    [Fact]
    public void Extract_Selection_WritesPagesInInputOrder()
    {
        using var source = PdfSource.Open(_file);
        var document = new EditableDocument(source.PageCount);
        var selection = PageSelectionParser.Parse("2,8-4,13-20,2", document.Pages.Count);
        var target = _temp.File("auszug.pdf");

        PdfExporter.Extract(source, document, selection.Pages, target);

        Assert.Equal([2, 4, 5, 6, 7, 8, 13, 14], HeadingsOf(target));
    }

    [Fact]
    public void Extract_RefersToCurrentPageOrder()
    {
        using var source = PdfSource.Open(_file);
        var document = new EditableDocument(source.PageCount);
        document.MovePage(9, 0);
        var target = _temp.File("auszug.pdf");

        PdfExporter.Extract(source, document, [1, 2], target);

        Assert.Equal([10, 1], HeadingsOf(target));
    }

    [Fact]
    public void Extract_WithoutPages_Throws()
    {
        using var source = PdfSource.Open(_file);

        Assert.Throws<ArgumentException>(() => PdfExporter.Extract(source, new EditableDocument(14), [], _temp.File("leer.pdf")));
    }

    [Fact]
    public void Save_Annotations_AreRealPdfAnnotationsWithAppearance()
    {
        var saved = SaveWith(document =>
        {
            var page = document.Pages[0].Id;
            document.AddAnnotation(page, Ink(pressure: false));
            document.AddAnnotation(page, Ink(pressure: true));
            document.AddAnnotation(page, new HighlightAnnotation(Guid.NewGuid(), "#FDE047", new PdfRect(70, 60, 300, 14)));
            document.AddAnnotation(page, Rectangle(new PdfRect(70, 300, 455, 120)));
            document.AddAnnotation(page, Comment("Erster Hinweis"));
            document.AddAnnotation(page, Comment("Zweiter Hinweis mit Umlauten: Größe ändern"));
        });

        var annotations = AnnotationsOf(saved, 0);
        Assert.Equal(
            ["/Ink", "/Ink", "/Highlight", "/Square", "/Square", "/Square", "/FreeText", "/FreeText", "/FreeText"],
            annotations.Select(annotation => annotation.Elements.GetName("/Subtype")));
        Assert.All(annotations, annotation => Assert.NotNull(annotation.Elements.GetDictionary("/AP")?.Elements.GetReference("/N")));
        Assert.Contains(annotations, annotation => annotation.Elements.GetString("/Contents") == "Zweiter Hinweis mit Umlauten: Größe ändern");
        Assert.Empty(AnnotationsOf(saved, 1));
    }

    [Fact]
    public void Save_Comment_ExtendsOnlyThatPageDownwards()
    {
        var saved = SaveWith(document => document.AddAnnotation(document.Pages[0].Id, Comment("Hinweis")));

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var (width, height) = source.PageSize(1);
        Assert.Equal(TestPdfFactory.Width, width);
        Assert.InRange(height, TestPdfFactory.Height + 40, TestPdfFactory.Height + 80);
        Assert.Equal((TestPdfFactory.Width, TestPdfFactory.Height), source.PageSize(2));
        Assert.Contains("Seite 1", source.PageText(1));
    }

    [Fact]
    public void Save_LongComment_ExtendsPageFurther()
    {
        var shortSaved = SaveWith(document => document.AddAnnotation(document.Pages[0].Id, Comment("kurz")), "kurz.pdf");
        var longSaved = SaveWith(document => document.AddAnnotation(document.Pages[0].Id, Comment(string.Join(' ', Enumerable.Repeat("Langer Kommentartext", 40)))), "lang.pdf");

        using var shortSource = PdfSource.Open(shortSaved, importSavedAnnotations: false);
        using var longSource = PdfSource.Open(longSaved, importSavedAnnotations: false);
        Assert.True(longSource.PageSize(1).Height > shortSource.PageSize(1).Height + 50);
    }

    [Fact]
    public void Save_Rectangle_IsRenderedAtDisplayPosition()
    {
        var saved = SaveWith(document => document.AddAnnotation(document.Pages[1].Id, Rectangle(new PdfRect(100, 100, 300, 200))));

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(2, scale: 1);
        AssertBlue(page.PixelAt(100, 200));
        AssertBlue(page.PixelAt(250, 300));
        Assert.Equal((255, 255, 255), page.PixelAt(250, 290));
    }

    [Fact]
    public void Save_Rectangle_HasRoundedCorners()
    {
        var saved = SaveWith(document => document.AddAnnotation(document.Pages[1].Id, Rectangle(new PdfRect(100, 100, 300, 200))));

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(2, scale: 4);
        var (r, g, b) = page.PixelAt(400, 400);
        Assert.True(r > 200 && g > 200 && b > 200, $"Ecke sollte abgerundet (hell) sein, war ({r}, {g}, {b})");
        AssertBlue(page.PixelAt(400, 600));
        AssertBlue(page.PixelAt(800, 400));
    }

    [Fact]
    public void Save_Rectangle_OnPageRotatedInFile_IsRenderedAtDisplayPosition()
    {
        var saved = SaveWith(document =>
            document.AddAnnotation(document.Pages[TestPdfFactory.RotatedPage - 1].Id, Rectangle(new PdfRect(100, 100, 300, 200))));

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(TestPdfFactory.RotatedPage, scale: 1);
        Assert.Equal((842, 595), (page.Width, page.Height));
        AssertBlue(page.PixelAt(100, 200));
        AssertBlue(page.PixelAt(400, 150));
        AssertBlue(page.PixelAt(250, 100));
    }

    [Fact]
    public void Save_RectangleOnRotatedPage_TurnsWithPage()
    {
        var saved = SaveWith(document =>
        {
            document.AddAnnotation(document.Pages[1].Id, Rectangle(new PdfRect(100, 100, 300, 200)));
            document.RotatePage(1);
        });

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(2, scale: 1);
        Assert.Equal((842, 595), (page.Width, page.Height));
        // 90° im Uhrzeigersinn: Anzeigepunkt (x, y) liegt danach bei (Höhe − y, x)
        AssertBlue(page.PixelAt(842 - 200, 100));
    }

    [Fact]
    public void Save_Comment_IsRenderedWithBorderAndBadge()
    {
        var saved = SaveWith(document => document.AddAnnotation(document.Pages[0].Id, Comment("Hinweis")));

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(1, scale: 1);
        var border = page.PixelAt(50, 130);
        Assert.True(border.B > 150 && border.R < 140, $"Indigo-Rahmen erwartet, war {border}");
        var badge = page.PixelAt(450 - 4, 100 - 4);
        Assert.True(badge.B > 150 && badge.R < 140, $"Fußnotenzahl-Kreis erwartet, war {badge}");
    }

    [Fact]
    public void Save_ProtectedSource_KeepsPassword()
    {
        var protectedFile = TestPdfFactory.Create(_temp.File("geschützt.pdf"), TestPdfFactory.Password);
        using var source = PdfSource.Open(protectedFile, TestPdfFactory.Password);
        var target = _temp.File("geschützt-gespeichert.pdf");

        PdfExporter.Save(source, new EditableDocument(source.PageCount), target);

        Assert.Equal(PdfOpenError.PasswordRequired, Assert.Throws<PdfOpenException>(() => PdfSource.Open(target)).Error);
        using var reopened = PdfSource.Open(target, TestPdfFactory.Password);
        Assert.Equal(TestPdfFactory.PageCount, reopened.PageCount);
    }

    [Fact]
    public void Save_OverSourceFile_Works()
    {
        using var source = PdfSource.Open(_file);
        var document = new EditableDocument(source.PageCount);
        document.DeletePage(0);

        PdfExporter.Save(source, document, _file);

        using var reopened = PdfSource.Open(_file);
        Assert.Equal(13, reopened.PageCount);
        Assert.Empty(Directory.GetFiles(_temp.Path, "*.tmp"));
    }

    private static InkAnnotation Ink(bool pressure) =>
        new(Guid.NewGuid(), "#C93434", 3,
            [new(new PdfPoint(100, 700), 0.2f), new(new PdfPoint(150, 680), 0.6f), new(new PdfPoint(200, 720), 0.9f)],
            Smoothing: 0.5, PressureSensitive: pressure);

    private static RectangleAnnotation Rectangle(PdfRect bounds) => new(Guid.NewGuid(), Blue, 2, bounds);

    private static CommentAnnotation Comment(string text) => new(Guid.NewGuid(), "#4F46E5", new PdfRect(50, 100, 400, 60), text);

    private static void AssertBlue((byte R, byte G, byte B) pixel) =>
        Assert.True(pixel.B > 180 && pixel.R < 120, $"Blauer Rahmen erwartet, war {pixel}");

    /// <summary>
    /// Öffnet die Testdatei, wendet Änderungen an und speichert.
    /// </summary>
    private string SaveWith(Action<EditableDocument> edit, string name = "gespeichert.pdf")
    {
        using var source = PdfSource.Open(_file);
        var document = new EditableDocument(source.PageCount);
        edit(document);
        var target = _temp.File(name);
        PdfExporter.Save(source, document, target);
        return target;
    }

    /// <summary>
    /// Liest die Überschriften „Seite N“ einer Datei in Seitenreihenfolge.
    /// </summary>
    private static List<int> HeadingsOf(string path)
    {
        using var source = PdfSource.Open(path);
        return Enumerable.Range(1, source.PageCount)
            .Select(page => System.Text.RegularExpressions.Regex.Match(source.PageText(page), @"Seite (\d+)"))
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
    }

    /// <summary>
    /// Liest die Annotationen einer Seite mit PDFsharp.
    /// </summary>
    private static List<PdfDictionary> AnnotationsOf(string path, int pageIndex)
    {
        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var annots = pdf.Pages[pageIndex].Elements.GetArray("/Annots");
        if (annots is null)
        {
            return [];
        }

        return annots.Elements.Select(item => item is PdfSharp.Pdf.Advanced.PdfReference reference ? (PdfDictionary)reference.Value : (PdfDictionary)item).ToList();
    }
}
