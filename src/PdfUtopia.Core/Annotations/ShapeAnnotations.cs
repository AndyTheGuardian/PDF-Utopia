using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Annotations;

/// <summary>Formen des Werkzeugs „Formen“ (A44).</summary>
public enum ShapeKind
{
    /// <summary>Rechteck mit abgerundeten Ecken.</summary>
    Rectangle,

    /// <summary>Ellipse.</summary>
    Ellipse,

    /// <summary>Gerade Linie.</summary>
    Line,

    /// <summary>Linie mit offener Pfeilspitze am Ende.</summary>
    Arrow,

    /// <summary>Linie mit geschlossener, gefüllter Pfeilspitze am Ende.</summary>
    ClosedArrow,
}

/// <summary>Pfeilspitze am Ende einer Linie.</summary>
public enum ArrowStyle
{
    /// <summary>Keine Spitze.</summary>
    None,

    /// <summary>Offene Spitze aus zwei Schenkeln.</summary>
    Open,

    /// <summary>Geschlossene, gefüllte Spitze (Dreieck).</summary>
    Closed,
}

/// <summary>Wo eine Linie Pfeilspitzen trägt.</summary>
public enum ArrowEnds
{
    /// <summary>Am Ende (dort, wo beim Zeichnen losgelassen wurde).</summary>
    End,

    /// <summary>Am Anfang.</summary>
    Start,

    /// <summary>An beiden Enden.</summary>
    Both,
}

/// <summary>
/// Eine Pfeilspitze: Spitze und die Enden ihrer beiden Schenkel.
/// </summary>
/// <param name="Tip">Spitze (Endpunkt der Linie).</param>
/// <param name="Left">Ende des einen Schenkels.</param>
/// <param name="Right">Ende des anderen Schenkels.</param>
public readonly record struct ArrowHead(PdfPoint Tip, PdfPoint Left, PdfPoint Right)
{
    /// <summary>Mitte der Grundlinie; hier endet die Linie bei geschlossener Spitze.</summary>
    public PdfPoint BaseCenter => new((Left.X + Right.X) / 2, (Left.Y + Right.Y) / 2);
}

/// <summary>
/// Gerade Linie, optional mit offenen oder geschlossenen Pfeilspitzen am Anfang, am Ende oder an beiden (A44).
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Farbe.</param>
/// <param name="Width">Strichstärke.</param>
/// <param name="Start">Anfang (Koordinaten der ungedrehten Seite).</param>
/// <param name="End">Ende.</param>
/// <param name="Arrow">Art der Pfeilspitzen.</param>
/// <param name="Ends">Wo Pfeilspitzen sitzen (nur mit <paramref name="Arrow"/>).</param>
public sealed record LineAnnotation(
    Guid Id, string ColorHex, double Width, PdfPoint Start, PdfPoint End, ArrowStyle Arrow, ArrowEnds Ends = ArrowEnds.End)
    : Annotation(Id, ColorHex)
{
    /// <summary>Öffnungswinkel der Pfeilspitze zu jeder Seite.</summary>
    public const double ArrowAngleDegrees = 28;

    /// <summary>
    /// Länge der Pfeilspitzen-Schenkel; sie wächst mit der Strichstärke, damit die Spitze im Verhältnis bleibt.
    /// </summary>
    public double ArrowLength => 6 + 3 * Width;

    /// <summary>Ob am Anfang eine Pfeilspitze sitzt.</summary>
    public bool HasStartArrow => Arrow != ArrowStyle.None && Ends is ArrowEnds.Start or ArrowEnds.Both;

    /// <summary>Ob am Ende eine Pfeilspitze sitzt.</summary>
    public bool HasEndArrow => Arrow != ArrowStyle.None && Ends is ArrowEnds.End or ArrowEnds.Both;

    /// <summary>Alle Pfeilspitzen der Linie (keine, eine oder zwei).</summary>
    public IReadOnlyList<ArrowHead> Heads
    {
        get
        {
            var heads = new List<ArrowHead>();
            if (HasStartArrow)
            {
                heads.Add(HeadAt(Start, End));
            }

            if (HasEndArrow)
            {
                heads.Add(HeadAt(End, Start));
            }

            return heads;
        }
    }

    /// <summary>
    /// Anfang der gezeichneten Strecke: bei geschlossener Spitze am Anfang die Mitte ihrer Grundlinie, damit die Linie
    /// nicht über die Spitze hinausragt.
    /// </summary>
    public PdfPoint StrokeStart => Arrow == ArrowStyle.Closed && HasStartArrow ? HeadAt(Start, End).BaseCenter : Start;

    /// <summary>Ende der gezeichneten Strecke (entsprechend <see cref="StrokeStart"/>).</summary>
    public PdfPoint StrokeEnd => Arrow == ArrowStyle.Closed && HasEndArrow ? HeadAt(End, Start).BaseCenter : End;

    /// <summary>Umschließendes Rechteck aus Linie und Pfeilspitzen (ohne Strichstärke).</summary>
    public PdfRect Bounds
    {
        get
        {
            var points = new List<PdfPoint> { Start, End };
            foreach (var head in Heads)
            {
                points.Add(head.Left);
                points.Add(head.Right);
            }

            return PdfRect.FromCorners(
                new PdfPoint(points.Min(point => point.X), points.Min(point => point.Y)),
                new PdfPoint(points.Max(point => point.X), points.Max(point => point.Y)));
        }
    }

    /// <summary>
    /// Pfeilspitze an einem Ende; die Schenkel sind zur Linie zurückgeneigt.
    /// </summary>
    /// <param name="tip">Das Ende mit der Spitze.</param>
    /// <param name="from">Das andere Ende.</param>
    /// <returns>Die Spitze.</returns>
    private ArrowHead HeadAt(PdfPoint tip, PdfPoint from)
    {
        var angle = Math.Atan2(tip.Y - from.Y, tip.X - from.X);
        var spread = ArrowAngleDegrees * Math.PI / 180;
        PdfPoint Leg(double direction) => new(tip.X - ArrowLength * Math.Cos(direction), tip.Y - ArrowLength * Math.Sin(direction));
        return new ArrowHead(tip, Leg(angle - spread), Leg(angle + spread));
    }
}

/// <summary>
/// Ellipse in einem Rechteck (A44).
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Farbe.</param>
/// <param name="Width">Strichstärke.</param>
/// <param name="Bounds">Umschließendes Rechteck (Koordinaten der ungedrehten Seite).</param>
public sealed record EllipseAnnotation(Guid Id, string ColorHex, double Width, PdfRect Bounds) : Annotation(Id, ColorHex);

/// <summary>
/// Hilfen beim Zeichnen von Formen mit gedrückter Umschalttaste (A44).
/// </summary>
public static class ShapeConstraint
{
    /// <summary>Winkelraster für Linien und Pfeile.</summary>
    public const double AngleStepDegrees = 15;

    /// <summary>
    /// Richtet das Ende einer Linie auf das nächste 15°-Raster aus; die Länge bleibt.
    /// </summary>
    /// <param name="start">Anfang.</param>
    /// <param name="end">Gezogenes Ende.</param>
    /// <returns>Ausgerichtetes Ende.</returns>
    public static PdfPoint SnapAngle(PdfPoint start, PdfPoint end)
    {
        var length = start.DistanceTo(end);
        var step = AngleStepDegrees * Math.PI / 180;
        var angle = Math.Round(Math.Atan2(end.Y - start.Y, end.X - start.X) / step) * step;
        return new PdfPoint(start.X + length * Math.Cos(angle), start.Y + length * Math.Sin(angle));
    }

    /// <summary>
    /// Macht ein aufgezogenes Rechteck quadratisch (für Quadrat und Kreis); die größere Seite zählt.
    /// </summary>
    /// <param name="start">Feste Ecke.</param>
    /// <param name="end">Gezogene Ecke.</param>
    /// <returns>Gegenüberliegende Ecke des Quadrats.</returns>
    public static PdfPoint Square(PdfPoint start, PdfPoint end)
    {
        var size = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        return new PdfPoint(start.X + size * Sign(end.X - start.X), start.Y + size * Sign(end.Y - start.Y));
    }

    /// <summary>Richtung, bei 0 nach rechts bzw. unten.</summary>
    private static int Sign(double value) => value < 0 ? -1 : 1;
}
