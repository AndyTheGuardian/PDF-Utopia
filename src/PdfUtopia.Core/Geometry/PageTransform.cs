using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Geometry;

/// <summary>
/// Umrechnung zwischen der ungedrehten Seite und ihrer Anzeige nach einer Drehung im Uhrzeigersinn.
/// Wird von der Oberfläche (Zeichnen auf einer gedrehten Seite) und beim Speichern (Annotationsdaten
/// in der neuen Ausrichtung) verwendet.
/// </summary>
public static class PageTransform
{
    /// <summary>
    /// Größe nach der Drehung.
    /// </summary>
    /// <param name="width">Breite der ungedrehten Seite.</param>
    /// <param name="height">Höhe der ungedrehten Seite.</param>
    /// <param name="rotation">Drehung.</param>
    /// <returns>Breite und Höhe der gedrehten Anzeige.</returns>
    public static (double Width, double Height) RotatedSize(double width, double height, PageRotation rotation) =>
        rotation is PageRotation.Clockwise90 or PageRotation.Clockwise270 ? (height, width) : (width, height);

    /// <summary>
    /// Rechnet einen Punkt der ungedrehten Seite in die gedrehte Anzeige um.
    /// </summary>
    /// <param name="point">Punkt auf der ungedrehten Seite.</param>
    /// <param name="rotation">Drehung.</param>
    /// <param name="width">Breite der ungedrehten Seite.</param>
    /// <param name="height">Höhe der ungedrehten Seite.</param>
    /// <returns>Punkt in der gedrehten Anzeige.</returns>
    public static PdfPoint Rotate(PdfPoint point, PageRotation rotation, double width, double height) => rotation switch
    {
        PageRotation.Clockwise90 => new PdfPoint(height - point.Y, point.X),
        PageRotation.Rotate180 => new PdfPoint(width - point.X, height - point.Y),
        PageRotation.Clockwise270 => new PdfPoint(point.Y, width - point.X),
        _ => point,
    };

    /// <summary>
    /// Rechnet einen Punkt der gedrehten Anzeige auf die ungedrehte Seite zurück (Umkehrung von
    /// <see cref="Rotate(PdfPoint, PageRotation, double, double)"/>).
    /// </summary>
    /// <param name="point">Punkt in der gedrehten Anzeige.</param>
    /// <param name="rotation">Drehung.</param>
    /// <param name="width">Breite der ungedrehten Seite.</param>
    /// <param name="height">Höhe der ungedrehten Seite.</param>
    /// <returns>Punkt auf der ungedrehten Seite.</returns>
    public static PdfPoint Unrotate(PdfPoint point, PageRotation rotation, double width, double height) => rotation switch
    {
        PageRotation.Clockwise90 => new PdfPoint(point.Y, height - point.X),
        PageRotation.Rotate180 => new PdfPoint(width - point.X, height - point.Y),
        PageRotation.Clockwise270 => new PdfPoint(width - point.Y, point.X),
        _ => point,
    };

    /// <summary>
    /// Rechnet ein Rechteck der ungedrehten Seite in die gedrehte Anzeige um.
    /// </summary>
    /// <param name="rect">Rechteck auf der ungedrehten Seite.</param>
    /// <param name="rotation">Drehung.</param>
    /// <param name="width">Breite der ungedrehten Seite.</param>
    /// <param name="height">Höhe der ungedrehten Seite.</param>
    /// <returns>Rechteck in der gedrehten Anzeige.</returns>
    public static PdfRect Rotate(PdfRect rect, PageRotation rotation, double width, double height) =>
        PdfRect.FromCorners(
            Rotate(new PdfPoint(rect.X, rect.Y), rotation, width, height),
            Rotate(new PdfPoint(rect.Right, rect.Bottom), rotation, width, height));

    /// <summary>
    /// Rechnet eine Annotation der ungedrehten Seite in die gedrehte Anzeige um.
    /// </summary>
    /// <param name="annotation">Die Annotation.</param>
    /// <param name="rotation">Drehung.</param>
    /// <param name="width">Breite der ungedrehten Seite.</param>
    /// <param name="height">Höhe der ungedrehten Seite.</param>
    /// <returns>Kopie mit gedrehter Lage (gleiche Kennung).</returns>
    public static Annotation Rotate(Annotation annotation, PageRotation rotation, double width, double height)
    {
        if (rotation == PageRotation.None)
        {
            return annotation;
        }

        return annotation switch
        {
            InkAnnotation ink => ink with
            {
                Points = ink.Points.Select(point => point with { Position = Rotate(point.Position, rotation, width, height) }).ToList(),
            },
            HighlightAnnotation highlight => highlight with { Bounds = Rotate(highlight.Bounds, rotation, width, height) },
            RectangleAnnotation rectangle => rectangle with { Bounds = Rotate(rectangle.Bounds, rotation, width, height) },
            CommentAnnotation comment => comment with { Bounds = Rotate(comment.Bounds, rotation, width, height) },
            LineAnnotation line => line with { Start = Rotate(line.Start, rotation, width, height), End = Rotate(line.End, rotation, width, height) },
            EllipseAnnotation ellipse => ellipse with { Bounds = Rotate(ellipse.Bounds, rotation, width, height) },
            TextAnnotation text => text with
            {
                Anchor = Rotate(text.Anchor, rotation, width, height),
                Orientation = (PageRotation)(((int)text.Orientation - (int)rotation + 360) % 360),
            },
            _ => throw new NotSupportedException($"Unbekannte Annotation {annotation.GetType().Name}."),
        };
    }
}
