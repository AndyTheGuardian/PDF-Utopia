using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Zum Schwärzen markierter Bereich (A52). Bis zum Speichern nur eine Markierung; beim Speichern wird die Seite als Bild
/// geschrieben und der Bereich darin schwarz gefüllt, sodass darunter nichts übrig bleibt (F38).
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="Bounds">Der Bereich.</param>
public sealed record RedactionAnnotation(Guid Id, PdfRect Bounds) : Annotation(Id, FillColor)
{
    /// <summary>Füllung beim Schwärzen (F40).</summary>
    public const string FillColor = "#000000";

    /// <summary>
    /// Wie weit eine Markierung über einem Suchtreffer in Textrichtung schmaler ist als der Treffer. Suchtreffer haben
    /// ringsum 3 pt Rand; in Textrichtung bleibt so 1 pt, damit Nachbarbuchstaben sichtbar bleiben. Quer dazu bleibt der
    /// volle Rand, damit Ober- und Unterlängen sicher abgedeckt sind.
    /// </summary>
    public const double SearchHitInset = 2;

    /// <summary>
    /// Markierung über einem Suchtreffer; die Textrichtung ist die längere Seite des Treffers.
    /// </summary>
    /// <param name="hit">Rechteck des Treffers (mit Rand).</param>
    /// <returns>Die Markierung.</returns>
    public static RedactionAnnotation ForSearchHit(PdfRect hit)
    {
        var inset = Math.Min(SearchHitInset, Math.Max(hit.Width, hit.Height) / 4);
        var bounds = hit.Width >= hit.Height
            ? new PdfRect(hit.X + inset, hit.Y, hit.Width - 2 * inset, hit.Height)
            : new PdfRect(hit.X, hit.Y + inset, hit.Width, hit.Height - 2 * inset);
        return new RedactionAnnotation(Guid.NewGuid(), bounds);
    }
}
