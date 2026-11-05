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
    /// <returns><c>true</c> für Marker, Freihand, Formen und Text.</returns>
    public static bool IsErasable(Annotation annotation) => annotation is InkAnnotation or HighlightAnnotation or RectangleAnnotation or TextAnnotation or LineAnnotation or EllipseAnnotation;

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
        LineAnnotation line => HitsLine(line, point, radius + line.Width / 2),
        EllipseAnnotation ellipse => DistanceToEllipse(ellipse.Bounds, point) <= radius + ellipse.Width / 2,
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
    /// Ob der Radierer eine Linie oder die Schenkel ihrer Pfeilspitze berührt.
    /// </summary>
    private static bool HitsLine(LineAnnotation line, PdfPoint point, double tolerance)
    {
        if (DistanceToSegment(point, line.Start, line.End) <= tolerance)
        {
            return true;
        }

        return line.Heads.Any(head =>
            DistanceToSegment(point, head.Tip, head.Left) <= tolerance || DistanceToSegment(point, head.Tip, head.Right) <= tolerance
            || (line.Arrow == ArrowStyle.Closed && (DistanceToSegment(point, head.Left, head.Right) <= tolerance || InTriangle(point, head.Tip, head.Left, head.Right))));
    }

    /// <summary>
    /// Ob ein Punkt im Dreieck liegt (gefüllte Pfeilspitze).
    /// </summary>
    private static bool InTriangle(PdfPoint point, PdfPoint a, PdfPoint b, PdfPoint c)
    {
        static double Side(PdfPoint p, PdfPoint from, PdfPoint to) => (to.X - from.X) * (p.Y - from.Y) - (to.Y - from.Y) * (p.X - from.X);
        var (ab, bc, ca) = (Side(point, a, b), Side(point, b, c), Side(point, c, a));
        return (ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0);
    }

    /// <summary>
    /// Ungefährer Abstand zum Umriss einer Ellipse: entlang des Strahls vom Mittelpunkt durch den Punkt.
    /// </summary>
    private static double DistanceToEllipse(PdfRect bounds, PdfPoint point)
    {
        var (radiusX, radiusY) = (bounds.Width / 2, bounds.Height / 2);
        var center = new PdfPoint(bounds.X + radiusX, bounds.Y + radiusY);
        if (radiusX <= 0 || radiusY <= 0)
        {
            return DistanceToSegment(point, new PdfPoint(bounds.X, bounds.Y), new PdfPoint(bounds.Right, bounds.Bottom));
        }

        var scaled = Math.Sqrt(Math.Pow((point.X - center.X) / radiusX, 2) + Math.Pow((point.Y - center.Y) / radiusY, 2));
        return scaled == 0 ? Math.Min(radiusX, radiusY) : Math.Abs(1 - 1 / scaled) * point.DistanceTo(center);
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
