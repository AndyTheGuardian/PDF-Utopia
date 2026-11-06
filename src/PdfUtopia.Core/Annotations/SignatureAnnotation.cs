using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Signatures;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Auf eine Seite gesetzte Unterschrift (A49): ihre Striche in Koordinaten der ungedrehten Seite. Sie wird als Ganzes
/// verschoben und radiert. Sichtbare Unterschrift, keine digitale Signatur.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Farbe.</param>
/// <param name="Width">Strichstärke auf der Seite.</param>
/// <param name="Strokes">Striche auf der Seite.</param>
/// <param name="PressureSensitive">Ob die Strichstärke dem Andruck folgt.</param>
public sealed record SignatureAnnotation(
    Guid Id, string ColorHex, double Width, IReadOnlyList<IReadOnlyList<StrokePoint>> Strokes, bool PressureSensitive)
    : Annotation(Id, ColorHex)
{
    /// <summary>Umschließendes Rechteck der Striche.</summary>
    public PdfRect Bounds => Signature.StrokeBounds(Strokes);

    /// <summary>
    /// Setzt eine gespeicherte Unterschrift in ein Rechteck der Anzeige; die Höhe folgt dem Seitenverhältnis.
    /// </summary>
    /// <param name="signature">Die Unterschrift.</param>
    /// <param name="left">Linker Rand in der Anzeige.</param>
    /// <param name="top">Oberer Rand in der Anzeige.</param>
    /// <param name="width">Breite in der Anzeige.</param>
    /// <param name="toPage">Rechnet Anzeige- in Seitenkoordinaten um (Seitendrehung).</param>
    /// <returns>Die Anmerkung.</returns>
    public static SignatureAnnotation Place(Signature signature, double left, double top, double width, Func<PdfPoint, PdfPoint> toPage)
    {
        var bounds = signature.Bounds;
        var scale = width / bounds.Width;
        var strokes = signature.Strokes.Where(stroke => stroke.Count > 1)
            .Select(stroke => (IReadOnlyList<StrokePoint>)stroke.Select(point => point with
            {
                Position = toPage(new PdfPoint(left + (point.Position.X - bounds.X) * scale, top + (point.Position.Y - bounds.Y) * scale)),
            }).ToList())
            .ToList();
        return new SignatureAnnotation(Guid.NewGuid(), signature.ColorHex, Signature.PadStrokeWidth * scale, strokes, signature.PressureSensitive);
    }

    /// <summary>
    /// Verschiebt die Unterschrift.
    /// </summary>
    /// <param name="dx">Waagerecht.</param>
    /// <param name="dy">Senkrecht.</param>
    /// <returns>Die verschobene Unterschrift (gleiche Kennung).</returns>
    public SignatureAnnotation Translate(double dx, double dy) => this with
    {
        Strokes = Strokes.Select(stroke => (IReadOnlyList<StrokePoint>)stroke
            .Select(point => point with { Position = new PdfPoint(point.Position.X + dx, point.Position.Y + dy) }).ToList()).ToList(),
    };
}
