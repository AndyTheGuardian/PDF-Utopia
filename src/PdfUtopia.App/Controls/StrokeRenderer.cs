using Avalonia;
using Avalonia.Media;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using BezierSegment = PdfUtopia.Core.Drawing.BezierSegment;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Zeichnet Striche wie beim Speichern (gleiche Glättung und druckabhängige Stärke), für Unterschriften (A49).
/// </summary>
public static class StrokeRenderer
{
    /// <summary>
    /// Zeichnet Striche.
    /// </summary>
    /// <param name="context">Zeichenfläche.</param>
    /// <param name="strokes">Die Striche.</param>
    /// <param name="colorHex">Farbe.</param>
    /// <param name="width">Grund-Strichstärke.</param>
    /// <param name="pressureSensitive">Ob die Stärke dem Andruck folgt.</param>
    /// <param name="smoothing">Glättung 0–1.</param>
    public static void Draw(DrawingContext context, IEnumerable<IReadOnlyList<StrokePoint>> strokes, string colorHex, double width, bool pressureSensitive, double smoothing)
    {
        var brush = new SolidColorBrush(Color.Parse(colorHex));
        foreach (var points in strokes.Where(points => points.Count > 1))
        {
            var segments = StrokeGeometry.ToSegments(points, smoothing, width, pressureSensitive);
            if (pressureSensitive)
            {
                foreach (var segment in segments)
                {
                    context.DrawGeometry(null, Pen(brush, segment.Width), Curve([segment]));
                }
            }
            else
            {
                context.DrawGeometry(null, Pen(brush, width), Curve(segments));
            }
        }
    }

    /// <summary>Stift mit runden Enden.</summary>
    private static Pen Pen(IBrush brush, double width) => new(brush, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    /// <summary>Kurvenzug aus Bézier-Stücken.</summary>
    private static StreamGeometry Curve(IReadOnlyList<BezierSegment> segments)
    {
        var geometry = new StreamGeometry();
        if (segments.Count == 0)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(new Point(segments[0].Start.X, segments[0].Start.Y), false);
        foreach (var segment in segments)
        {
            context.CubicBezierTo(
                new Point(segment.Control1.X, segment.Control1.Y), new Point(segment.Control2.X, segment.Control2.Y), new Point(segment.End.X, segment.End.Y));
        }

        context.EndFigure(false);
        return geometry;
    }
}
