namespace PdfUtopia.Core.Geometry;

/// <summary>
/// Anpassung der Seite an den Anzeigebereich.
/// </summary>
public enum FitMode
{
    /// <summary>Kein Anpassen: Zoom wird von Hand gewählt.</summary>
    None,

    /// <summary>Volle Höhe: Seitenhöhe füllt den Bereich.</summary>
    Height,

    /// <summary>Volle Breite: Seitenbreite füllt den Bereich.</summary>
    Width,

    /// <summary>Ganze Seite: die ganze Seite ist sichtbar.</summary>
    Page,
}

/// <summary>
/// Berechnet den Zoom, mit dem eine Seite in den Anzeigebereich passt (A33).
/// </summary>
public static class ZoomFit
{
    /// <summary>Kleinster Zoom.</summary>
    public const double MinZoom = 0.3;

    /// <summary>Größter Zoom.</summary>
    public const double MaxZoom = 4;

    /// <summary>
    /// Zoom für eine Anpassung.
    /// </summary>
    /// <param name="mode">Die Anpassung.</param>
    /// <param name="viewportWidth">Nutzbare Breite des Anzeigebereichs (ohne Ränder).</param>
    /// <param name="viewportHeight">Nutzbare Höhe des Anzeigebereichs (ohne Ränder).</param>
    /// <param name="pageWidth">Angezeigte Seitenbreite in Punkten.</param>
    /// <param name="pageHeight">Angezeigte Seitenhöhe in Punkten.</param>
    /// <returns>Der Zoom (begrenzt), oder <c>null</c> bei <see cref="FitMode.None"/> oder ungültigen Größen.</returns>
    public static double? Calculate(FitMode mode, double viewportWidth, double viewportHeight, double pageWidth, double pageHeight)
    {
        if (mode == FitMode.None || viewportWidth <= 0 || viewportHeight <= 0 || pageWidth <= 0 || pageHeight <= 0)
        {
            return null;
        }

        var zoom = mode switch
        {
            FitMode.Height => viewportHeight / pageHeight,
            FitMode.Width => viewportWidth / pageWidth,
            _ => Math.Min(viewportWidth / pageWidth, viewportHeight / pageHeight),
        };
        return Math.Clamp(Math.Round(zoom, 3), MinZoom, MaxZoom);
    }
}
