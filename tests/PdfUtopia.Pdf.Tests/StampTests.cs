using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Images;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Stempel und Bilder (A51): gespeichert als „Stamp“-Annotation mit Erscheinungsbild, gedreht, und beim Wiederöffnen
/// mit denselben Bilddaten wieder bearbeitbar.
/// </summary>
public sealed class StampTests : IDisposable
{
    /// <summary>Rotes JPEG mit 16 × 8 Bildpunkten.</summary>
    private static readonly byte[] RedJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAIABADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDwKiiivzI/uM//2Q==");

    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public StampTests() => _file = TestPdfFactory.Create(_temp.File("testdatei.pdf"));

    public void Dispose() => _temp.Dispose();

    /// <summary>Blaues Bild 4 × 2: obere Zeile deckend, untere durchsichtig.</summary>
    private static ImageData BlueHalfTransparent() => ImageData.FromRgba(4, 2,
    [
        .. Enumerable.Repeat<byte[]>([20, 60, 230, 255], 4).SelectMany(pixel => pixel),
        .. Enumerable.Repeat<byte[]>([20, 60, 230, 0], 4).SelectMany(pixel => pixel),
    ]);

    [Fact]
    public void Save_Stamp_IsStampAnnotationWithFrameAndText()
    {
        var stamp = StampAnnotation.Create("Genehmigt", "06.11.2026", "#B91C1C", new PdfPoint(300, 400), 200);
        var saved = Save(document => document.AddAnnotation(document.Pages[1].Id, stamp));

        var annotation = Assert.Single(AnnotationsOf(saved, 1));
        Assert.Equal("/Stamp", annotation.Elements.GetName("/Subtype"));
        Assert.Equal("GENEHMIGT 06.11.2026", annotation.Elements.GetString("/Contents"));
        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(2, scale: 1);
        // Linker Rahmen in der Mitte der Höhe ist rot
        AssertRed(page.PixelAt((int)Math.Round(stamp.Bounds.X + 1.5 * stamp.Bounds.Width / stamp.Layout.Width), 400));
    }

    [Fact]
    public void Save_RotatedStamp_TurnsTheFrame()
    {
        var stamp = StampAnnotation.Create("Entwurf", string.Empty, "#B91C1C", new PdfPoint(300, 400), 200, angle: 90);
        var saved = Save(document => document.AddAnnotation(document.Pages[1].Id, stamp));

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(2, scale: 1);
        var top = stamp.Corners[0];
        var outer = stamp.FromLocal(0, -stamp.Bounds.Height / 2 + 1.5 * stamp.Bounds.Width / stamp.Layout.Width);
        // Nach 90° liegt die Oberkante rechts; sie verläuft senkrecht durch die Mitte der Höhe
        AssertRed(page.PixelAt((int)Math.Round(outer.X), 400));
        Assert.True(top.X > 300);
        // Links neben dem gedrehten Stempel bleibt die Seite unverändert
        using var original = PdfSource.Open(_file, importSavedAnnotations: false);
        Assert.Equal(original.Render(2, scale: 1).PixelAt(300 - 90, 400), page.PixelAt(300 - 90, 400));
    }

    [Fact]
    public void Save_Image_EmbedsPixelsWithAlphaMask()
    {
        var image = ImageAnnotation.Create(BlueHalfTransparent(), new PdfPoint(300, 400), 200);
        var saved = Save(document => document.AddAnnotation(document.Pages[1].Id, image));

        var annotation = Assert.Single(AnnotationsOf(saved, 1));
        Assert.Equal("/Stamp", annotation.Elements.GetName("/Subtype"));
        var picture = (PdfDictionary)((PdfSharp.Pdf.Advanced.PdfReference)annotation.Elements[AnnotationWriter.ImageKey]!).Value;
        Assert.Equal("/FlateDecode", picture.Elements.GetName("/Filter"));
        Assert.NotNull(picture.Elements["/SMask"]);

        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        var page = source.Render(2, scale: 1);
        var blue = page.PixelAt(300, 375);
        Assert.True(blue.B > 180 && blue.R < 80, $"Blau erwartet, war {blue}");
        Assert.Equal((255, 255, 255), page.PixelAt(300, 425));
    }

    [Fact]
    public void Save_Jpeg_IsEmbeddedUnchanged()
    {
        var jpeg = new ImageData(16, 8, ImageEncoding.Jpeg, RedJpeg);
        var image = ImageAnnotation.Create(jpeg, new PdfPoint(300, 400), 160, angle: 30);
        var saved = Save(document => document.AddAnnotation(document.Pages[1].Id, image));

        var annotation = Assert.Single(AnnotationsOf(saved, 1));
        var picture = (PdfDictionary)((PdfSharp.Pdf.Advanced.PdfReference)annotation.Elements[AnnotationWriter.ImageKey]!).Value;
        Assert.Equal("/DCTDecode", picture.Elements.GetName("/Filter"));
        Assert.Equal(RedJpeg, picture.Stream.Value);
        using var source = PdfSource.Open(saved, importSavedAnnotations: false);
        AssertRed(source.Render(2, scale: 1).PixelAt(300, 400));
    }

    [Fact]
    public void Save_SameImageTwice_EmbedsItOnce()
    {
        var data = BlueHalfTransparent();
        var saved = Save(document =>
        {
            document.AddAnnotation(document.Pages[1].Id, ImageAnnotation.Create(data, new PdfPoint(200, 200), 100));
            document.AddAnnotation(document.Pages[2].Id, ImageAnnotation.Create(data, new PdfPoint(200, 200), 100));
        });

        var first = AnnotationsOf(saved, 1).Single().Elements[AnnotationWriter.ImageKey];
        var second = AnnotationsOf(saved, 2).Single().Elements[AnnotationWriter.ImageKey];
        Assert.Equal(((PdfSharp.Pdf.Advanced.PdfReference)first!).ObjectNumber, ((PdfSharp.Pdf.Advanced.PdfReference)second!).ObjectNumber);
    }

    [Fact]
    public void Reopen_RestoresStampsAndImagesWithTheirPixels()
    {
        var stamp = StampAnnotation.Create("Geprüft", "06.11.2026 · A. Muster", "#15803D", new PdfPoint(300, 200), 180, angle: 345);
        var raw = ImageAnnotation.Create(BlueHalfTransparent(), new PdfPoint(200, 500), 120, angle: 15);
        var jpeg = ImageAnnotation.Create(new ImageData(16, 8, ImageEncoding.Jpeg, RedJpeg), new PdfPoint(400, 600), 80);
        var saved = Save(document =>
        {
            foreach (var annotation in new Annotation[] { stamp, raw, jpeg })
            {
                document.AddAnnotation(document.Pages[0].Id, annotation);
            }
        });

        using var source = PdfSource.Open(saved);
        var restored = source.CreateDocument().Annotations.For(1);

        Assert.Equal(AnnotationSerializer.Serialize(stamp), AnnotationSerializer.Serialize(restored[0]));
        var restoredRaw = Assert.IsType<ImageAnnotation>(restored[1]);
        Assert.Equal(raw.Bounds, restoredRaw.Bounds);
        Assert.Equal(15, restoredRaw.Angle, 6);
        Assert.Equal(raw.Image.Data, restoredRaw.Image.Data);
        Assert.Equal(raw.Image.Alpha, restoredRaw.Image.Alpha);
        var restoredJpeg = Assert.IsType<ImageAnnotation>(restored[2]);
        Assert.Equal(ImageEncoding.Jpeg, restoredJpeg.Image.Encoding);
        Assert.Equal(RedJpeg, restoredJpeg.Image.Data);
        // Eigene Annotationen zeichnet der Editor; die bereinigte Seite zeigt sie nicht doppelt
        using var original = PdfSource.Open(_file, importSavedAnnotations: false);
        Assert.Equal(original.Render(1, scale: 1).PixelAt(300, 200), source.Render(1, scale: 1).PixelAt(300, 200));
    }

    private static void AssertRed((byte R, byte G, byte B) pixel) =>
        Assert.True(pixel.R > 150 && pixel.G < 110 && pixel.B < 110, $"Rot erwartet, war {pixel}");

    /// <summary>Öffnet die Testdatei, wendet Änderungen an und speichert.</summary>
    private string Save(Action<EditableDocument> edit)
    {
        using var source = PdfSource.Open(_file);
        var document = new EditableDocument(source.PageCount);
        edit(document);
        var target = _temp.File("gespeichert.pdf");
        PdfExporter.Save(source, document, target);
        return target;
    }

    /// <summary>Annotationen einer Seite der gespeicherten Datei.</summary>
    private static List<PdfDictionary> AnnotationsOf(string path, int pageIndex)
    {
        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var annots = pdf.Pages[pageIndex].Elements.GetArray("/Annots");
        return annots is null
            ? []
            : annots.Elements.Select(item => item is PdfSharp.Pdf.Advanced.PdfReference reference ? (PdfDictionary)reference.Value : (PdfDictionary)item).ToList();
    }
}
