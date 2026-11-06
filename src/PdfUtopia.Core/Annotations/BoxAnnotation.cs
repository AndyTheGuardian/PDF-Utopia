using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Gemeinsame Geometrie von Stempeln und Bildern (A51): ein Rechteck, das um seine Mitte gedreht ist. Verschieben,
/// Größe ändern und Drehen ersetzen nur <see cref="Bounds"/> und <see cref="Angle"/>.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Farbe.</param>
/// <param name="Bounds">Ungedrehtes Rechteck; seine Mitte ist der Drehpunkt.</param>
/// <param name="Angle">Drehung in Grad im Uhrzeigersinn (0 bis unter 360).</param>
public abstract record BoxAnnotation(Guid Id, string ColorHex, PdfRect Bounds, double Angle) : Annotation(Id, ColorHex)
{
    /// <summary>Kleinste Breite in Punkten.</summary>
    public const double MinWidth = 30;

    /// <summary>Schrittweite beim Einrasten der Drehung in Grad (F37).</summary>
    public const double AngleStep = 15;

    /// <summary>Mittelpunkt (Drehpunkt).</summary>
    public PdfPoint Center => new(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);

    /// <summary>Ecken nach der Drehung: oben links, oben rechts, unten rechts, unten links (bezogen auf das ungedrehte Rechteck).</summary>
    public IReadOnlyList<PdfPoint> Corners =>
    [
        FromLocal(-Bounds.Width / 2, -Bounds.Height / 2), FromLocal(Bounds.Width / 2, -Bounds.Height / 2),
        FromLocal(Bounds.Width / 2, Bounds.Height / 2), FromLocal(-Bounds.Width / 2, Bounds.Height / 2),
    ];

    /// <summary>Achsenparalleles Rechteck um die gedrehten Ecken.</summary>
    public PdfRect Envelope
    {
        get
        {
            var corners = Corners;
            return PdfRect.FromCorners(
                new PdfPoint(corners.Min(p => p.X), corners.Min(p => p.Y)), new PdfPoint(corners.Max(p => p.X), corners.Max(p => p.Y)));
        }
    }

    /// <summary>
    /// Punkt relativ zur Mitte in Richtung der Kanten des ungedrehten Rechtecks (Umkehrung der Drehung).
    /// </summary>
    /// <param name="point">Punkt auf der Seite.</param>
    /// <returns>Versatz zur Mitte vor der Drehung.</returns>
    public PdfPoint ToLocal(PdfPoint point)
    {
        var (sin, cos) = Math.SinCos(-Angle * Math.PI / 180);
        var (dx, dy) = (point.X - Center.X, point.Y - Center.Y);
        return new PdfPoint(dx * cos - dy * sin, dx * sin + dy * cos);
    }

    /// <summary>
    /// Punkt zu einem Versatz von der Mitte in Richtung der Kanten (mit Drehung).
    /// </summary>
    /// <param name="right">Versatz entlang der Oberkante.</param>
    /// <param name="down">Versatz entlang der Seitenkante.</param>
    /// <returns>Punkt auf der Seite.</returns>
    public PdfPoint FromLocal(double right, double down)
    {
        var (sin, cos) = Math.SinCos(Angle * Math.PI / 180);
        return new PdfPoint(Center.X + right * cos - down * sin, Center.Y + right * sin + down * cos);
    }

    /// <summary>
    /// Ob ein Punkt im gedrehten Rechteck liegt.
    /// </summary>
    /// <param name="point">Der Punkt.</param>
    /// <param name="margin">Zusätzlicher Rand.</param>
    /// <returns><c>true</c> bei einem Treffer.</returns>
    public bool Contains(PdfPoint point, double margin = 0)
    {
        var local = ToLocal(point);
        return Math.Abs(local.X) <= Bounds.Width / 2 + margin && Math.Abs(local.Y) <= Bounds.Height / 2 + margin;
    }

    /// <summary>
    /// Abstand eines Punktes zum gedrehten Rechteck (0 innerhalb).
    /// </summary>
    /// <param name="point">Der Punkt.</param>
    /// <returns>Der Abstand.</returns>
    public double DistanceTo(PdfPoint point)
    {
        var local = ToLocal(point);
        var dx = Math.Max(Math.Abs(local.X) - Bounds.Width / 2, 0);
        var dy = Math.Max(Math.Abs(local.Y) - Bounds.Height / 2, 0);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Verschiebt das Rechteck.
    /// </summary>
    /// <param name="dx">Waagerecht.</param>
    /// <param name="dy">Senkrecht.</param>
    /// <returns>Kopie mit gleicher Kennung.</returns>
    public BoxAnnotation Translate(double dx, double dy) =>
        this with { Bounds = new PdfRect(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height) };

    /// <summary>
    /// Setzt Mitte, Breite und Drehung; die Höhe folgt dem Seitenverhältnis.
    /// </summary>
    /// <param name="center">Neue Mitte.</param>
    /// <param name="width">Neue Breite (mindestens <see cref="MinWidth"/>).</param>
    /// <param name="angle">Neue Drehung in Grad.</param>
    /// <returns>Kopie mit gleicher Kennung.</returns>
    public BoxAnnotation Place(PdfPoint center, double width, double angle)
    {
        width = Math.Max(MinWidth, width);
        var height = width * Bounds.Height / Bounds.Width;
        return this with { Bounds = Centered(center, width, height), Angle = NormalizeAngle(angle) };
    }

    /// <summary>
    /// Ändert die Größe über eine Ecke: die gegenüberliegende Ecke bleibt stehen, das Seitenverhältnis bleibt.
    /// </summary>
    /// <param name="corner">Gezogene Ecke (Index wie <see cref="Corners"/>).</param>
    /// <param name="pointer">Zeigerposition.</param>
    /// <returns>Kopie mit gleicher Kennung.</returns>
    public BoxAnnotation ResizeFrom(int corner, PdfPoint pointer)
    {
        var anchor = Corners[(corner + 2) % 4];
        // Abstand entlang der Diagonalen auf die Breite umrechnen
        var (sin, cos) = Math.SinCos(-Angle * Math.PI / 180);
        var (dx, dy) = (pointer.X - anchor.X, pointer.Y - anchor.Y);
        var localX = Math.Abs(dx * cos - dy * sin);
        var localY = Math.Abs(dx * sin + dy * cos);
        var ratio = Bounds.Height / Bounds.Width;
        var width = Math.Max(MinWidth, Math.Max(localX, localY / ratio));
        var height = width * ratio;
        var (signX, signY) = corner switch { 0 => (-1, -1), 1 => (1, -1), 2 => (1, 1), _ => (-1, 1) };
        var (rsin, rcos) = Math.SinCos(Angle * Math.PI / 180);
        var (hx, hy) = (signX * width / 2, signY * height / 2);
        var center = new PdfPoint(anchor.X + hx * rcos - hy * rsin, anchor.Y + hx * rsin + hy * rcos);
        return this with { Bounds = Centered(center, width, height) };
    }

    /// <summary>
    /// Drehwinkel, wenn der Drehgriff (über der Oberkante) an einen Punkt gezogen wird.
    /// </summary>
    /// <param name="pointer">Zeigerposition.</param>
    /// <param name="free">Stufenlos (Umschalt); sonst rastet der Winkel in 15°-Schritten ein.</param>
    /// <returns>Der Winkel in Grad (0 bis unter 360).</returns>
    public double AngleTowards(PdfPoint pointer, bool free)
    {
        // Der Griff sitzt bei 0° genau über der Mitte, also in Richtung −Y
        var angle = Math.Atan2(pointer.X - Center.X, Center.Y - pointer.Y) * 180 / Math.PI;
        return NormalizeAngle(free ? angle : Math.Round(angle / AngleStep) * AngleStep);
    }

    /// <summary>
    /// Bringt einen Winkel in den Bereich 0 bis unter 360 Grad.
    /// </summary>
    /// <param name="angle">Der Winkel.</param>
    /// <returns>Der Winkel im Bereich.</returns>
    public static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return Math.Abs(normalized - 360) < 1e-9 ? 0 : normalized;
    }

    /// <summary>
    /// Rechteck mit gegebener Mitte und Größe.
    /// </summary>
    /// <param name="center">Mitte.</param>
    /// <param name="width">Breite.</param>
    /// <param name="height">Höhe.</param>
    /// <returns>Das Rechteck.</returns>
    public static PdfRect Centered(PdfPoint center, double width, double height) =>
        new(center.X - width / 2, center.Y - height / 2, width, height);
}
