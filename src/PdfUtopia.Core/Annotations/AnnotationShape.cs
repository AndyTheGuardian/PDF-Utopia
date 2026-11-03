namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Gemeinsame Form von Rechtecken und Kommentaren, damit Anzeige und gespeicherte PDF gleich aussehen (A37).
/// </summary>
public static class AnnotationShape
{
    /// <summary>Eckenradius von Rechtecken und Kommentarrahmen in Punkten.</summary>
    public const double CornerRadius = 4;

    /// <summary>
    /// Eckenradius für ein Rechteck bestimmter Größe: höchstens die halbe kürzere Seite, damit sich die Rundungen
    /// kleiner Rechtecke nicht überschneiden.
    /// </summary>
    /// <param name="width">Breite.</param>
    /// <param name="height">Höhe.</param>
    /// <returns>Radius in Punkten (nie negativ).</returns>
    public static double CornerRadiusFor(double width, double height) =>
        Math.Max(0, Math.Min(CornerRadius, Math.Min(width, height) / 2));
}
