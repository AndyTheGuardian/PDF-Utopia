using PdfUtopia.Core;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Lesezeichen lesen und beim Speichern/Extrahieren erhalten (A46, P6).
/// </summary>
public sealed class BookmarkTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public BookmarkTests() => _file = TestPdfFactory.CreateWithBookmarks(_temp.File("mit-lesezeichen.pdf"));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Bookmarks_AreReadAsTree()
    {
        using var source = PdfSource.Open(_file);

        Assert.Equal(["Kapitel 1", "Kapitel 2 – Übersicht", "Anhang"], source.Bookmarks.Select(bookmark => bookmark.Title));
        Assert.Equal([1, 5, (int?)null], source.Bookmarks.Select(bookmark => bookmark.SourcePageNumber));
        var section = Assert.Single(source.Bookmarks[0].Children);
        Assert.Equal(("Abschnitt 1.1", 2), (section.Title, section.SourcePageNumber));
        Assert.Equal(14, Assert.Single(source.Bookmarks[2].Children).SourcePageNumber);
    }

    [Fact]
    public void Bookmarks_FileWithout_IsEmpty()
    {
        using var source = PdfSource.Open(TestPdfFactory.Create(_temp.File("ohne.pdf")));
        Assert.Empty(source.Bookmarks);
    }

    [Fact]
    public void Save_KeepsBookmarks_InNewPageOrder()
    {
        var saved = Save(document => document.MovePage(4, 0));

        using var reopened = PdfSource.Open(saved);
        Assert.Equal([2, 1, null], reopened.Bookmarks.Select(bookmark => bookmark.SourcePageNumber));
        Assert.Equal(3, reopened.Bookmarks[0].Children[0].SourcePageNumber);
    }

    [Fact]
    public void Save_DeletedTarget_PromotesChildren()
    {
        var saved = Save(document => document.DeletePage(0));

        using var reopened = PdfSource.Open(saved);
        Assert.Equal(["Abschnitt 1.1", "Kapitel 2 – Übersicht", "Anhang"], reopened.Bookmarks.Select(bookmark => bookmark.Title));
        Assert.Equal(1, reopened.Bookmarks[0].SourcePageNumber);
    }

    [Fact]
    public void Extract_KeepsBookmarksOfExtractedPages()
    {
        using var source = PdfSource.Open(_file);
        var target = _temp.File("auszug.pdf");

        PdfExporter.Extract(source, source.CreateDocument(), [5, 14], target);

        using var extracted = PdfSource.Open(target);
        Assert.Equal(["Kapitel 2 – Übersicht", "Anhang"], extracted.Bookmarks.Select(bookmark => bookmark.Title));
        Assert.Equal(1, extracted.Bookmarks[0].SourcePageNumber);
        Assert.Equal(2, Assert.Single(extracted.Bookmarks[1].Children).SourcePageNumber);
    }

    /// <summary>
    /// Speichert die Datei mit Lesezeichen nach einer Änderung.
    /// </summary>
    private string Save(Action<EditableDocument> edit)
    {
        using var source = PdfSource.Open(_file);
        var document = source.CreateDocument();
        edit(document);
        var target = _temp.File($"{Guid.NewGuid():N}.pdf");
        PdfExporter.Save(source, document, target);
        return target;
    }
}
