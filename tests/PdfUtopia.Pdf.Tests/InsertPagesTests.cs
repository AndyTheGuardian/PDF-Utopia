using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Seiten aus einer anderen Datei einfügen (A41): Speichern mit mehreren Quelldateien, Passwort der Quelle,
/// mitgenommene Annotationen.
/// </summary>
public sealed class InsertPagesTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public InsertPagesTests() => _file = TestPdfFactory.Create(_temp.File("testdatei.pdf"));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Save_WithInsertedPages_WritesPagesOfBothFiles()
    {
        using var opened = PdfSource.Open(_file);
        using var other = PdfSource.Open(TestPdfFactory.Create(_temp.File("anhang.pdf")));
        var document = opened.CreateDocument();
        document.InsertPages(1, 1, [5, 3]);
        var target = _temp.File("zusammen.pdf");

        PdfExporter.Save([opened, other], document, target);

        Assert.Equal([1, 5, 3, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14], HeadingsOf(target));
    }

    [Fact]
    public void Save_ProtectedInsertSource_UsesProtectionOfOpenedFile()
    {
        using var opened = PdfSource.Open(_file);
        using var other = PdfSource.Open(TestPdfFactory.Create(_temp.File("geschützt.pdf"), TestPdfFactory.Password), TestPdfFactory.Password);
        var document = opened.CreateDocument();
        document.InsertPages(0, 1, [14]);
        var target = _temp.File("zusammen.pdf");

        PdfExporter.Save([opened, other], document, target);

        using var reopened = PdfSource.Open(target);
        Assert.Equal(15, reopened.PageCount);
        Assert.Contains("Seite 14", reopened.PageText(1), StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_CanContainInsertedPages()
    {
        using var opened = PdfSource.Open(_file);
        using var other = PdfSource.Open(TestPdfFactory.Create(_temp.File("anhang.pdf")));
        var document = opened.CreateDocument();
        document.InsertPages(0, 1, [7]);
        var target = _temp.File("auszug.pdf");

        PdfExporter.Extract([opened, other], document, [1, 2], target);

        Assert.Equal([7, 1], HeadingsOf(target));
    }

    [Fact]
    public void Save_InsertedPages_KeepTheirAnnotationsEditable()
    {
        // Quelle mit einem Kommentar auf Seite 2 erzeugen
        var annotated = _temp.File("mit-kommentar.pdf");
        using (var plain = PdfSource.Open(TestPdfFactory.Create(_temp.File("anhang.pdf"))))
        {
            var sourceDocument = plain.CreateDocument();
            sourceDocument.AddAnnotation(2, new CommentAnnotation(Guid.NewGuid(), "#4F46E5", new PdfRect(60, 120, 300, 40), "Aus dem Anhang"));
            PdfExporter.Save(plain, sourceDocument, annotated);
        }

        using var opened = PdfSource.Open(_file);
        using var other = PdfSource.Open(annotated);
        var document = opened.CreateDocument();
        var inserted = document.InsertPages(14, 1, [2], page => other.SavedAnnotations.GetValueOrDefault(page) ?? []);
        Assert.Equal("Aus dem Anhang", Assert.IsType<CommentAnnotation>(Assert.Single(document.Annotations.For(inserted[0].Id))).Text);
        var target = _temp.File("zusammen.pdf");

        PdfExporter.Save([opened, other], document, target);

        using var reopened = PdfSource.Open(target);
        var comment = Assert.IsType<CommentAnnotation>(Assert.Single(reopened.SavedAnnotations[15]));
        Assert.Equal("Aus dem Anhang", comment.Text);
    }

    [Fact]
    public void BlankPage_HasRequestedSize_AndIsSavedAsEmptyPage()
    {
        using var opened = PdfSource.Open(_file);
        using var blank = PdfSource.CreateBlankPage(842, 595);
        Assert.True(blank.IsGenerated);
        Assert.Equal((842, 595), blank.PageSize(1));
        var document = opened.CreateDocument();
        document.InsertPages(1, 1, [1]);
        var target = _temp.File("mit-leerer-seite.pdf");

        PdfExporter.Save([opened, blank], document, target);

        using var reopened = PdfSource.Open(target);
        Assert.Equal(15, reopened.PageCount);
        Assert.Equal((842, 595), reopened.PageSize(2));
        Assert.Equal(string.Empty, reopened.PageText(2));
        Assert.Contains("Seite 2", reopened.PageText(3), StringComparison.Ordinal);
    }

    [Fact]
    public void Save_UnknownSourceIndex_Throws()
    {
        using var opened = PdfSource.Open(_file);
        var document = opened.CreateDocument();
        document.InsertPages(0, 1, [1]);

        Assert.Throws<ArgumentOutOfRangeException>(() => PdfExporter.Save(opened, document, _temp.File("x.pdf")));
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
}
