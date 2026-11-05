using System.Runtime.InteropServices;
using PDFiumCore;
using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.History;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Import;

namespace PdfUtopia.Pdf.Rendering;

/// <summary>
/// Eine geöffnete PDF-Datei zum Anzeigen: Seitenanzahl, Seitengrößen, Rendern und Text der Seiten.
/// Die Datei wird vollständig in den Speicher gelesen; sie bleibt dadurch nicht gesperrt und darf beim
/// Speichern überschrieben werden. Pfade mit Umlauten funktionieren auf allen Plattformen.
/// </summary>
public sealed class PdfSource : IDisposable
{
    /// <summary>PDFium-Flag: Annotationen mitzeichnen.</summary>
    private const int RenderAnnotations = 0x01;

    /// <summary>Abstand der Hinterlegung von Suchtreffern zum Text in Punkten (A36).</summary>
    private const double SearchHitPadding = 3;

    /// <summary>PDFium-Bitmapformat BGRA.</summary>
    private const int HasAlpha = 1;

    private readonly byte[] _data;
    private GCHandle _pin;
    private FpdfDocumentT? _document;
    private IReadOnlyList<Bookmark>? _bookmarks;

    /// <summary>
    /// Übernimmt ein geladenes PDFium-Dokument.
    /// </summary>
    private PdfSource(string path, string? password, LoadedDocument loaded, IReadOnlyDictionary<int, IReadOnlyList<Annotation>> savedAnnotations)
    {
        FilePath = path;
        Password = password;
        _data = loaded.Data;
        _pin = loaded.Pin;
        _document = loaded.Document;
        PageCount = fpdfview.FPDF_GetPageCount(loaded.Document);
        SavedAnnotations = savedAnnotations;
    }

    /// <summary>Pfad der Datei.</summary>
    public string FilePath { get; }

    /// <summary>Verwendetes Passwort oder <c>null</c>.</summary>
    public string? Password { get; }

    /// <summary>Seitenanzahl.</summary>
    public int PageCount { get; }

    /// <summary>
    /// Von PDF Utopia gespeicherte Annotationen je Seitennummer (1-basiert); sie sind aus der Anzeige entfernt
    /// und werden über <see cref="CreateDocument"/> wieder bearbeitbar (F23).
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<Annotation>> SavedAnnotations { get; }

    /// <summary>
    /// Lesezeichen (Inhaltsverzeichnis) der Datei als Baum (A46); leer, wenn die Datei keine hat.
    /// </summary>
    public IReadOnlyList<Bookmark> Bookmarks
    {
        get
        {
            if (_bookmarks is null)
            {
                lock (PdfiumLibrary.Sync)
                {
                    ObjectDisposedException.ThrowIf(_document is null, this);
                    _bookmarks = ReadBookmarks(_document, null, new HashSet<IntPtr>(), depth: 0);
                }
            }

            return _bookmarks;
        }
    }

    /// <summary>
    /// Inhalt der Datei beim Öffnen, bereinigt um eigene Annotationen und Seitenverlängerungen
    /// (Grundlage für Anzeige und Speichern).
    /// </summary>
    internal byte[] Data => _data;

    /// <summary>
    /// Öffnet eine PDF-Datei.
    /// </summary>
    /// <param name="path">Pfad.</param>
    /// <param name="password">Passwort für geschützte Dateien oder <c>null</c>.</param>
    /// <param name="importSavedAnnotations"><c>true</c> (Standard): eigene gespeicherte Annotationen werden
    /// eingelesen und aus der Anzeige entfernt; <c>false</c>: Datei wird unverändert angezeigt (z. B. Vorschau
    /// des gespeicherten Ergebnisses).</param>
    /// <returns>Die geöffnete Datei.</returns>
    /// <exception cref="PdfOpenException">Wenn die Datei nicht lesbar, beschädigt oder geschützt ist.</exception>
    public static PdfSource Open(string path, string? password = null, bool importSavedAnnotations = true)
    {
        var original = ReadFile(path);
        var loaded = Load(original, password, path);
        var import = importSavedAnnotations
            ? ImportSavedAnnotations(original, password)
            : new ImportResult(original, new Dictionary<int, IReadOnlyList<Annotation>>());
        if (ReferenceEquals(import.Data, original))
        {
            return new PdfSource(path, password, loaded, import.Annotations);
        }

        loaded.Close();
        return new PdfSource(path, password, Load(import.Data, password, path), import.Annotations);
    }

    /// <summary>
    /// Erzeugt das Bearbeitungsmodell mit den in der Datei gespeicherten Annotationen.
    /// </summary>
    /// <param name="undoLimit">Höchstzahl der Rückgängig-Schritte.</param>
    /// <returns>Das Modell.</returns>
    public EditableDocument CreateDocument(int undoLimit = UndoHistory.DefaultLimit)
    {
        var document = new EditableDocument(PageCount, undoLimit);
        foreach (var (pageNumber, annotations) in SavedAnnotations)
        {
            document.LoadAnnotations(document.Pages[pageNumber - 1].Id, annotations);
        }

        return document;
    }

    /// <summary>
    /// Liest die Datei vollständig ein.
    /// </summary>
    private static byte[] ReadFile(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PdfOpenException(PdfOpenError.FileNotReadable, path);
        }
    }

    /// <summary>
    /// Lädt Dateiinhalt in PDFium; prüft dabei Format und Passwort.
    /// </summary>
    private static LoadedDocument Load(byte[] data, string? password, string path)
    {
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        lock (PdfiumLibrary.Sync)
        {
            PdfiumLibrary.EnsureInitialized();
            var document = fpdfview.FPDF_LoadMemDocument(pin.AddrOfPinnedObject(), data.Length, password);
            if (document is null || document.__Instance == IntPtr.Zero)
            {
                var error = PdfiumLibrary.LastError();
                pin.Free();
                throw new PdfOpenException(error, path);
            }

            return new LoadedDocument(data, pin, document);
        }
    }

    /// <summary>
    /// Liest gespeicherte Annotationen ein. Kann die Datei dafür nicht verarbeitet werden, bleibt sie
    /// unverändert anzeigbar; die Annotationen sind dann nur nicht bearbeitbar.
    /// </summary>
    private static ImportResult ImportSavedAnnotations(byte[] data, string? password)
    {
        try
        {
            return SavedAnnotationImporter.Import(data, password);
        }
        catch (Exception exception) when (exception is PdfSharp.Pdf.IO.PdfReaderException or InvalidOperationException or NotImplementedException)
        {
            return new ImportResult(data, new Dictionary<int, IReadOnlyList<Annotation>>());
        }
    }

    /// <summary>
    /// Größe einer Seite in Punkten, wie sie angezeigt wird (Drehung aus der Datei berücksichtigt).
    /// </summary>
    /// <param name="sourcePageNumber">Seitennummer in der Datei (1-basiert).</param>
    /// <returns>Breite und Höhe.</returns>
    public (double Width, double Height) PageSize(int sourcePageNumber) =>
        WithPage(sourcePageNumber, page => ((double)fpdfview.FPDF_GetPageWidthF(page), (double)fpdfview.FPDF_GetPageHeightF(page)));

    /// <summary>
    /// Rendert eine Seite inklusive ihrer Annotationen auf weißem Grund.
    /// </summary>
    /// <param name="sourcePageNumber">Seitennummer in der Datei (1-basiert).</param>
    /// <param name="scale">Pixel je Punkt (1 = 72 dpi).</param>
    /// <param name="rotation">Zusätzliche Drehung aus der Bearbeitung.</param>
    /// <returns>Die Pixel.</returns>
    public RenderedPage Render(int sourcePageNumber, double scale, PageRotation rotation = PageRotation.None)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        return WithPage(sourcePageNumber, page =>
        {
            var width = Math.Max(1, (int)Math.Round(fpdfview.FPDF_GetPageWidthF(page) * scale));
            var height = Math.Max(1, (int)Math.Round(fpdfview.FPDF_GetPageHeightF(page) * scale));
            if (rotation is PageRotation.Clockwise90 or PageRotation.Clockwise270)
            {
                (width, height) = (height, width);
            }

            return RenderToPixels(page, width, height, (int)rotation / 90);
        });
    }

    /// <summary>
    /// Text einer Seite in Lesereihenfolge (für Suche und Tests).
    /// </summary>
    /// <param name="sourcePageNumber">Seitennummer in der Datei (1-basiert).</param>
    /// <returns>Der Text; leer bei Seiten ohne Textebene (z. B. Scans).</returns>
    public string PageText(int sourcePageNumber) => WithTextPage(sourcePageNumber, (textPage, _) => ReadText(textPage));

    /// <summary>
    /// Absätze einer Seite in Anzeige-Koordinaten (Ursprung links oben, Drehung aus der Datei berücksichtigt),
    /// für den Absatzvorschlag beim Kommentieren (F21).
    /// </summary>
    /// <param name="sourcePageNumber">Seitennummer in der Datei (1-basiert).</param>
    /// <returns>Die Absätze; leer bei Seiten ohne Textebene.</returns>
    public IReadOnlyList<PdfRect> TextBlocks(int sourcePageNumber) => WithTextPage(sourcePageNumber, (textPage, toDisplay) =>
    {
        var count = fpdf_text.FPDFTextCountChars(textPage);
        var boxes = Enumerable.Range(0, Math.Max(0, count)).Select(i => CharBox(textPage, i)).OfType<PdfRect>();
        return TextLayout.Paragraphs(boxes).Select(toDisplay).ToList();
    });

    /// <summary>
    /// Text in einem Bereich der Seite, z. B. unter einer Markierung (Anmerkungsübersicht, A45). Ein Zeichen zählt,
    /// wenn seine Mitte im Bereich liegt; Leerraum wird zu einzelnen Leerzeichen zusammengefasst.
    /// </summary>
    /// <param name="sourcePageNumber">Seitennummer in der Datei (1-basiert).</param>
    /// <param name="area">Bereich in Anzeige-Koordinaten (wie die Annotationen).</param>
    /// <returns>Der Text; leer, wenn dort kein Text steht (z. B. bei Scans).</returns>
    public string TextInArea(int sourcePageNumber, PdfRect area) => WithTextPage(sourcePageNumber, (textPage, toDisplay) =>
    {
        var text = ReadText(textPage);
        var inside = Enumerable.Range(0, text.Length)
            .Where(i => CharBox(textPage, i) is { } box && Contains(area, Center(toDisplay(box))))
            .ToList();
        if (inside.Count == 0)
        {
            return string.Empty;
        }

        var span = text[inside[0]..(inside[^1] + 1)];
        return string.Join(' ', span.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    });

    /// <summary>
    /// Fundstellen eines Suchbegriffs auf einer Seite (ohne Groß-/Kleinschreibung) zum Hinterlegen in der
    /// Akzentfarbe. Eine Fundstelle über einen Zeilenumbruch ergibt ein Rechteck je Zeile.
    /// </summary>
    /// <param name="sourcePageNumber">Seitennummer in der Datei (1-basiert).</param>
    /// <param name="term">Suchbegriff; leer ergibt keine Fundstellen.</param>
    /// <returns>Rechtecke in Anzeige-Koordinaten.</returns>
    public IReadOnlyList<PdfRect> FindText(int sourcePageNumber, string term)
    {
        var trimmed = term.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        return WithTextPage(sourcePageNumber, (textPage, toDisplay) =>
        {
            var text = ReadText(textPage);
            var hits = new List<PdfRect>();
            for (var at = text.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase); at >= 0;
                 at = text.IndexOf(trimmed, at + trimmed.Length, StringComparison.OrdinalIgnoreCase))
            {
                var boxes = Enumerable.Range(at, trimmed.Length).Select(i => CharBox(textPage, i)).OfType<PdfRect>();
                hits.AddRange(TextLayout.Lines(boxes).Select(toDisplay).Select(rect =>
                    new PdfRect(rect.X - SearchHitPadding, rect.Y - SearchHitPadding, rect.Width + 2 * SearchHitPadding, rect.Height + 2 * SearchHitPadding)));
            }

            return (IReadOnlyList<PdfRect>)hits;
        });
    }

    /// <summary>
    /// Anzahl der Fundstellen eines Suchbegriffs in einem Text (ohne Groß-/Kleinschreibung, wie <see cref="FindText"/>).
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <param name="term">Der Suchbegriff.</param>
    /// <returns>Die Anzahl.</returns>
    public static int CountOccurrences(string text, string term)
    {
        var trimmed = term.Trim();
        if (trimmed.Length == 0)
        {
            return 0;
        }

        var count = 0;
        for (var at = text.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = text.IndexOf(trimmed, at + trimmed.Length, StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }

    /// <summary>Mittelpunkt eines Rechtecks.</summary>
    private static PdfPoint Center(PdfRect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    /// <summary>Ob ein Punkt im Rechteck liegt.</summary>
    private static bool Contains(PdfRect rect, PdfPoint point) =>
        point.X >= rect.X && point.X <= rect.Right && point.Y >= rect.Y && point.Y <= rect.Bottom;

    /// <summary>
    /// Lädt die Textebene einer Seite und stellt eine Umrechnung von Schreibrichtungs- in Anzeige-Koordinaten bereit.
    /// </summary>
    private T WithTextPage<T>(int sourcePageNumber, Func<FpdfTextpageT, Func<PdfRect, PdfRect>, T> action) => WithPage(sourcePageNumber, page =>
    {
        var textPage = fpdf_text.FPDFTextLoadPage(page);
        try
        {
            var width = fpdfview.FPDF_GetPageWidthF(page);
            var height = fpdfview.FPDF_GetPageHeightF(page);
            PdfRect ToDisplayRect(PdfRect rect) =>
                PdfRect.FromCorners(ToDisplay(page, width, height, rect.X, -rect.Y), ToDisplay(page, width, height, rect.Right, -rect.Bottom));
            return action(textPage, ToDisplayRect);
        }
        finally
        {
            fpdf_text.FPDFTextClosePage(textPage);
        }
    });

    /// <summary>
    /// Umriss eines sichtbaren Zeichens in Schreibrichtung (Benutzerraum mit nach unten wachsendem Y, also −y),
    /// damit Zeilen auch auf in der Datei gedrehten Seiten waagerecht gruppiert werden.
    /// </summary>
    /// <returns>Der Umriss oder <c>null</c> bei Leerraum.</returns>
    private static PdfRect? CharBox(FpdfTextpageT textPage, int index)
    {
        if (char.IsWhiteSpace((char)fpdf_text.FPDFTextGetUnicode(textPage, index)))
        {
            return null;
        }

        double left = 0, right = 0, bottom = 0, top = 0;
        return fpdf_text.FPDFTextGetCharBox(textPage, index, ref left, ref right, ref bottom, ref top) != 0
            ? PdfRect.FromCorners(new PdfPoint(left, -top), new PdfPoint(right, -bottom))
            : null;
    }

    /// <summary>Höchste Verschachtelungstiefe von Lesezeichen (Schutz vor fehlerhaften Dateien).</summary>
    private const int MaxBookmarkDepth = 32;

    /// <summary>
    /// Liest die Unterpunkte eines Lesezeichens (bzw. die oberste Ebene). Bereits besuchte Lesezeichen werden
    /// übersprungen, damit fehlerhafte Dateien mit Zyklen keine Endlosschleife auslösen.
    /// </summary>
    private static List<Bookmark> ReadBookmarks(FpdfDocumentT document, FpdfBookmarkT? parent, HashSet<IntPtr> visited, int depth)
    {
        var result = new List<Bookmark>();
        if (depth > MaxBookmarkDepth)
        {
            return result;
        }

        for (var item = fpdf_doc.FPDFBookmarkGetFirstChild(document, parent);
             item is not null && item.__Instance != IntPtr.Zero && visited.Add(item.__Instance);
             item = fpdf_doc.FPDFBookmarkGetNextSibling(document, item))
        {
            result.Add(new Bookmark(
                BookmarkTitle(item),
                BookmarkPage(document, item),
                fpdf_doc.FPDFBookmarkGetCount(item) > 0,
                ReadBookmarks(document, item, visited, depth + 1)));
        }

        return result;
    }

    /// <summary>
    /// Titel eines Lesezeichens (UTF-16).
    /// </summary>
    private static string BookmarkTitle(FpdfBookmarkT bookmark)
    {
        var length = (int)fpdf_doc.FPDFBookmarkGetTitle(bookmark, IntPtr.Zero, 0);
        if (length <= 2)
        {
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            fpdf_doc.FPDFBookmarkGetTitle(bookmark, buffer, (ulong)length);
            return (Marshal.PtrToStringUni(buffer, length / 2 - 1) ?? string.Empty).Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Zielseite eines Lesezeichens (1-basiert): direktes Ziel oder Ziel einer „Gehe zu“-Aktion.
    /// </summary>
    private static int? BookmarkPage(FpdfDocumentT document, FpdfBookmarkT bookmark)
    {
        var destination = fpdf_doc.FPDFBookmarkGetDest(document, bookmark);
        if (destination is null || destination.__Instance == IntPtr.Zero)
        {
            var action = fpdf_doc.FPDFBookmarkGetAction(bookmark);
            destination = action is null || action.__Instance == IntPtr.Zero ? null : fpdf_doc.FPDFActionGetDest(document, action);
        }

        if (destination is null || destination.__Instance == IntPtr.Zero)
        {
            return null;
        }

        var index = fpdf_doc.FPDFDestGetDestPageIndex(document, destination);
        return index >= 0 ? index + 1 : null;
    }

    /// <summary>
    /// Text einer geladenen Textebene, ein Zeichen je Zeichenindex.
    /// </summary>
    private static string ReadText(FpdfTextpageT textPage)
    {
        var count = fpdf_text.FPDFTextCountChars(textPage);
        if (count <= 0)
        {
            return string.Empty;
        }

        var buffer = new ushort[count + 1];
        var written = fpdf_text.FPDFTextGetText(textPage, 0, count, ref buffer[0]);
        return new string(buffer.Take(Math.Max(0, written - 1)).Select(code => (char)code).ToArray());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (PdfiumLibrary.Sync)
        {
            if (_document is null)
            {
                return;
            }

            fpdfview.FPDF_CloseDocument(_document);
            _document = null;
            _pin.Free();
        }
    }

    /// <summary>
    /// Rechnet einen Punkt des PDF-Benutzerraums in Anzeige-Koordinaten um (PDFium berücksichtigt Seitenbereich
    /// und Drehung). PDFium liefert ganze Zahlen; mit zehnfachem Maßstab bleiben Zehntelpunkte erhalten.
    /// </summary>
    private static PdfPoint ToDisplay(FpdfPageT page, float width, float height, double x, double y)
    {
        const int precision = 10;
        int deviceX = 0, deviceY = 0;
        fpdfview.FPDF_PageToDevice(page, 0, 0, (int)Math.Round(width * precision), (int)Math.Round(height * precision), 0, x, y, ref deviceX, ref deviceY);
        return new PdfPoint(deviceX / (double)precision, deviceY / (double)precision);
    }

    /// <summary>
    /// Rendert eine geladene Seite in ein BGRA-Pixelfeld.
    /// </summary>
    private static RenderedPage RenderToPixels(FpdfPageT page, int width, int height, int quarterTurns)
    {
        var bitmap = fpdfview.FPDFBitmapCreate(width, height, HasAlpha);
        try
        {
            fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
            fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, quarterTurns, RenderAnnotations);
            var stride = fpdfview.FPDFBitmapGetStride(bitmap);
            var pixels = new byte[stride * height];
            Marshal.Copy(fpdfview.FPDFBitmapGetBuffer(bitmap), pixels, 0, pixels.Length);
            return new RenderedPage(width, height, stride, pixels);
        }
        finally
        {
            fpdfview.FPDFBitmapDestroy(bitmap);
        }
    }

    /// <summary>
    /// Lädt eine Seite, führt eine Aktion unter der PDFium-Sperre aus und schließt die Seite wieder.
    /// </summary>
    private T WithPage<T>(int sourcePageNumber, Func<FpdfPageT, T> action)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourcePageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourcePageNumber, PageCount);
        lock (PdfiumLibrary.Sync)
        {
            ObjectDisposedException.ThrowIf(_document is null, this);
            var page = fpdfview.FPDF_LoadPage(_document, sourcePageNumber - 1);
            try
            {
                return action(page);
            }
            finally
            {
                fpdfview.FPDF_ClosePage(page);
            }
        }
    }

    /// <summary>
    /// In PDFium geladener Dateiinhalt.
    /// </summary>
    /// <param name="Data">Der Inhalt (bleibt angeheftet, solange PDFium ihn nutzt).</param>
    /// <param name="Pin">Anheftung im Speicher.</param>
    /// <param name="Document">Das PDFium-Dokument.</param>
    private sealed record LoadedDocument(byte[] Data, GCHandle Pin, FpdfDocumentT Document)
    {
        /// <summary>
        /// Schließt das Dokument und gibt den Speicher frei.
        /// </summary>
        public void Close()
        {
            lock (PdfiumLibrary.Sync)
            {
                fpdfview.FPDF_CloseDocument(Document);
                Pin.Free();
            }
        }
    }
}
