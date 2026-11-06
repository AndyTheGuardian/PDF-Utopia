using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Umgang mit dem Passwortschutz beim Speichern (F22).
/// </summary>
public enum PasswordMode
{
    /// <summary>Wie die Quelldatei: geschützt mit dem beim Öffnen eingegebenen Passwort oder ungeschützt.</summary>
    KeepSource,

    /// <summary>Ohne Passwortschutz speichern.</summary>
    Remove,

    /// <summary>Mit einem neuen Passwort schützen (auch bei bisher ungeschützten Dateien).</summary>
    Change,
}

/// <summary>
/// Einstellungen für das Speichern.
/// </summary>
public sealed record ExportOptions
{
    /// <summary>Überschrift des Fußnotenbereichs in der Sprache der Oberfläche.</summary>
    public string FootnoteHeading { get; init; } = "Kommentare";

    /// <summary>Umgang mit dem Passwortschutz.</summary>
    public PasswordMode PasswordMode { get; init; } = PasswordMode.KeepSource;

    /// <summary>Neues Passwort bei <see cref="PasswordMode.Change"/>; sonst ohne Bedeutung.</summary>
    public string? NewPassword { get; init; }

    /// <summary>
    /// Passwort, mit dem die Zieldatei geschützt wird.
    /// </summary>
    /// <param name="sourcePassword">Passwort der Quelldatei oder <c>null</c>.</param>
    /// <returns>Das Passwort oder <c>null</c> für eine ungeschützte Datei.</returns>
    /// <exception cref="ArgumentException">Bei <see cref="PasswordMode.Change"/> ohne neues Passwort.</exception>
    public string? TargetPassword(string? sourcePassword) => PasswordMode switch
    {
        PasswordMode.Remove => null,
        PasswordMode.Change when string.IsNullOrEmpty(NewPassword) =>
            throw new ArgumentException("Zum Ändern des Passworts muss ein neues Passwort angegeben werden.", nameof(NewPassword)),
        PasswordMode.Change => NewPassword,
        _ => string.IsNullOrEmpty(sourcePassword) ? null : sourcePassword,
    };
}

/// <summary>
/// Speichert ein bearbeitetes Dokument als neue PDF-Datei („Speichern unter“, F7) oder extrahiert ausgewählte
/// Seiten in eine neue Datei. Übernimmt Reihenfolge, Drehung und Löschungen der Seitenliste, schreibt
/// Annotationen als echte PDF-Annotationen und verlängert Seiten mit Kommentaren für die Fußnoten.
/// Der Passwortschutz wird nach <see cref="ExportOptions.PasswordMode"/> behalten, geändert oder entfernt (F22).
/// </summary>
public static class PdfExporter
{
    /// <summary>
    /// Speichert alle Seiten in der aktuellen Reihenfolge.
    /// </summary>
    /// <param name="source">Die geöffnete Datei.</param>
    /// <param name="document">Das Bearbeitungsmodell.</param>
    /// <param name="targetPath">Zieldatei; darf die Quelldatei sein.</param>
    /// <param name="options">Exporteinstellungen.</param>
    public static void Save(PdfSource source, EditableDocument document, string targetPath, ExportOptions? options = null) =>
        Save([source], document, targetPath, options);

    /// <summary>
    /// Speichert alle Seiten in der aktuellen Reihenfolge, auch eingefügte Seiten anderer Dateien (A41).
    /// </summary>
    /// <param name="sources">Quelldateien nach <see cref="PageEntry.SourceIndex"/>; die erste ist die geöffnete Datei.</param>
    /// <param name="document">Das Bearbeitungsmodell.</param>
    /// <param name="targetPath">Zieldatei; darf die Quelldatei sein.</param>
    /// <param name="options">Exporteinstellungen; der Passwortschutz richtet sich nach der geöffneten Datei.</param>
    public static void Save(IReadOnlyList<PdfSource> sources, EditableDocument document, string targetPath, ExportOptions? options = null) =>
        Write(sources, document, document.Pages.ToList(), targetPath, options ?? new ExportOptions());

    /// <summary>
    /// Speichert ausgewählte Seiten in eine neue Datei, z. B. aus <see cref="PageSelectionParser"/>.
    /// </summary>
    /// <param name="source">Die geöffnete Datei.</param>
    /// <param name="document">Das Bearbeitungsmodell.</param>
    /// <param name="positions">Seitenpositionen der aktuellen Seitenliste (1-basiert), in Zielreihenfolge.</param>
    /// <param name="targetPath">Zieldatei.</param>
    /// <param name="options">Exporteinstellungen.</param>
    /// <exception cref="ArgumentException">Ohne Seiten.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Position außerhalb der Seitenliste.</exception>
    public static void Extract(
        PdfSource source, EditableDocument document, IReadOnlyList<int> positions, string targetPath, ExportOptions? options = null) =>
        Extract([source], document, positions, targetPath, options);

    /// <summary>
    /// Speichert ausgewählte Seiten in eine neue Datei, auch eingefügte Seiten anderer Dateien (A41).
    /// </summary>
    /// <param name="sources">Quelldateien nach <see cref="PageEntry.SourceIndex"/>; die erste ist die geöffnete Datei.</param>
    /// <param name="document">Das Bearbeitungsmodell.</param>
    /// <param name="positions">Seitenpositionen der aktuellen Seitenliste (1-basiert), in Zielreihenfolge.</param>
    /// <param name="targetPath">Zieldatei.</param>
    /// <param name="options">Exporteinstellungen.</param>
    /// <exception cref="ArgumentException">Ohne Seiten.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Position außerhalb der Seitenliste.</exception>
    public static void Extract(
        IReadOnlyList<PdfSource> sources, EditableDocument document, IReadOnlyList<int> positions, string targetPath, ExportOptions? options = null)
    {
        if (positions.Count == 0)
        {
            throw new ArgumentException("Es muss mindestens eine Seite ausgewählt sein.", nameof(positions));
        }

        foreach (var position in positions)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(position, document.Pages.Count);
        }

        Write(sources, document, positions.Select(position => document.Pages[position - 1]).ToList(), targetPath, options ?? new ExportOptions());
    }

    /// <summary>
    /// Baut das Zieldokument und speichert es. Jede benötigte Quelldatei wird einmal zum Import geöffnet.
    /// </summary>
    private static void Write(IReadOnlyList<PdfSource> sources, EditableDocument document, IReadOnlyList<PageEntry> entries, string targetPath, ExportOptions options)
    {
        if (sources.Count == 0)
        {
            throw new ArgumentException("Es muss mindestens die geöffnete Datei angegeben sein.", nameof(sources));
        }

        var targetPassword = options.TargetPassword(sources[PageEntry.OpenedFile].Password);
        var inputs = new Dictionary<int, PdfDocument>();
        try
        {
            using var output = new PdfDocument();
            var writer = new AnnotationWriter(output, options);
            var written = new List<PdfPage>();
            var origins = new List<(PdfPage Page, int SourceIndex)>();
            var redactions = new List<PageRedaction>();
            foreach (var entry in entries)
            {
                if (!inputs.TryGetValue(entry.SourceIndex, out var input))
                {
                    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(entry.SourceIndex, sources.Count, nameof(entry.SourceIndex));
                    input = OpenForImport(sources[entry.SourceIndex]);
                    inputs[entry.SourceIndex] = input;
                }

                var page = output.AddPage(input.Pages[entry.SourcePageNumber - 1]);
                var geometry = GeometryOf(page);
                var extension = writer.Write(page, geometry, document.Annotations.For(entry.Id), entry.Rotation);
                if (extension > 0)
                {
                    Extend(page, geometry, extension);
                }

                if (Redactions(document.Annotations.For(entry.Id), GeometryOf(page), entry.Rotation, written.Count) is { } redaction)
                {
                    redactions.Add(redaction);
                }

                page.Rotate = (geometry.FileRotation + (int)entry.Rotation) % 360;
                written.Add(page);
                origins.Add((page, entry.SourceIndex));
            }

            // Lesezeichen der geöffneten Datei auf die neue Seitenfolge umstellen (P6)
            AddBookmarks(output.Outlines, BookmarkMap.Prune(BookmarkMap.Place(sources[PageEntry.OpenedFile].Bookmarks, entries)), written);
            // Formular-Registrierung neu aufbauen, sonst sind Formularfelder in anderen Programmen nicht mehr ausfüllbar (P7)
            FormRebuilder.Rebuild(output, origins, inputs, document.Form);
            if (redactions.Count == 0)
            {
                Protect(output, targetPassword);
                SaveAtomically(output, targetPath);
                return;
            }

            // Schwärzen (A52): erst vollständig schreiben, dann die betroffenen Seiten als Bild ersetzen
            using var buffer = new MemoryStream();
            output.Save(buffer, closeStream: false);
            using var redacted = Redactor.Apply(buffer.ToArray(), redactions);
            Protect(redacted, targetPassword);
            SaveAtomically(redacted, targetPath);
        }
        finally
        {
            foreach (var input in inputs.Values)
            {
                input.Dispose();
            }
        }
    }

    /// <summary>
    /// Schwärzungen einer Seite, umgerechnet auf die gespeicherte Anzeige (Bearbeitungsdrehung und Verlängerung).
    /// </summary>
    /// <returns>Die Schwärzungen oder <c>null</c>, wenn die Seite keine hat.</returns>
    private static PageRedaction? Redactions(IReadOnlyList<Annotation> annotations, PageGeometry geometry, PageRotation rotation, int pageIndex)
    {
        var areas = annotations.OfType<RedactionAnnotation>()
            .Select(redaction => PageTransform.Rotate(redaction.Bounds, rotation, geometry.DisplayWidth, geometry.DisplayHeight))
            .ToList();
        return areas.Count == 0 ? null : new PageRedaction(pageIndex, areas);
    }

    /// <summary>
    /// Schreibt Lesezeichen samt Unterpunkten; Gliederungspunkte ohne Ziel bleiben ohne Ziel.
    /// </summary>
    private static void AddBookmarks(PdfOutlineCollection target, IReadOnlyList<PlacedBookmark> bookmarks, List<PdfPage> pages)
    {
        foreach (var bookmark in bookmarks)
        {
            var outline = new PdfOutline { Title = bookmark.Source.Title, Opened = bookmark.Source.IsOpen };
            if (bookmark.PageIndex is { } index)
            {
                outline.DestinationPage = pages[index];
            }

            target.Add(outline);
            AddBookmarks(outline.Outlines, bookmark.Children, pages);
        }
    }

    /// <summary>
    /// Öffnet eine Quelldatei aus dem Speicher zum Übernehmen von Seiten.
    /// </summary>
    private static PdfDocument OpenForImport(PdfSource source)
    {
        var stream = new MemoryStream(source.Data, writable: false);
        return source.Password is { } password
            ? PdfReader.Open(stream, password, PdfDocumentOpenMode.Import)
            : PdfReader.Open(stream, PdfDocumentOpenMode.Import);
    }

    /// <summary>
    /// Geometrie einer Seite: sichtbarer Bereich (CropBox, sonst MediaBox) und Drehung aus der Datei.
    /// </summary>
    private static PageGeometry GeometryOf(PdfPage page)
    {
        var box = page.Elements.ContainsKey("/CropBox") ? page.CropBox : page.MediaBox;
        return new PageGeometry(box.X1, box.Y1, box.X2, box.Y2, page.Rotate);
    }

    /// <summary>
    /// Verlängert die angezeigte Seite nach unten (MediaBox und, falls vorhanden, CropBox).
    /// </summary>
    private static void Extend(PdfPage page, PageGeometry geometry, double extension)
    {
        var marker = new PdfArray(page.Owner);
        marker.Elements.Add(new PdfReal(extension));
        marker.Elements.Add(new PdfInteger(geometry.FileRotation));
        page.Elements[AnnotationWriter.ExtensionKey] = marker;

        var (left, bottom, right, top) = geometry.ExtendedBox(extension);
        var media = page.MediaBox;
        page.MediaBox = Box(Math.Min(media.X1, left), Math.Min(media.Y1, bottom), Math.Max(media.X2, right), Math.Max(media.Y2, top));
        if (page.Elements.ContainsKey("/CropBox"))
        {
            page.CropBox = Box(left, bottom, right, top);
        }
    }

    /// <summary>
    /// Rechteck aus Rändern.
    /// </summary>
    private static PdfRectangle Box(double left, double bottom, double right, double top) =>
        new(new XPoint(left, bottom), new XPoint(right, top));

    /// <summary>
    /// Schützt das Zieldokument (Benutzer- und Besitzerpasswort gleich); <c>null</c> = ungeschützt.
    /// </summary>
    private static void Protect(PdfDocument output, string? password)
    {
        if (password is null)
        {
            return;
        }

        output.SecuritySettings.UserPassword = password;
        output.SecuritySettings.OwnerPassword = password;
    }

    /// <summary>
    /// Speichert erst in eine temporäre Datei und ersetzt dann das Ziel – ein Fehler beim Schreiben
    /// lässt eine vorhandene Datei unversehrt.
    /// </summary>
    private static void SaveAtomically(PdfDocument output, string targetPath)
    {
        var temporary = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            output.Save(temporary);
            File.Move(temporary, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
