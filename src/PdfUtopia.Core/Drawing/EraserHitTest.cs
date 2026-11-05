using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Drawing;

/// <summary>
/// Trefferprüfung des Radierers: Ein Radierstrich entfernt jede Markierung, die er berührt, vollständig
/// (Objekt-Radierer). Berücksichtigt werden Marker, Freihand und Rechtecke; Kommentare nicht – sie werden über
/// ihre Fußnotenzahl entfernt (A26, A27).
/// </summary>
public static class EraserHitTest
{
    /// <summary>Kleinster Radius in Punkten.</summary>
    public const double MinRadius = 2;

    /// <summary>Größter Radius in Punkten.</summary>
    public const double MaxRadius = 40;

    /// <summary>Standardradius in Punkten.</summary>
    public const double DefaultRadius = 12;

    /// <summary>
    /// Ob eine Annotation vom Radierer entfernt werden kann.
    /// </summary>
    /// <param name="annotation">Die Annotation.</param>
    /// <returns><c>true</c> für Marker, Freihand, Rechtecke und Text.</returns>
    public static bool IsErasable(Annotation annotation) => annotation is InkAnnotation or HighlightAnnotation or RectangleAnnotation or TextAnnotation;

    /// <summary>
    /// Ob der Radierer an einem Punkt die Annotation berührt.
    /// </summary>
    /// <param name="annotation">Die Annotation.</param>
    /// <param name="point">Mittelpunkt des Radierers.</param>
    /// <param name="radius">Radius des Radierers.</param>
    /// <returns><c>true</c> bei Berührung.</returns>
    public static bool Hits(Annotation annotation, PdfPoint point, double radius) => annotation switch
    {
        InkAnnotation ink => HitsStroke(ink, point, radius),
        HighlightAnnotation highlight => DistanceToArea(highlight.Bounds, point) <= radius,
        RectangleAnnotation rectangle => DistanceToOutline(rectangle.Bounds, point) <= radius + rectangle.Width / 2,
        TextAnnotation text => DistanceToArea(text.Bounds, point) <= radius,
        _ => false,
    };

    /// <summary>
    /// Alle Annotationen einer Seite, die ein Radierstrich berührt, in Zeichenreihenfolge.
    /// </summary>
    /// <param name="annotations">Annotationen der Seite.</param>
    /// <param name="path">Punkte des Radierstrichs.</param>
    /// <param name="radius">Radius des Radierers.</param>
    /// <returns>Die getroffenen Annotationen.</returns>
    public static IReadOnlyList<Annotation> HitsAlong(IEnumerable<Annotation> annotations, IReadOnlyList<PdfPoint> path, double radius) =>
        annotations.Where(annotation => IsErasable(annotation) && path.Any(point => Hits(annotation, point, radius))).ToList();

    /// <summary>
    /// Begrenzt den Radius auf den erlaubten Bereich.
    /// </summary>
    /// <param name="radius">Gewünschter Radius.</param>
    /// <returns>Radius zwischen <see cref="MinRadius"/> und <see cref="MaxRadius"/>.</returns>
    public static double ClampRadius(double radius) => Math.Clamp(radius, MinRadius, MaxRadius);

    /// <summary>
    /// Berührung eines Freihandstrichs: Abstand zu den Teilstücken höchstens Radius plus halbe Strichstärke.
    /// </summary>
    private static bool HitsStroke(InkAnnotation ink, PdfPoint point, double radius)
    {
        var tolerance = radius + StrokeGeometry.PressureWidth(ink.Width, 1f) / 2;
        if (ink.Points.Count == 1)
        {
            return ink.Points[0].Position.DistanceTo(point) <= tolerance;
        }

        for (var i = 1; i < ink.Points.Count; i++)
        {
            if (DistanceToSegment(point, ink.Points[i - 1].Position, ink.Points[i].Position) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Abstand eines Punkts zu einer Strecke.
    /// </summary>
    private static double DistanceToSegment(PdfPoint point, PdfPoint start, PdfPoint end)
    {
        var (dx, dy) = (end.X - start.X, end.Y - start.Y);
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
        return point.DistanceTo(new PdfPoint(start.X + t * dx, start.Y + t * dy));
    }

    /// <summary>
    /// Abstand zu einer Fläche (0 innerhalb).
    /// </summary>
    private static double DistanceToArea(PdfRect rect, PdfPoint point)
    {
        var dx = Math.Max(Math.Max(rect.X - point.X, 0), point.X - rect.Right);
        var dy = Math.Max(Math.Max(rect.Y - point.Y, 0), point.Y - rect.Bottom);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Abstand zum Rand eines Rechtecks; das Innere eines Rechtecks gehört nicht zur Markierung.
    /// </summary>
    private static double DistanceToOutline(PdfRect rect, PdfPoint point)
    {
        var outside = DistanceToArea(rect, point);
        if (outside > 0)
        {
            return outside;
        }

        return Math.Min(Math.Min(point.X - rect.X, rect.Right - point.X), Math.Min(point.Y - rect.Y, rect.Bottom - point.Y));
    }
}
