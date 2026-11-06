using System.IO.Compression;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Schwärzungen einer Seite der gespeicherten Datei.
/// </summary>
/// <param name="PageIndex">Position der Seite (0-basiert).</param>
/// <param name="Areas">Bereiche in Punkten, bezogen auf die angezeigte Seite (mit ihrer Drehung, Ursprung links oben).</param>
internal sealed record PageRedaction(int PageIndex, IReadOnlyList<PdfRect> Areas);

/// <summary>
/// Schwärzt Seiten sicher (A52, F38): Jede betroffene Seite wird samt Anmerkungen und Formularfeldern als Bild
/// gerendert, die Bereiche werden in den Bildpunkten schwarz gefüllt, und die Seite wird durch das Bild ersetzt.
/// Text, Schriften, Vektorgrafik, Bilder, Anmerkungen und Formularfelder dieser Seite sind danach nicht mehr in der
/// Datei; ebenso die Strukturinformationen (Tagged PDF), die Text als Ersatztext enthalten könnten.
/// </summary>
internal static class Redactor
{
    /// <summary>Auflösung des Seitenbildes.</summary>
    public const double Dpi = 300;

    private const double PointsPerInch = 72;

    /// <summary>
    /// Wendet die Schwärzungen auf eine gespeicherte (ungeschützte) Datei an.
    /// </summary>
    /// <param name="data">Inhalt der Datei.</param>
    /// <param name="redactions">Schwärzungen je Seite.</param>
    /// <returns>Das geänderte Dokument zum Speichern.</returns>
    public static PdfDocument Apply(byte[] data, IReadOnlyList<PageRedaction> redactions)
    {
        var images = new Dictionary<int, (RenderedPage Pixels, double Width, double Height)>();
        using (var source = PdfSource.FromData(data))
        {
            var scale = Dpi / PointsPerInch;
            foreach (var redaction in redactions)
            {
                var pageNumber = redaction.PageIndex + 1;
                var (width, height) = source.PageSize(pageNumber);
                var pixels = source.Render(pageNumber, scale);
                foreach (var area in redaction.Areas)
                {
                    FillBlack(pixels, area, pixels.Width / width, pixels.Height / height);
                }

                images[redaction.PageIndex] = (pixels, width, height);
            }
        }

        var document = PdfReader.Open(new MemoryStream(data, writable: false), PdfDocumentOpenMode.Modify);
        var removedAnnotations = new HashSet<int>();
        foreach (var (index, image) in images)
        {
            ReplaceWithImage(document, document.Pages[index], image.Pixels, image.Width, image.Height, removedAnnotations);
        }

        PruneFormFields(document, removedAnnotations);
        // Strukturbaum und Markierungen können Text als Ersatztext enthalten
        document.Internals.Catalog.Elements.Remove("/StructTreeRoot");
        document.Internals.Catalog.Elements.Remove("/MarkInfo");
        return document;
    }

    /// <summary>
    /// Füllt einen Bereich schwarz; Pixel, die er nur teilweise bedeckt, werden mitgefüllt.
    /// </summary>
    private static void FillBlack(RenderedPage page, PdfRect area, double scaleX, double scaleY)
    {
        var left = Math.Clamp((int)Math.Floor(area.X * scaleX), 0, page.Width);
        var right = Math.Clamp((int)Math.Ceiling(area.Right * scaleX), 0, page.Width);
        var top = Math.Clamp((int)Math.Floor(area.Y * scaleY), 0, page.Height);
        var bottom = Math.Clamp((int)Math.Ceiling(area.Bottom * scaleY), 0, page.Height);
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var offset = y * page.Stride + x * 4;
                page.Pixels[offset] = 0;
                page.Pixels[offset + 1] = 0;
                page.Pixels[offset + 2] = 0;
                page.Pixels[offset + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Ersetzt den Inhalt einer Seite durch ein Bild in ihrer angezeigten Größe. Die Seite ist danach ungedreht und
    /// so groß wie die bisherige Anzeige; alles andere auf der Seite entfällt.
    /// </summary>
    private static void ReplaceWithImage(PdfDocument document, PdfPage page, RenderedPage pixels, double width, double height, HashSet<int> removedAnnotations)
    {
        if (page.Elements.GetArray("/Annots") is { } annotations)
        {
            foreach (var item in annotations.Elements.OfType<PdfReference>())
            {
                removedAnnotations.Add(item.ObjectNumber);
            }
        }

        foreach (var key in new[]
                 {
                     "/Annots", "/Thumb", "/PieceInfo", "/Metadata", "/StructParents", "/B", "/AA", "/CropBox", "/BleedBox", "/TrimBox", "/ArtBox",
                     "/Group", AnnotationWriter.ExtensionKey,
                 })
        {
            page.Elements.Remove(key);
        }

        var picture = new PdfDictionary(document);
        picture.Elements.SetName("/Type", "/XObject");
        picture.Elements.SetName("/Subtype", "/Image");
        picture.Elements.SetInteger("/Width", pixels.Width);
        picture.Elements.SetInteger("/Height", pixels.Height);
        picture.Elements.SetName("/ColorSpace", "/DeviceRGB");
        picture.Elements.SetInteger("/BitsPerComponent", 8);
        picture.Elements.SetName("/Filter", "/FlateDecode");
        picture.CreateStream(Deflate(ToRgb(pixels)));
        document.Internals.AddObject(picture);

        var objects = new PdfDictionary(document);
        objects.Elements["/Im0"] = picture.Reference;
        var resources = new PdfDictionary(document);
        resources.Elements["/XObject"] = objects;
        page.Elements["/Resources"] = resources;

        var content = new PdfDictionary(document);
        content.CreateStream(new ContentBuilder().Save().Transform(width, 0, 0, height, 0, 0).Op("/Im0 Do").Restore().ToBytes());
        document.Internals.AddObject(content);
        page.Elements["/Contents"] = content.Reference;

        var box = new PdfArray(document);
        foreach (var value in new[] { 0, 0, width, height })
        {
            box.Elements.Add(new PdfReal(value));
        }

        page.Elements["/MediaBox"] = box;
        page.Elements.SetInteger("/Rotate", 0);
    }

    /// <summary>
    /// Entfernt Formularfelder, deren Widgets auf geschwärzten Seiten lagen, aus der Formular-Registrierung.
    /// </summary>
    private static void PruneFormFields(PdfDocument document, HashSet<int> removedAnnotations)
    {
        if (removedAnnotations.Count == 0 || document.Internals.Catalog.Elements.GetDictionary("/AcroForm") is not { } form
            || form.Elements.GetArray("/Fields") is not { } fields)
        {
            return;
        }

        Prune(fields, removedAnnotations);
        if (fields.Elements.Count == 0)
        {
            document.Internals.Catalog.Elements.Remove("/AcroForm");
        }
    }

    /// <summary>
    /// Entfernt entfallene Widgets und dadurch leer gewordene Felder aus einer Feldliste.
    /// </summary>
    private static void Prune(PdfArray array, HashSet<int> removed)
    {
        for (var i = array.Elements.Count - 1; i >= 0; i--)
        {
            if (array.Elements[i] is not PdfReference reference)
            {
                continue;
            }

            if (removed.Contains(reference.ObjectNumber))
            {
                array.Elements.RemoveAt(i);
                continue;
            }

            if (reference.Value is PdfDictionary field && field.Elements.GetArray("/Kids") is { } kids)
            {
                Prune(kids, removed);
                if (kids.Elements.Count == 0)
                {
                    array.Elements.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Wandelt BGRA-Pixel in RGB-Bytes.
    /// </summary>
    private static byte[] ToRgb(RenderedPage page)
    {
        var rgb = new byte[page.Width * page.Height * 3];
        for (var y = 0; y < page.Height; y++)
        {
            for (var x = 0; x < page.Width; x++)
            {
                var source = y * page.Stride + x * 4;
                var target = (y * page.Width + x) * 3;
                rgb[target] = page.Pixels[source + 2];
                rgb[target + 1] = page.Pixels[source + 1];
                rgb[target + 2] = page.Pixels[source];
            }
        }

        return rgb;
    }

    /// <summary>
    /// Komprimiert Daten im zlib-Format (PDF-Filter „FlateDecode“).
    /// </summary>
    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }
}
