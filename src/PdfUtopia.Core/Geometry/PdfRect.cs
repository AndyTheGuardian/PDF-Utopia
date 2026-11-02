namespace PdfUtopia.Core.Geometry;

/// <summary>
/// Rechteck auf einer Seite in PDF-Punkten (Ursprung links oben, siehe <see cref="PdfPoint"/>).
/// Breite und Höhe sind nie negativ.
/// </summary>
public readonly record struct PdfRect
{
    /// <summary>
    /// Erzeugt ein Rechteck.
    /// </summary>
    /// <param name="x">Linker Rand.</param>
    /// <param name="y">Oberer Rand.</param>
    /// <param name="width">Breite (≥ 0).</param>
    /// <param name="height">Höhe (≥ 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei negativer Breite oder Höhe.</exception>
    public PdfRect(double x, double y, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Linker Rand.</summary>
    public double X { get; }

    /// <summary>Oberer Rand.</summary>
    public double Y { get; }

    /// <summary>Breite.</summary>
    public double Width { get; }

    /// <summary>Höhe.</summary>
    public double Height { get; }

    /// <summary>Rechter Rand.</summary>
    public double Right => X + Width;

    /// <summary>Unterer Rand.</summary>
    public double Bottom => Y + Height;

    /// <summary>
    /// Erzeugt ein Rechteck aus zwei beliebigen gegenüberliegenden Ecken, z. B. Start- und Endpunkt beim Aufziehen.
    /// </summary>
    /// <param name="first">Erste Ecke.</param>
    /// <param name="second">Gegenüberliegende Ecke.</param>
    /// <returns>Das Rechteck.</returns>
    public static PdfRect FromCorners(PdfPoint first, PdfPoint second) =>
        new(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));
}
