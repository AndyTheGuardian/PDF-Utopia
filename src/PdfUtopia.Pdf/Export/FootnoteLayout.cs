using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Text;

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
    public static IReadOnlyList<string> Wrap(string text, double width) => TextWrapper.Wrap(text, width, FontSize);
}
