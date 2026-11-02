namespace PdfUtopia.Core.Geometry;

/// <summary>
/// Punkt auf einer Seite in PDF-Punkten (1/72 Zoll). Ursprung ist die linke obere Ecke der ungedrehten Seite,
/// Y wächst nach unten – wie in der Oberfläche. Die PDF-Anbindung rechnet beim Speichern in PDF-Koordinaten um.
/// </summary>
/// <param name="X">Abstand vom linken Rand.</param>
/// <param name="Y">Abstand vom oberen Rand.</param>
public readonly record struct PdfPoint(double X, double Y)
{
    /// <summary>
    /// Abstand zu einem anderen Punkt.
    /// </summary>
    /// <param name="other">Der andere Punkt.</param>
    /// <returns>Der euklidische Abstand.</returns>
    public double DistanceTo(PdfPoint other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
}
