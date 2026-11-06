using System.Globalization;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Text;

/// <summary>
/// Ein erkanntes Wort (A53).
/// </summary>
/// <param name="Text">Das Wort.</param>
/// <param name="Bounds">Lage auf der ungedrehten Seite.</param>
/// <param name="Paragraph">Absatz, zu dem es gehört (fortlaufende Nummer der Seite).</param>
/// <param name="Line">Zeile, zu der es gehört (fortlaufende Nummer der Seite).</param>
public sealed record RecognizedWord(string Text, PdfRect Bounds, int Paragraph, int Line);

/// <summary>
/// Durch Texterkennung gewonnener Text einer Seite (A53, F42): Wörter in Lesereihenfolge. Suche, Absatzerkennung und
/// Text unter Markierungen nutzen ihn wie die Textebene einer Datei.
/// </summary>
/// <param name="Words">Wörter in Lesereihenfolge.</param>
/// <param name="Orientation">Seitendrehung bei der Erkennung: Der Text läuft in deren Leserichtung (siehe
/// <see cref="TextAnnotation.Axes"/>).</param>
public sealed record RecognizedPage(IReadOnlyList<RecognizedWord> Words, PageRotation Orientation)
{
    /// <summary>Text der Seite: Wörter mit Leerzeichen, Zeilen mit Zeilenumbruch.</summary>
    public string Text => string.Join('\n', Words.GroupBy(word => word.Line).Select(line => string.Join(' ', line.Select(word => word.Text))));

    /// <summary>
    /// Fundstellen eines Begriffs (ohne Groß-/Kleinschreibung) als Rechtecke, je Wort ein Rechteck. Steht der Begriff
    /// nur in einem Teil eines Wortes, wird das Rechteck anteilig verkürzt.
    /// </summary>
    /// <param name="term">Suchbegriff.</param>
    /// <param name="padding">Rand um jedes Rechteck.</param>
    /// <returns>Die Rechtecke.</returns>
    public IReadOnlyList<PdfRect> Find(string term, double padding = 0)
    {
        var trimmed = term.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        // Fortlaufender Text mit der Zuordnung jedes Zeichens zu Wort und Stelle im Wort
        var text = new System.Text.StringBuilder();
        var owners = new List<(int Word, int Index)>();
        for (var i = 0; i < Words.Count; i++)
        {
            if (i > 0)
            {
                text.Append(Words[i].Line == Words[i - 1].Line ? ' ' : '\n');
                owners.Add((-1, 0));
            }

            for (var c = 0; c < Words[i].Text.Length; c++)
            {
                text.Append(Words[i].Text[c]);
                owners.Add((i, c));
            }
        }

        var all = text.ToString();
        var hits = new List<PdfRect>();
        for (var at = all.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = all.IndexOf(trimmed, at + trimmed.Length, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var group in Enumerable.Range(at, trimmed.Length).Select(i => owners[i]).Where(owner => owner.Word >= 0).GroupBy(owner => owner.Word))
            {
                var word = Words[group.Key];
                hits.Add(Inflate(Part(word, group.Min(owner => owner.Index), group.Max(owner => owner.Index) + 1), padding));
            }
        }

        return hits;
    }

    /// <summary>Umschließende Rechtecke der Absätze (für die Absatzerkennung beim Kommentieren).</summary>
    public IReadOnlyList<PdfRect> Paragraphs => Words.GroupBy(word => word.Paragraph).Select(paragraph => Enclose(paragraph.Select(word => word.Bounds))).ToList();

    /// <summary>
    /// Text der Wörter, deren Mitte im Bereich liegt (z. B. unter einer Markierung).
    /// </summary>
    /// <param name="area">Der Bereich.</param>
    /// <returns>Der Text, Wörter durch Leerzeichen getrennt.</returns>
    public string TextIn(PdfRect area) => string.Join(' ', Words
        .Where(word => Contains(area, new PdfPoint(word.Bounds.X + word.Bounds.Width / 2, word.Bounds.Y + word.Bounds.Height / 2)))
        .Select(word => word.Text));

    /// <summary>
    /// Ohne die Wörter, die einen der Bereiche berühren (Schwärzungen, A52).
    /// </summary>
    /// <param name="areas">Die Bereiche.</param>
    /// <returns>Die Seite ohne diese Wörter.</returns>
    public RecognizedPage Without(IReadOnlyCollection<PdfRect> areas) =>
        areas.Count == 0 ? this : this with { Words = Words.Where(word => !areas.Any(area => Intersects(area, word.Bounds))).ToList() };

    /// <summary>Anteil eines Wortes von Zeichen <paramref name="start"/> bis vor <paramref name="end"/>, in Leserichtung.</summary>
    private PdfRect Part(RecognizedWord word, int start, int end)
    {
        if (start == 0 && end == word.Text.Length)
        {
            return word.Bounds;
        }

        var (across, _) = TextAnnotation.Axes(Orientation);
        var (from, to) = ((double)start / word.Text.Length, (double)end / word.Text.Length);
        var bounds = word.Bounds;
        return across switch
        {
            { X: > 0 } => new PdfRect(bounds.X + bounds.Width * from, bounds.Y, bounds.Width * (to - from), bounds.Height),
            { X: < 0 } => new PdfRect(bounds.Right - bounds.Width * to, bounds.Y, bounds.Width * (to - from), bounds.Height),
            { Y: > 0 } => new PdfRect(bounds.X, bounds.Y + bounds.Height * from, bounds.Width, bounds.Height * (to - from)),
            _ => new PdfRect(bounds.X, bounds.Bottom - bounds.Height * to, bounds.Width, bounds.Height * (to - from)),
        };
    }

    /// <summary>Kleinstes Rechteck um alle Rechtecke.</summary>
    private static PdfRect Enclose(IEnumerable<PdfRect> rects)
    {
        var list = rects.ToList();
        return PdfRect.FromCorners(new PdfPoint(list.Min(r => r.X), list.Min(r => r.Y)), new PdfPoint(list.Max(r => r.Right), list.Max(r => r.Bottom)));
    }

    private static PdfRect Inflate(PdfRect rect, double amount) =>
        amount == 0 ? rect : new PdfRect(rect.X - amount, rect.Y - amount, rect.Width + 2 * amount, rect.Height + 2 * amount);

    private static bool Contains(PdfRect rect, PdfPoint point) =>
        point.X >= rect.X && point.X <= rect.Right && point.Y >= rect.Y && point.Y <= rect.Bottom;

    private static bool Intersects(PdfRect a, PdfRect b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}

/// <summary>
/// Liest die TSV-Ausgabe von Tesseract (A53): je Wort Absatz, Zeile, Rechteck in Bildpunkten, Sicherheit und Text.
/// </summary>
public static class TesseractTsv
{
    /// <summary>Wörter mit geringerer Sicherheit (0–100) werden verworfen.</summary>
    public const double MinConfidence = 30;

    private const int WordLevel = 5;

    /// <summary>
    /// Wertet die Ausgabe aus und rechnet die Rechtecke auf die Seite um.
    /// </summary>
    /// <param name="tsv">Ausgabe von <c>tesseract … tsv</c>.</param>
    /// <param name="toPage">Rechnet ein Rechteck in Bildpunkten auf die ungedrehte Seite um.</param>
    /// <param name="orientation">Seitendrehung, mit der das Bild gerendert wurde.</param>
    /// <returns>Der erkannte Text der Seite.</returns>
    public static RecognizedPage Parse(string tsv, Func<PdfRect, PdfRect> toPage, PageRotation orientation)
    {
        var words = new List<RecognizedWord>();
        var paragraphs = new Dictionary<(int, int), int>();
        var lines = new Dictionary<(int, int, int), int>();
        foreach (var row in tsv.Split('\n').Skip(1))
        {
            var columns = row.TrimEnd('\r').Split('\t');
            if (columns.Length < 12 || !int.TryParse(columns[0], out var level) || level != WordLevel)
            {
                continue;
            }

            var text = columns[11].Trim();
            if (text.Length == 0 || !double.TryParse(columns[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence) || confidence < MinConfidence)
            {
                continue;
            }

            var (block, paragraph, line) = (Number(columns[2]), Number(columns[3]), Number(columns[4]));
            var paragraphId = Id(paragraphs, (block, paragraph));
            var lineId = Id(lines, (block, paragraph, line));
            var pixels = new PdfRect(Number(columns[6]), Number(columns[7]), Number(columns[8]), Number(columns[9]));
            words.Add(new RecognizedWord(text, toPage(pixels), paragraphId, lineId));
        }

        return new RecognizedPage(words, orientation);
    }

    private static int Number(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static int Id<TKey>(Dictionary<TKey, int> ids, TKey key)
        where TKey : notnull
    {
        if (!ids.TryGetValue(key, out var id))
        {
            id = ids.Count;
            ids[key] = id;
        }

        return id;
    }
}
