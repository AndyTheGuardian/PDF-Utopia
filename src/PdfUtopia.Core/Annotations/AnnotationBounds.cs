using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Umschließendes Rechteck einer Anmerkung (mit Strichstärke), z. B. um sie in den sichtbaren Bereich zu holen
/// und hervorzuheben (A45).
/// </summary>
public static class AnnotationBounds
{
    /// <summary>
    /// Bestimmt das Rechteck.
    /// </summary>
    /// <param name="annotation">Die Anmerkung (Koordinaten wie gespeichert oder angezeigt).</param>
    /// <returns>Das Rechteck in denselben Koordinaten.</returns>
    public static PdfRect Of(Annotation annotation) => annotation switch
    {
        InkAnnotation ink => Inflate(Enclose(ink.Points.Select(point => point.Position)), ink.Width / 2),
        HighlightAnnotation highlight => highlight.Bounds,
        RectangleAnnotation rectangle => Inflate(rectangle.Bounds, rectangle.Width / 2),
        EllipseAnnotation ellipse => Inflate(ellipse.Bounds, ellipse.Width / 2),
        LineAnnotation line => Inflate(line.Bounds, line.Width / 2),
        CommentAnnotation comment => comment.Bounds,
        TextAnnotation text => text.Bounds,
        SignatureAnnotation signature => Inflate(signature.Bounds, signature.Width),
        BoxAnnotation box => box.Envelope,
        RedactionAnnotation redaction => redaction.Bounds,
        _ => throw new NotSupportedException($"Unbekannte Annotation {annotation.GetType().Name}."),
    };

    /// <summary>Kleinstes Rechteck um alle Punkte.</summary>
    private static PdfRect Enclose(IEnumerable<PdfPoint> points)
    {
        var list = points.ToList();
        return list.Count == 0
            ? default
            : PdfRect.FromCorners(new PdfPoint(list.Min(p => p.X), list.Min(p => p.Y)), new PdfPoint(list.Max(p => p.X), list.Max(p => p.Y)));
    }

    /// <summary>Vergrößert ein Rechteck auf allen Seiten.</summary>
    private static PdfRect Inflate(PdfRect rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + 2 * amount, rect.Height + 2 * amount);
}
