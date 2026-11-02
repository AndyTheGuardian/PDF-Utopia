using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Umrechnung zwischen Anzeige-Koordinaten des Core (Ursprung links oben, Y nach unten, Seite so gedreht,
/// wie die Datei sie anzeigt) und dem PDF-Benutzerraum der Seite (Ursprung links unten, Y nach oben,
/// vor Anwendung von <c>/Rotate</c>). Die zusätzliche Drehung aus der Bearbeitung ist hier nicht enthalten;
/// sie wird über <c>/Rotate</c> gespeichert und dreht Annotationen mit.
/// </summary>
/// <param name="left">Linker Rand des sichtbaren Bereichs (CropBox, sonst MediaBox).</param>
/// <param name="bottom">Unterer Rand.</param>
/// <param name="right">Rechter Rand.</param>
/// <param name="top">Oberer Rand.</param>
/// <param name="fileRotation">Drehung der Seite in der Datei (0, 90, 180, 270).</param>
internal sealed class PageGeometry(double left, double bottom, double right, double top, int fileRotation)
{
    private readonly double _width = right - left;
    private readonly double _height = top - bottom;

    /// <summary>Drehung der Seite in der Datei (0, 90, 180, 270).</summary>
    public int FileRotation { get; } = Normalize(fileRotation);

    /// <summary>Angezeigte Breite.</summary>
    public double DisplayWidth => FileRotation is 90 or 270 ? _height : _width;

    /// <summary>Angezeigte Höhe.</summary>
    public double DisplayHeight => FileRotation is 90 or 270 ? _width : _height;

    /// <summary>
    /// Rechnet einen Anzeigepunkt in den Benutzerraum um. Punkte außerhalb der Seite (z. B. im verlängerten
    /// Bereich für Fußnoten) werden linear fortgesetzt.
    /// </summary>
    /// <param name="display">Punkt in Anzeige-Koordinaten.</param>
    /// <returns>Punkt im Benutzerraum.</returns>
    public PdfPoint ToUser(PdfPoint display)
    {
        var (x, y) = FileRotation switch
        {
            90 => (display.Y, display.X),
            180 => (_width - display.X, display.Y),
            270 => (_width - display.Y, _height - display.X),
            _ => (display.X, _height - display.Y),
        };
        return new PdfPoint(left + x, bottom + y);
    }

    /// <summary>
    /// Rechnet ein Anzeige-Rechteck in den Benutzerraum um.
    /// </summary>
    /// <param name="display">Rechteck in Anzeige-Koordinaten.</param>
    /// <returns>Linker, unterer, rechter und oberer Rand im Benutzerraum.</returns>
    public (double Left, double Bottom, double Right, double Top) ToUser(PdfRect display)
    {
        var a = ToUser(new PdfPoint(display.X, display.Y));
        var b = ToUser(new PdfPoint(display.Right, display.Bottom));
        return (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    /// <summary>
    /// Ecken eines Anzeige-Rechtecks im Benutzerraum in der Reihenfolge für <c>/QuadPoints</c>:
    /// oben links, oben rechts, unten links, unten rechts (bezogen auf die Anzeige).
    /// </summary>
    /// <param name="display">Rechteck in Anzeige-Koordinaten.</param>
    /// <returns>Acht Zahlen x1 y1 … x4 y4.</returns>
    public double[] QuadPoints(PdfRect display)
    {
        PdfPoint[] corners =
        [
            ToUser(new PdfPoint(display.X, display.Y)), ToUser(new PdfPoint(display.Right, display.Y)),
            ToUser(new PdfPoint(display.X, display.Bottom)), ToUser(new PdfPoint(display.Right, display.Bottom)),
        ];
        return corners.SelectMany(point => new[] { point.X, point.Y }).ToArray();
    }

    /// <summary>
    /// Matrix für Erscheinungsbilder: Inhalt wird in Anzeige-Ausrichtung gezeichnet und gegen die Dateidrehung
    /// zurückgedreht, damit er auf der angezeigten Seite aufrecht steht.
    /// </summary>
    /// <returns>Die sechs Matrixwerte a b c d e f.</returns>
    public double[] AppearanceMatrix() => FileRotation switch
    {
        90 => [0, 1, -1, 0, 0, 0],
        180 => [-1, 0, 0, -1, 0, 0],
        270 => [0, -1, 1, 0, 0, 0],
        _ => [1, 0, 0, 1, 0, 0],
    };

    /// <summary>
    /// Sichtbarer Bereich nach Verlängerung der angezeigten Seite nach unten.
    /// </summary>
    /// <param name="extension">Zusätzliche Höhe in Punkten.</param>
    /// <returns>Linker, unterer, rechter und oberer Rand im Benutzerraum.</returns>
    public (double Left, double Bottom, double Right, double Top) ExtendedBox(double extension) => FileRotation switch
    {
        90 => (left, bottom, right + extension, top),
        180 => (left, bottom, right, top + extension),
        270 => (left - extension, bottom, right, top),
        _ => (left, bottom - extension, right, top),
    };

    /// <summary>
    /// Bringt eine Drehung auf 0, 90, 180 oder 270.
    /// </summary>
    private static int Normalize(int rotation) => ((rotation % 360) + 360) % 360 / 90 * 90;
}
