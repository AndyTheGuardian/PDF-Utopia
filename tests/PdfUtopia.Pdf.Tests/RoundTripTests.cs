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
/// Tests zum Wiederöffnen gespeicherter Dateien (F23): Annotationen kommen bearbeitbar zurück, erscheinen nicht
/// doppelt, die Seitenverlängerung wird nicht wiederholt, fremde Annotationen bleiben erhalten.
/// </summary>
public sealed class RoundTripTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public RoundTripTests() => _file = TestPdfFactory.Create(_temp.File("testdatei.pdf"));

    public void Dispose() => _temp.Dispose();

    private static readonly Annotation[] AllKinds =
    [
        new InkAnnotation(Guid.NewGuid(), "#1F9D55", 4, [new(new PdfPoint(100, 700), 0.2f), new(new PdfPoint(160, 690), 0.9f)], 0.5, true),
        new HighlightAnnotation(Guid.NewGuid(), "#FDE047", new PdfRect(70, 60, 300, 14)),
        new RectangleAnnotation(Guid.NewGuid(), "#2F6FDF", 2, new PdfRect(100, 300, 300, 200)),
        new CommentAnnotation(Guid.NewGuid(), "#4F46E5", new PdfRect(50, 100, 400, 60), "Erster Hinweis"),
        new CommentAnnotation(Guid.NewGuid(), "#FFECB3", new PdfRect(50, 180, 400, 40), "Zweiter Hinweis – Größe"),
    ];

    [Fact]
    public void Reopen_RestoresAllAnnotationsInOrder()
    {
        var saved = Save(_file, document => Add(document, 0, AllKinds));

        using var source = PdfSource.Open(saved);
        var document = source.CreateDocument();

        Assert.Equal(AllKinds.Select(AnnotationSerializer.Serialize), document.Annotations.For(document.Pages[0].Id).Select(AnnotationSerializer.Serialize));
        Assert.False(document.IsModified);
        Assert.False(document.History.CanUndo);
    }

    [Fact]
    public void Reopen_RemovesOwnAnnotationsFromDisplayAndExtension()
    {
        var saved = Save(_file, document => Add(document, 0, AllKinds));

        using var source = PdfSource.Open(saved);

        Assert.Equal((TestPdfFactory.Width, TestPdfFactory.Height), source.PageSize(1));
        var page = source.Render(1, scale: 1);
        Assert.Equal((255, 255, 255), page.PixelAt(100, 470));
        Assert.Equal((255, 255, 255), page.PixelAt(50, 130));
    }

    [Fact]
    public void SaveAgain_ExtendsPageOnlyOnce()
    {
        var first = Save(_file, document => Add(document, 0, AllKinds), "erste.pdf");
        var second = Save(first, _ => { }, "zweite.pdf");

        using var firstSource = PdfSource.Open(first);
        using var secondSource = PdfSource.Open(second);
        Assert.Equal(PageHeightInFile(first), PageHeightInFile(second));
        Assert.Equal(AllKinds.Length, secondSource.CreateDocument().Annotations.For(1).Count);
    }

    [Fact]
    public void Reopen_EditedAnnotationsAreSavedAgain()
    {
        var first = Save(_file, document => Add(document, 0, AllKinds), "erste.pdf");
        var commentId = AllKinds.OfType<CommentAnnotation>().First().Id;
        var second = Save(first, document =>
        {
            var comment = document.Annotations.CommentsFor(1).Single(c => c.Id == commentId);
            document.UpdateAnnotation(1, comment with { Text = "Geändert" });
            document.RemoveAnnotation(1, AllKinds[0].Id);
        }, "zweite.pdf");

        using var source = PdfSource.Open(second);
        var annotations = source.CreateDocument().Annotations;
        Assert.Equal(AllKinds.Length - 1, annotations.For(1).Count);
        Assert.Equal("Geändert", annotations.CommentsFor(1)[0].Text);
    }

    [Fact]
    public void Reopen_AfterRotation_AnnotationsMatchRotatedPage()
    {
        var rectangle = (RectangleAnnotation)AllKinds[2];
        var saved = Save(_file, document =>
        {
            Add(document, 1, [rectangle]);
            document.RotatePage(1);
        });

        using var source = PdfSource.Open(saved);
        var restored = (RectangleAnnotation)source.CreateDocument().Annotations.For(2).Single();

        Assert.Equal((TestPdfFactory.Height, TestPdfFactory.Width), source.PageSize(2));
        Assert.Equal(PageTransform.Rotate(rectangle.Bounds, PageRotation.Clockwise90, TestPdfFactory.Width, TestPdfFactory.Height), restored.Bounds);
    }

    [Fact]
    public void Reopen_KeepsForeignAnnotations()
    {
        AddForeignAnnotation(_file);
        var saved = Save(_file, document => Add(document, 0, [AllKinds[2]]));

        using var source = PdfSource.Open(saved);
        Assert.Single(source.CreateDocument().Annotations.For(1));
        var resaved = Save(saved, _ => { }, "erneut.pdf");
        Assert.Contains("/Text", Subtypes(resaved, 0));
        Assert.Contains("/Square", Subtypes(resaved, 0));
    }

    [Fact]
    public void Reopen_FileWithoutOwnAnnotations_HasNoSavedAnnotations()
    {
        using var source = PdfSource.Open(_file);

        Assert.Empty(source.SavedAnnotations);
    }

    [Fact]
    public void Reopen_ProtectedFile_RestoresAnnotations()
    {
        var protectedFile = TestPdfFactory.Create(_temp.File("geschützt.pdf"), TestPdfFactory.Password);
        var saved = _temp.File("geschützt-gespeichert.pdf");
        using (var source = PdfSource.Open(protectedFile, TestPdfFactory.Password))
        {
            var document = source.CreateDocument();
            Add(document, 0, [AllKinds[3]]);
            PdfExporter.Save(source, document, saved);
        }

        using var reopened = PdfSource.Open(saved, TestPdfFactory.Password);
        Assert.Single(reopened.CreateDocument().Annotations.For(1));
        Assert.Equal((TestPdfFactory.Width, TestPdfFactory.Height), reopened.PageSize(1));
        Assert.Equal(PdfOpenError.PasswordRequired, Assert.Throws<PdfOpenException>(() => PdfSource.Open(saved)).Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("{\"Version\":99,\"Annotation\":{\"kind\":\"comment\"}}")]
    public void Serializer_UnknownOrBrokenData_ReturnsNull(string? json) => Assert.Null(AnnotationSerializer.Deserialize(json));

    private static void Add(EditableDocument document, int pageIndex, IEnumerable<Annotation> annotations)
    {
        foreach (var annotation in annotations)
        {
            document.AddAnnotation(document.Pages[pageIndex].Id, annotation);
        }
    }

    private string Save(string path, Action<EditableDocument> edit, string name = "gespeichert.pdf")
    {
        using var source = PdfSource.Open(path);
        var document = source.CreateDocument();
        edit(document);
        var target = _temp.File(name);
        PdfExporter.Save(source, document, target);
        return target;
    }

    private static double PageHeightInFile(string path)
    {
        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var box = pdf.Pages[0].MediaBox;
        return box.Y2 - box.Y1;
    }

    private static List<string> Subtypes(string path, int pageIndex)
    {
        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var annots = pdf.Pages[pageIndex].Elements.GetArray("/Annots");
        return annots is null
            ? []
            : annots.Elements.Select(item => ((PdfDictionary)((PdfSharp.Pdf.Advanced.PdfReference)item).Value).Elements.GetName("/Subtype")).ToList();
    }

    /// <summary>
    /// Fügt eine Notiz-Annotation hinzu, wie sie ein anderes Programm anlegen würde.
    /// </summary>
    private static void AddForeignAnnotation(string path)
    {
        using (var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Modify))
        {
            var note = new PdfSharp.Pdf.Annotations.PdfTextAnnotation { Title = "Fremd", Contents = "Notiz eines anderen Programms" };
            note.Rectangle = new PdfRectangle(new PdfSharp.Drawing.XPoint(500, 800), new PdfSharp.Drawing.XPoint(520, 820));
            pdf.Pages[0].Annotations.Add(note);
            pdf.Save(path + ".neu");
        }

        File.Move(path + ".neu", path, overwrite: true);
    }
}
