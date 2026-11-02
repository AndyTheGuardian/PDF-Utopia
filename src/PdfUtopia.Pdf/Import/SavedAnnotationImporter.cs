using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Pdf.Export;

namespace PdfUtopia.Pdf.Import;

/// <summary>
/// Ergebnis des Einlesens: bereinigte Datei und die darin gespeicherten Annotationen von PDF Utopia.
/// </summary>
/// <param name="Data">Dateiinhalt ohne eigene Annotationen und ohne Seitenverlängerung.</param>
/// <param name="Annotations">Eingelesene Annotationen je Seitennummer (1-basiert) in Zeichenreihenfolge.</param>
internal sealed record ImportResult(byte[] Data, IReadOnlyDictionary<int, IReadOnlyList<Annotation>> Annotations);

/// <summary>
/// Liest die von PDF Utopia gespeicherten Annotationen aus einer Datei zurück, damit sie wieder bearbeitbar
/// sind (F23). Entfernt dabei die eigenen Annotationen (samt Fußnoten und Überschrift) und nimmt die
/// Seitenverlängerung zurück – sonst würden sie doppelt angezeigt und beim erneuten Speichern doppelt
/// geschrieben. Fremde Annotationen anderer Programme bleiben unverändert (A25).
/// </summary>
internal static class SavedAnnotationImporter
{
    private static readonly byte[] Marker = "/PdfUtopia"u8.ToArray();

    /// <summary>
    /// Liest die Annotationen ein und bereinigt die Datei.
    /// </summary>
    /// <param name="data">Dateiinhalt.</param>
    /// <param name="password">Passwort oder <c>null</c>.</param>
    /// <returns>Bereinigter Inhalt und Annotationen; ohne eigene Annotationen der unveränderte Inhalt.</returns>
    public static ImportResult Import(byte[] data, string? password)
    {
        var none = new ImportResult(data, new Dictionary<int, IReadOnlyList<Annotation>>());
        if (data.AsSpan().IndexOf(Marker) < 0)
        {
            return none;
        }

        using var document = Open(data, password);
        var annotations = new Dictionary<int, IReadOnlyList<Annotation>>();
        var changed = false;
        for (var index = 0; index < document.PageCount; index++)
        {
            var page = document.Pages[index];
            var (own, removed) = TakeOwnAnnotations(page);
            changed |= removed | RemoveExtension(page);
            if (own.Count > 0)
            {
                annotations[index + 1] = own;
            }
        }

        if (!changed)
        {
            return none;
        }

        using var cleaned = new MemoryStream();
        document.Save(cleaned, closeStream: false);
        return new ImportResult(cleaned.ToArray(), annotations);
    }

    /// <summary>
    /// Öffnet die Datei zum Ändern.
    /// </summary>
    private static PdfDocument Open(byte[] data, string? password)
    {
        var stream = new MemoryStream(data, writable: false);
        return password is null
            ? PdfReader.Open(stream, PdfDocumentOpenMode.Modify)
            : PdfReader.Open(stream, password, PdfDocumentOpenMode.Modify);
    }

    /// <summary>
    /// Entfernt alle eigenen Annotationen einer Seite und liefert die lesbaren Daten in Zeichenreihenfolge.
    /// </summary>
    /// <returns>Die Annotationen und ob etwas entfernt wurde.</returns>
    private static (List<Annotation> Own, bool Removed) TakeOwnAnnotations(PdfPage page)
    {
        var own = new List<Annotation>();
        var array = page.Elements.GetArray("/Annots");
        if (array is null)
        {
            return (own, false);
        }

        var removed = false;
        for (var i = 0; i < array.Elements.Count; i++)
        {
            if (Resolve(array.Elements[i]) is not { } dictionary || !dictionary.Elements.GetBoolean(AnnotationWriter.OwnerKey))
            {
                continue;
            }

            if (AnnotationSerializer.Deserialize(dictionary.Elements.GetString(AnnotationWriter.DataKey)) is { } annotation)
            {
                own.Add(annotation);
            }

            array.Elements.RemoveAt(i--);
            removed = true;
        }

        if (array.Elements.Count == 0)
        {
            page.Elements.Remove("/Annots");
        }

        return (own, removed);
    }

    /// <summary>
    /// Nimmt eine von PDF Utopia angelegte Seitenverlängerung zurück.
    /// </summary>
    /// <returns><c>true</c>, wenn die Seite verlängert war.</returns>
    private static bool RemoveExtension(PdfPage page)
    {
        var marker = page.Elements.GetArray(AnnotationWriter.ExtensionKey);
        if (marker is null)
        {
            return false;
        }

        var extension = marker.Elements.GetReal(0);
        var rotation = marker.Elements.GetInteger(1);
        page.MediaBox = Shrink(page.MediaBox, rotation, extension);
        if (page.Elements.ContainsKey("/CropBox"))
        {
            page.CropBox = Shrink(page.CropBox, rotation, extension);
        }

        page.Elements.Remove(AnnotationWriter.ExtensionKey);
        return true;
    }

    /// <summary>
    /// Verkleinert einen Bereich um die Verlängerung (Umkehrung von <see cref="PageGeometry.ExtendedBox"/>).
    /// </summary>
    private static PdfRectangle Shrink(PdfRectangle box, int rotation, double extension)
    {
        var (left, bottom, right, top) = new PageGeometry(box.X1, box.Y1, box.X2, box.Y2, rotation).ExtendedBox(-extension);
        return new PdfRectangle(new PdfSharp.Drawing.XPoint(left, bottom), new PdfSharp.Drawing.XPoint(right, top));
    }

    /// <summary>
    /// Löst einen Verweis auf ein Wörterbuch auf.
    /// </summary>
    private static PdfDictionary? Resolve(PdfItem item) => item switch
    {
        PdfReference reference => reference.Value as PdfDictionary,
        PdfDictionary dictionary => dictionary,
        _ => null,
    };
}
