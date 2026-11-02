using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Eine gesetzte Fußnote: Nummer, Bereich auf der verlängerten Seite und umbrochene Zeilen.
/// </summary>
/// <param name="Number">Fußnotenzahl.</param>
/// <param name="Comment">Der Kommentar.</param>
/// <param name="Bounds">Bereich in Anzeige-Koordinaten (unterhalb der ursprünglichen Seite).</param>
/// <param name="Lines">Umbrochene Textzeilen.</param>
internal sealed record PlacedFootnote(int Number, CommentAnnotation Comment, PdfRect Bounds, IReadOnlyList<string> Lines);

/// <summary>
/// Ergebnis des Fußnotensatzes einer Seite.
/// </summary>
/// <param name="Extension">Zusätzliche Seitenhöhe in Punkten.</param>
/// <param name="Heading">Bereich der Überschrift mit Trennlinie.</param>
/// <param name="Notes">Die Fußnoten.</param>
internal sealed record FootnoteBlock(double Extension, PdfRect Heading, IReadOnlyList<PlacedFootnote> Notes);

/// <summary>
/// Setzt die Fußnoten der Kommentare auf den nach unten verlängerten Bereich einer Seite (F3, P5):
/// gestrichelte Trennlinie mit Überschrift, darunter je Kommentar Nummer und umbrochener Text.
/// </summary>
internal static class FootnoteLayout
{
    /// <summary>Schriftgröße des Fußnotentexts.</summary>
    public const double FontSize = 9;

    /// <summary>Zeilenabstand.</summary>
    public const double LineHeight = 12;

    /// <summary>Breite der Spalte für die Fußnotenzahl.</summary>
    public const double NumberColumn = 18;

    /// <summary>Höhe der Überschrift mit Trennlinie.</summary>
    public const double HeadingHeight = 22;

    private const double MinMargin = 36;
    private const double MarginRatio = 0.1;
    private const double NoteGap = 4;
    private const double BottomPadding = 18;

    /// <summary>
    /// Seitenrand links und rechts.
    /// </summary>
    /// <param name="pageWidth">Angezeigte Seitenbreite.</param>
    /// <returns>Der Rand.</returns>
    public static double Margin(double pageWidth) => Math.Max(MinMargin, pageWidth * MarginRatio);

    /// <summary>
    /// Setzt die Fußnoten.
    /// </summary>
    /// <param name="comments">Kommentare der Seite in Nummernreihenfolge.</param>
    /// <param name="pageWidth">Angezeigte Seitenbreite.</param>
    /// <param name="pageHeight">Angezeigte Seitenhöhe (Beginn der Verlängerung).</param>
    /// <returns>Der Satz; ohne Kommentare ist die Verlängerung 0.</returns>
    public static FootnoteBlock Layout(IReadOnlyList<CommentAnnotation> comments, double pageWidth, double pageHeight)
    {
        var margin = Margin(pageWidth);
        var blockWidth = pageWidth - 2 * margin;
        if (comments.Count == 0)
        {
            return new FootnoteBlock(0, new PdfRect(margin, pageHeight, blockWidth, 0), []);
        }

        var heading = new PdfRect(margin, pageHeight, blockWidth, HeadingHeight);
        var y = heading.Bottom;
        var notes = new List<PlacedFootnote>();
        for (var i = 0; i < comments.Count; i++)
        {
            var lines = Wrap(comments[i].Text, blockWidth - NumberColumn);
            var bounds = new PdfRect(margin, y, blockWidth, lines.Count * LineHeight);
            notes.Add(new PlacedFootnote(i + 1, comments[i], bounds, lines));
            y = bounds.Bottom + NoteGap;
        }

        return new FootnoteBlock(y - NoteGap + BottomPadding - pageHeight, heading, notes);
    }

    /// <summary>
    /// Bricht Text an Wortgrenzen um; Zeilenumbrüche im Text bleiben erhalten, zu lange Wörter werden geteilt.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <param name="width">Verfügbare Breite.</param>
    /// <returns>Mindestens eine Zeile.</returns>
    public static IReadOnlyList<string> Wrap(string text, double width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            WrapParagraph(paragraph, width, lines);
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }

    /// <summary>
    /// Bricht einen Absatz um.
    /// </summary>
    private static void WrapParagraph(string paragraph, double width, List<string> lines)
    {
        var current = string.Empty;
        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (Fits(candidate, width))
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            current = BreakLongWord(word, width, lines);
        }

        lines.Add(current);
    }

    /// <summary>
    /// Teilt ein Wort, das allein nicht in die Zeile passt; gibt den Rest zurück.
    /// </summary>
    private static string BreakLongWord(string word, double width, List<string> lines)
    {
        var rest = word;
        while (!Fits(rest, width) && rest.Length > 1)
        {
            var length = rest.Length - 1;
            while (length > 1 && !Fits(rest[..length], width))
            {
                length--;
            }

            lines.Add(rest[..length]);
            rest = rest[length..];
        }

        return rest;
    }

    /// <summary>
    /// Ob ein Text in die Breite passt.
    /// </summary>
    private static bool Fits(string text, double width) => StandardFont.Width(text, FontSize) <= width;
}
