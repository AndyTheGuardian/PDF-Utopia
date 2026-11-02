using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Drawing;

/// <summary>
/// Kubisches Bézier-Kurvenstück von <paramref name="Start"/> nach <paramref name="End"/>.
/// </summary>
/// <param name="Start">Anfangspunkt.</param>
/// <param name="Control1">Erster Kontrollpunkt.</param>
/// <param name="Control2">Zweiter Kontrollpunkt.</param>
/// <param name="End">Endpunkt.</param>
/// <param name="Width">Strichstärke dieses Stücks (bei Druckempfindlichkeit je Stück verschieden).</param>
public readonly record struct BezierSegment(PdfPoint Start, PdfPoint Control1, PdfPoint Control2, PdfPoint End, double Width);

/// <summary>
/// Berechnet die Darstellung eines Freihandstrichs: Glättung als Catmull-Rom-Kurve (umgerechnet in
/// Bézier-Stücke) und die Strichstärke aus dem Andruck. Wird von Oberfläche und PDF-Export gleich verwendet,
/// damit gespeicherte Striche genauso aussehen wie beim Zeichnen.
/// </summary>
public static class StrokeGeometry
{
    /// <summary>Strichstärke-Faktor bei Andruck 0.</summary>
    private const double MinPressureFactor = 0.3;

    /// <summary>Zuwachs des Faktors bis Andruck 1 (Faktor dann 1,7; bei 0,5 genau 1).</summary>
    private const double PressureFactorRange = 1.4;

    /// <summary>
    /// Strichstärke abhängig vom Andruck: 0 → 30 %, 0,5 → 100 %, 1 → 170 % der Grundstärke.
    /// </summary>
    /// <param name="baseWidth">Grund-Strichstärke.</param>
    /// <param name="pressure">Andruck 0–1.</param>
    /// <returns>Die Strichstärke.</returns>
    public static double PressureWidth(double baseWidth, float pressure) =>
        baseWidth * (MinPressureFactor + PressureFactorRange * Math.Clamp(pressure, 0f, 1f));

    /// <summary>
    /// Zerlegt einen Strich in Bézier-Stücke.
    /// </summary>
    /// <param name="stroke">Der Freihandstrich.</param>
    /// <returns>Ein Stück je Abschnitt zwischen zwei Punkten.</returns>
    public static IReadOnlyList<BezierSegment> ToSegments(InkAnnotation stroke) =>
        ToSegments(stroke.Points, stroke.Smoothing, stroke.Width, stroke.PressureSensitive);

    /// <summary>
    /// Zerlegt Punkte in Bézier-Stücke (Catmull-Rom). Glättung 0 ergibt gerade Stücke, 1 volle Rundung.
    /// </summary>
    /// <param name="points">Die Punkte.</param>
    /// <param name="smoothing">Glättung 0–1 (wird begrenzt).</param>
    /// <param name="baseWidth">Grund-Strichstärke.</param>
    /// <param name="pressureSensitive">Ob die Stärke dem Andruck am Ende des Stücks folgt.</param>
    /// <returns>Die Stücke; leer bei weniger als zwei Punkten.</returns>
    public static IReadOnlyList<BezierSegment> ToSegments(
        IReadOnlyList<StrokePoint> points, double smoothing, double baseWidth, bool pressureSensitive)
    {
        var factor = Math.Clamp(smoothing, 0, 1) / 6;
        var segments = new List<BezierSegment>(Math.Max(0, points.Count - 1));
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)].Position;
            var p1 = points[i].Position;
            var p2 = points[i + 1].Position;
            var p3 = points[Math.Min(points.Count - 1, i + 2)].Position;
            var control1 = new PdfPoint(p1.X + (p2.X - p0.X) * factor, p1.Y + (p2.Y - p0.Y) * factor);
            var control2 = new PdfPoint(p2.X - (p3.X - p1.X) * factor, p2.Y - (p3.Y - p1.Y) * factor);
            var width = pressureSensitive ? PressureWidth(baseWidth, points[i + 1].Pressure) : baseWidth;
            segments.Add(new BezierSegment(p1, control1, control2, p2, width));
        }

        return segments;
    }
}
