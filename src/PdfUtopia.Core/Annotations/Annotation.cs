using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Gemeinsame Basis aller Annotationen einer Seite. Annotationen sind unveränderlich; eine Änderung
/// (z. B. ein neuer Kommentartext) ersetzt sie durch eine Kopie mit derselben <see cref="Id"/>.
/// Koordinaten in PDF-Punkten, Ursprung links oben auf der ungedrehten Seite.
/// </summary>
/// <param name="Id">Eindeutige Kennung.</param>
/// <param name="ColorHex">Farbe im Format <c>#RRGGBB</c>.</param>
public abstract record Annotation(Guid Id, string ColorHex);

/// <summary>
/// Punkt eines Freihandstrichs mit dem wirksamen Andruck (0–1).
/// </summary>
/// <param name="Position">Position auf der Seite.</param>
/// <param name="Pressure">Andruck; fehlte er beim Gerät, steht hier der Mittelwert 0,5.</param>
public readonly record struct StrokePoint(PdfPoint Position, float Pressure);

/// <summary>
/// Freihandzeichnung.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Strichfarbe.</param>
/// <param name="Width">Grund-Strichstärke in Punkten.</param>
/// <param name="Points">Die aufgezeichneten Punkte (mindestens zwei).</param>
/// <param name="Smoothing">Glättung 0–1 (0 = gerade Stücke).</param>
/// <param name="PressureSensitive">Ob die Strichstärke dem Andruck folgt.</param>
public sealed record InkAnnotation(
    Guid Id, string ColorHex, double Width, IReadOnlyList<StrokePoint> Points, double Smoothing, bool PressureSensitive)
    : Annotation(Id, ColorHex);

/// <summary>
/// Textmarker: waagerechter, halbtransparenter Balken wie bei einem echten Textmarker.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Markerfarbe.</param>
/// <param name="Bounds">Überdeckte Fläche.</param>
public sealed record HighlightAnnotation(Guid Id, string ColorHex, PdfRect Bounds) : Annotation(Id, ColorHex);

/// <summary>
/// Rechteck um ein Objekt.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Rahmenfarbe.</param>
/// <param name="Width">Strichstärke in Punkten.</param>
/// <param name="Bounds">Lage und Größe.</param>
public sealed record RectangleAnnotation(Guid Id, string ColorHex, double Width, PdfRect Bounds) : Annotation(Id, ColorHex);

/// <summary>
/// Kommentar zu einem Textabschnitt: farbig umrandet und hinterlegt, mit Fußnotenzahl oben rechts
/// und dem Text als Fußnote auf der verlängerten Seite.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Farbe von Rahmen, Hinterlegung und Fußnotenzahl.</param>
/// <param name="Bounds">Kommentierter Abschnitt.</param>
/// <param name="Text">Fußnotentext.</param>
public sealed record CommentAnnotation(Guid Id, string ColorHex, PdfRect Bounds, string Text) : Annotation(Id, ColorHex);
