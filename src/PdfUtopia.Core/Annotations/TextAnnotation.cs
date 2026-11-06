using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Text;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Freier Text auf der Seite (Text-Werkzeug, A43). Der Text wird in Helvetica auf <paramref name="Width"/>
/// umgebrochen; die Höhe ergibt sich aus der Zeilenzahl.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Schriftfarbe.</param>
/// <param name="Anchor">Linke obere Ecke des Textfelds in Leserichtung (Koordinaten der ungedrehten Seite).</param>
/// <param name="Width">Zeilenbreite in Punkten.</param>
/// <param name="FontSize">Schriftgröße in Punkten.</param>
/// <param name="Text">Der Text.</param>
/// <param name="Orientation">Seitendrehung, bei der der Text aufrecht steht (die Drehung beim Anlegen).</param>
public sealed record TextAnnotation(
    Guid Id, string ColorHex, PdfPoint Anchor, double Width, double FontSize, string Text, PageRotation Orientation = PageRotation.None)
    : Annotation(Id, ColorHex)
{
    /// <summary>Kleinste Schriftgröße.</summary>
    public const double MinFontSize = 8;

    /// <summary>Größte Schriftgröße.</summary>
    public const double MaxFontSize = 36;

    /// <summary>Standard-Schriftgröße.</summary>
    public const double DefaultFontSize = 12;

    /// <summary>Breite eines per Klick gesetzten Textfelds.</summary>
    public const double DefaultWidth = 200;

    /// <summary>Kleinste Breite eines aufgezogenen Textfelds.</summary>
    public const double MinWidth = 24;

    /// <summary>Zeilenabstand als Vielfaches der Schriftgröße.</summary>
    public const double LineSpacing = 1.2;

    /// <summary>Zeilenhöhe in Punkten.</summary>
    public double LineHeight => FontSize * LineSpacing;

    /// <summary>Umbrochene Zeilen.</summary>
    public IReadOnlyList<string> Lines => TextWrapper.Wrap(Text, Width, FontSize);

    /// <summary>Höhe des Textfelds in Punkten (mindestens eine Zeile).</summary>
    public double Height => Lines.Count * LineHeight;

    /// <summary>
    /// Abstand der Grundlinie einer Zeile vom oberen Rand des Textfelds (in Leserichtung).
    /// </summary>
    /// <param name="line">Zeile (0-basiert).</param>
    /// <returns>Der Abstand in Punkten.</returns>
    public double Baseline(int line) => line * LineHeight + (LineHeight - FontSize) / 2 + FontSize * Helvetica.Ascent / 1000;

    /// <summary>
    /// Punkt im Textfeld (in Leserichtung: rechts, nach unten) auf der Seite.
    /// </summary>
    /// <param name="right">Abstand vom linken Rand des Textfelds.</param>
    /// <param name="down">Abstand vom oberen Rand des Textfelds.</param>
    /// <returns>Der Punkt in Koordinaten der ungedrehten Seite.</returns>
    public PdfPoint ToPage(double right, double down)
    {
        var (across, along) = Axes(Orientation);
        return new PdfPoint(Anchor.X + right * across.X + down * along.X, Anchor.Y + right * across.Y + down * along.Y);
    }

    /// <summary>Umschließendes Rechteck auf der Seite.</summary>
    public PdfRect Bounds => PdfRect.FromCorners(ToPage(0, 0), ToPage(Width, Height));

    /// <summary>
    /// Richtungen von Leserichtung (rechts) und Zeilenfolge (nach unten) auf der ungedrehten Seite, wenn der Text bei
    /// der angegebenen Seitendrehung aufrecht steht.
    /// </summary>
    /// <param name="orientation">Die Drehung.</param>
    /// <returns>Beide Richtungen als Einheitsvektoren.</returns>
    public static (PdfPoint Across, PdfPoint Along) Axes(PageRotation orientation) => orientation switch
    {
        PageRotation.Clockwise90 => (new PdfPoint(0, -1), new PdfPoint(1, 0)),
        PageRotation.Rotate180 => (new PdfPoint(-1, 0), new PdfPoint(0, -1)),
        PageRotation.Clockwise270 => (new PdfPoint(0, 1), new PdfPoint(-1, 0)),
        _ => (new PdfPoint(1, 0), new PdfPoint(0, 1)),
    };

    /// <summary>
    /// Begrenzt eine Schriftgröße auf den erlaubten Bereich.
    /// </summary>
    /// <param name="size">Gewünschte Größe.</param>
    /// <returns>Größe zwischen 8 und 36 Punkten.</returns>
    public static double ClampFontSize(double size) => Math.Clamp(size, MinFontSize, MaxFontSize);
}
