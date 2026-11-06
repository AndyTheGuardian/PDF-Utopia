using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Signatures;

/// <summary>
/// Eine gespeicherte Unterschrift (A49): Striche, wie sie im Unterschriftenfeld geschrieben wurden.
/// </summary>
/// <param name="Name">Anzeigename, z. B. „Unterschrift“ oder „Kürzel“.</param>
/// <param name="ColorHex">Farbe.</param>
/// <param name="PressureSensitive">Ob die Strichstärke dem Andruck folgt.</param>
/// <param name="Strokes">Striche in Koordinaten des Unterschriftenfelds.</param>
public sealed record Signature(string Name, string ColorHex, bool PressureSensitive, IReadOnlyList<IReadOnlyList<StrokePoint>> Strokes)
{
    /// <summary>Höchstzahl gespeicherter Unterschriften (F31).</summary>
    public const int MaxCount = 5;

    /// <summary>Breite des Unterschriftenfelds.</summary>
    public const double PadWidth = 480;

    /// <summary>Höhe des Unterschriftenfelds.</summary>
    public const double PadHeight = 200;

    /// <summary>Strichstärke im Unterschriftenfeld; auf der Seite wird sie mit der Unterschrift skaliert.</summary>
    public const double PadStrokeWidth = 3;

    /// <summary>Glättung der Striche (wie Freihand mit 50 %).</summary>
    public const double Smoothing = 0.5;

    /// <summary>Breite einer per Klick gesetzten Unterschrift in Punkten.</summary>
    public const double DefaultPlacedWidth = 150;

    /// <summary>Kleinste Breite einer aufgezogenen Unterschrift.</summary>
    public const double MinPlacedWidth = 30;

    /// <summary>Wählbare Farben: Dunkelblau (Standard), Schwarz, Blau, Dunkelrot, Rot.</summary>
    public static readonly IReadOnlyList<string> Colors = ["#1E3A8A", "#111827", "#2563EB", "#991B1B", "#DC2626"];

    /// <summary>Ob die Unterschrift Striche hat.</summary>
    public bool HasStrokes => Strokes.Any(stroke => stroke.Count > 1);

    /// <summary>Umschließendes Rechteck der Striche (mindestens 1 × 1).</summary>
    public PdfRect Bounds => StrokeBounds(Strokes);

    /// <summary>
    /// Höhe bei einer Breite, mit dem Seitenverhältnis der Unterschrift.
    /// </summary>
    /// <param name="width">Die Breite.</param>
    /// <returns>Die Höhe.</returns>
    public double HeightFor(double width) => width * Bounds.Height / Bounds.Width;

    /// <summary>
    /// Umschließendes Rechteck von Strichen (mindestens 1 × 1, damit nie durch null geteilt wird).
    /// </summary>
    /// <param name="strokes">Die Striche.</param>
    /// <returns>Das Rechteck.</returns>
    public static PdfRect StrokeBounds(IEnumerable<IReadOnlyList<StrokePoint>> strokes)
    {
        var points = strokes.SelectMany(stroke => stroke).Select(point => point.Position).ToList();
        if (points.Count == 0)
        {
            return new PdfRect(0, 0, 1, 1);
        }

        var (left, top) = (points.Min(point => point.X), points.Min(point => point.Y));
        return new PdfRect(left, top, Math.Max(1, points.Max(point => point.X) - left), Math.Max(1, points.Max(point => point.Y) - top));
    }
}
