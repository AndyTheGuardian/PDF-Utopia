using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Schreibt den Ordner <c>testdata/</c> im Projektverzeichnis neu (P1). Läuft nur auf ausdrücklichen Aufruf:
/// <c>dotnet test --project tests/PdfUtopia.Pdf.Tests -- --filter-method "*WriteTestdataFolder" --explicit only</c>.
/// </summary>
public sealed class TestdataGenerator
{
    [Fact(Explicit = true)]
    public void WriteTestdataFolder()
    {
        var folder = Path.Combine(RepositoryRoot(), "testdata");
        Directory.CreateDirectory(folder);

        var plain = TestPdfFactory.Create(Path.Combine(folder, "testdatei.pdf"));
        TestPdfFactory.Create(Path.Combine(folder, "testdatei-geschuetzt.pdf"), TestPdfFactory.Password);
        WriteEditedExample(plain, Path.Combine(folder, "beispiel-bearbeitet.pdf"));
        WriteBookmarkExample();
    }

    /// <summary>
    /// Schreibt nur die Testdatei mit Lesezeichen (A46), ohne die übrigen Testdateien neu zu erzeugen:
    /// <c>dotnet test --project tests/PdfUtopia.Pdf.Tests -- --filter-method "*WriteBookmarkExample" --explicit only</c>.
    /// </summary>
    [Fact(Explicit = true)]
    public void WriteBookmarkExample() =>
        TestPdfFactory.CreateWithBookmarks(Path.Combine(RepositoryRoot(), "testdata", "testdatei-lesezeichen.pdf"));

    /// <summary>
    /// Beispiel mit allen Annotationsarten, umsortierten, gedrehten und gelöschten Seiten.
    /// </summary>
    private static void WriteEditedExample(string sourcePath, string targetPath)
    {
        using var source = PdfSource.Open(sourcePath);
        var document = new EditableDocument(source.PageCount);
        var first = document.Pages[0].Id;
        document.AddAnnotation(first, new HighlightAnnotation(Guid.NewGuid(), "#FDE047", new PdfRect(68, 113, 300, 15)));
        document.AddAnnotation(first, new CommentAnnotation(Guid.NewGuid(), "#4F46E5", new PdfRect(62, 150, 330, 26), "Zahlen bitte mit der Quartalsauswertung abgleichen."));
        document.AddAnnotation(first, new CommentAnnotation(Guid.NewGuid(), "#FFECB3", new PdfRect(62, 190, 330, 26), "Heller Pastellton: Zahl dunkel, Rahmen grau. Umlaute: Größe, Übersicht, Maß."));
        document.AddAnnotation(first, new RectangleAnnotation(Guid.NewGuid(), "#2F6FDF", 2, new PdfRect(64, 296, 467, 132)));
        document.AddAnnotation(first, new InkAnnotation(Guid.NewGuid(), "#C93434", 3, Wave(380, 470), 0.5, PressureSensitive: false));
        document.AddAnnotation(first, new InkAnnotation(Guid.NewGuid(), "#1F9D55", 4, Wave(380, 520), 1, PressureSensitive: true));
        document.MovePage(13, 1);
        document.RotatePage(2);
        document.DeletePage(5);

        PdfExporter.Save(source, document, targetPath);
    }

    /// <summary>
    /// Wellenlinie mit ansteigendem Andruck.
    /// </summary>
    private static StrokePoint[] Wave(double x, double y) =>
        Enumerable.Range(0, 12).Select(i => new StrokePoint(new PdfPoint(x + i * 12, y + 10 * Math.Sin(i / 1.5)), i / 11f)).ToArray();

    /// <summary>
    /// Sucht das Projektverzeichnis (enthält <c>PdfUtopia.slnx</c>).
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PdfUtopia.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("PdfUtopia.slnx nicht gefunden.");
    }
}
