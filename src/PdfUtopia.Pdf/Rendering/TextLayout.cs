using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Pdf.Rendering;

/// <summary>
/// Fasst Zeichen einer Seite zu Zeilen und Absätzen zusammen, damit ein Klick mit dem Kommentar-Werkzeug den
/// Absatz vorschlagen kann (F21). Eine PDF kennt keine Absätze; die Erkennung arbeitet mit Abständen:
/// Zeichen auf gleicher Höhe bilden eine Zeile, eng aufeinander folgende, links ähnlich ausgerichtete Zeilen
/// einen Absatz. Bei Spalten oder Tabellen kann sie danebenliegen – das Aufziehen eines Bereichs geht immer.
/// </summary>
internal static class TextLayout
{
    /// <summary>Abstand des Kommentarrahmens zum Text erkannter Absätze in Punkten (A36).</summary>
    public const double Padding = 8;

    /// <summary>Höchster Zeilenabstand (relativ zur Zeilenhöhe), bis zu dem Zeilen zusammengehören.</summary>
    private const double MaxLineGapRatio = 0.7;

    /// <summary>Höchster Versatz des linken Rands (relativ zur Zeilenhöhe) innerhalb eines Absatzes.</summary>
    private const double MaxIndentRatio = 3;

    /// <summary>Zeichen mit kleinerer Abweichung der Mitte (relativ zur Höhe) liegen in derselben Zeile.</summary>
    private const double SameLineRatio = 0.5;

    /// <summary>
    /// Erkennt Absätze.
    /// </summary>
    /// <param name="characters">Umrisse der sichtbaren Zeichen in Lesereihenfolge (Anzeige-Koordinaten).</param>
    /// <returns>Umrisse der Absätze, mit etwas Rand.</returns>
    public static IReadOnlyList<PdfRect> Paragraphs(IEnumerable<PdfRect> characters)
    {
        var paragraphs = new List<PdfRect>();
        PdfRect? current = null;
        var currentLineHeight = 0.0;
        foreach (var line in Lines(characters))
        {
            if (current is { } paragraph && BelongsTo(paragraph, line, currentLineHeight))
            {
                current = Union(paragraph, line);
            }
            else
            {
                if (current is { } finished)
                {
                    paragraphs.Add(Inflate(finished));
                }

                current = line;
            }

            currentLineHeight = line.Height;
        }

        if (current is { } last)
        {
            paragraphs.Add(Inflate(last));
        }

        return paragraphs;
    }

    /// <summary>
    /// Fasst Zeichen zu Zeilen zusammen.
    /// </summary>
    /// <param name="characters">Umrisse der Zeichen in Lesereihenfolge.</param>
    /// <returns>Umrisse der Zeilen.</returns>
    public static IReadOnlyList<PdfRect> Lines(IEnumerable<PdfRect> characters)
    {
        var lines = new List<PdfRect>();
        PdfRect? current = null;
        foreach (var character in characters.Where(c => c.Width > 0 && c.Height > 0))
        {
            if (current is { } line && OnSameLine(line, character))
            {
                current = Union(line, character);
                continue;
            }

            if (current is { } finished)
            {
                lines.Add(finished);
            }

            current = character;
        }

        if (current is { } last)
        {
            lines.Add(last);
        }

        return lines;
    }

    /// <summary>
    /// Ob ein Zeichen die Zeile fortsetzt: Mitte auf ähnlicher Höhe und kein großer Sprung zurück nach links.
    /// </summary>
    private static bool OnSameLine(PdfRect line, PdfRect character)
    {
        var height = Math.Max(line.Height, character.Height);
        var centerOffset = Math.Abs((line.Y + line.Bottom) / 2 - (character.Y + character.Bottom) / 2);
        return centerOffset <= height * SameLineRatio && character.X >= line.X - height;
    }

    /// <summary>
    /// Ob eine Zeile den Absatz fortsetzt: kleiner Abstand nach unten und ähnlicher linker Rand.
    /// </summary>
    private static bool BelongsTo(PdfRect paragraph, PdfRect line, double previousLineHeight)
    {
        var height = Math.Max(previousLineHeight, line.Height);
        var gap = line.Y - paragraph.Bottom;
        return gap >= -height * SameLineRatio && gap <= height * MaxLineGapRatio && Math.Abs(line.X - paragraph.X) <= height * MaxIndentRatio;
    }

    /// <summary>Vereinigung zweier Rechtecke.</summary>
    private static PdfRect Union(PdfRect a, PdfRect b) =>
        PdfRect.FromCorners(new PdfPoint(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new PdfPoint(Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom)));

    /// <summary>Rechteck mit Rand.</summary>
    private static PdfRect Inflate(PdfRect rect) =>
        new(rect.X - Padding, rect.Y - Padding, rect.Width + 2 * Padding, rect.Height + 2 * Padding);
}
