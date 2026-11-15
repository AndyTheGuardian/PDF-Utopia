namespace PdfUtopia.Core.Compression;

/// <summary>
/// Stufe der Komprimierung (A57): Bilder werden auf eine Auflösung verkleinert und als JPEG neu gespeichert.
/// </summary>
/// <param name="Dpi">Höchste Bildauflösung bezogen auf die Seitengröße.</param>
/// <param name="JpegQuality">JPEG-Qualität (1–100).</param>
public sealed record CompressionPreset(int Dpi, int JpegQuality)
{
    /// <summary>Qualität: 150 dpi, JPEG 85 % (F49).</summary>
    public static CompressionPreset Quality { get; } = new(150, 85);

    /// <summary>Sparsam: 96 dpi, JPEG 60 % (F49).</summary>
    public static CompressionPreset Economy { get; } = new(96, 60);

    /// <summary>
    /// Höchste Kantenlänge eines Bildes in Pixeln, damit es auf einer Seite dieser Größe die Auflösung nicht
    /// überschreitet.
    /// </summary>
    /// <param name="pageSidePoints">Längste Seitenkante in Punkten (1/72 Zoll).</param>
    /// <returns>Kantenlänge in Pixeln, mindestens 1.</returns>
    public int MaxPixels(double pageSidePoints) => Math.Max(1, (int)Math.Ceiling(pageSidePoints / 72 * Dpi));

    /// <summary>
    /// Zielgröße eines Bildes: verkleinert mit gleichem Seitenverhältnis, nie vergrößert.
    /// </summary>
    /// <param name="width">Breite in Pixeln.</param>
    /// <param name="height">Höhe in Pixeln.</param>
    /// <param name="pageSidePoints">Längste Seitenkante in Punkten.</param>
    /// <returns>Neue Breite und Höhe.</returns>
    public (int Width, int Height) TargetSize(int width, int height, double pageSidePoints)
    {
        var limit = MaxPixels(pageSidePoints);
        var longest = Math.Max(width, height);
        if (longest <= limit)
        {
            return (width, height);
        }

        var factor = (double)limit / longest;
        return (Math.Max(1, (int)Math.Round(width * factor)), Math.Max(1, (int)Math.Round(height * factor)));
    }

    /// <summary>
    /// Ob eine komprimierte Größe eine nennenswerte Ersparnis ist (unter 98 % des Originals, wie im Mockup v11).
    /// </summary>
    /// <param name="original">Originalgröße in Bytes.</param>
    /// <param name="compressed">Komprimierte Größe in Bytes.</param>
    /// <returns><c>true</c>, wenn sich das Speichern lohnt.</returns>
    public static bool IsWorthwhile(long original, long compressed) => original > 0 && compressed < original * 0.98;

    /// <summary>
    /// Ersparnis in ganzen Prozent.
    /// </summary>
    /// <param name="original">Originalgröße in Bytes.</param>
    /// <param name="compressed">Komprimierte Größe in Bytes.</param>
    /// <returns>Prozent (0, wenn nichts gespart wird).</returns>
    public static int SavingPercent(long original, long compressed) =>
        original <= 0 || compressed >= original ? 0 : (int)Math.Round(100.0 * (original - compressed) / original);
}
