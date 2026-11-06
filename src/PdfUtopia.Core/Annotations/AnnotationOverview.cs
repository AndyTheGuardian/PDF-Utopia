using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Annotations;

/// <summary>Arten im Filter der Anmerkungsübersicht (A45).</summary>
public enum AnnotationCategory
{
    /// <summary>Kommentare.</summary>
    Comment,

    /// <summary>Markierungen (Marker).</summary>
    Highlight,

    /// <summary>Freihandlinien.</summary>
    Freehand,

    /// <summary>Formen: Rechteck, Ellipse, Linie, Pfeil.</summary>
    Shape,

    /// <summary>Freier Text.</summary>
    Text,
}

/// <summary>Genaue Art einer Anmerkung für ihre Beschriftung in der Übersicht.</summary>
public enum AnnotationLabel
{
    /// <summary>Kommentar.</summary>
    Comment,

    /// <summary>Markierung.</summary>
    Highlight,

    /// <summary>Freihandlinie.</summary>
    Freehand,

    /// <summary>Rechteck.</summary>
    Rectangle,

    /// <summary>Ellipse.</summary>
    Ellipse,

    /// <summary>Linie.</summary>
    Line,

    /// <summary>Pfeil.</summary>
    Arrow,

    /// <summary>Freier Text.</summary>
    Text,

    /// <summary>Unterschrift (A49).</summary>
    Signature,

    /// <summary>Stempel (A51).</summary>
    Stamp,

    /// <summary>Bild (A51).</summary>
    Image,

    /// <summary>Schwärzung (A52).</summary>
    Redaction,
}

/// <summary>
/// Ein Eintrag der Anmerkungsübersicht.
/// </summary>
/// <param name="PageIndex">Position der Seite in der aktuellen Seitenliste (0-basiert).</param>
/// <param name="Page">Die Seite.</param>
/// <param name="Annotation">Die Anmerkung.</param>
/// <param name="Category">Art für den Filter.</param>
/// <param name="Label">Genaue Art für die Beschriftung.</param>
/// <param name="FootnoteNumber">Fußnotenzahl bei Kommentaren, sonst <c>null</c>.</param>
/// <param name="Text">Kurztext: Kommentar- bzw. Text-Inhalt, bei Markierungen der markierte Text; sonst leer.</param>
public sealed record AnnotationEntry(
    int PageIndex, PageEntry Page, Annotation Annotation, AnnotationCategory Category, AnnotationLabel Label, int? FootnoteNumber, string Text);

/// <summary>
/// Stellt die Anmerkungsübersicht zusammen (A45): alle Anmerkungen in Seitenreihenfolge, je Seite zuerst die
/// Kommentare in Fußnotenreihenfolge, dann alle übrigen in Zeichenreihenfolge.
/// </summary>
public static class AnnotationOverview
{
    /// <summary>Höchstlänge eines Kurztexts.</summary>
    public const int MaxTextLength = 120;

    /// <summary>
    /// Baut die Einträge.
    /// </summary>
    /// <param name="document">Das Dokument.</param>
    /// <param name="highlightedText">Liefert den Text unter einer Markierung (Seite, Bereich) oder <c>null</c>.</param>
    /// <returns>Die Einträge.</returns>
    public static IReadOnlyList<AnnotationEntry> Build(EditableDocument document, Func<PageEntry, PdfRect, string>? highlightedText = null)
    {
        var entries = new List<AnnotationEntry>();
        for (var index = 0; index < document.Pages.Count; index++)
        {
            var page = document.Pages[index];
            var annotations = document.Annotations.For(page.Id);
            var comments = annotations.OfType<CommentAnnotation>().ToList();
            entries.AddRange(comments.Select((comment, i) =>
                new AnnotationEntry(index, page, comment, AnnotationCategory.Comment, AnnotationLabel.Comment, i + 1, Shorten(comment.Text))));
            entries.AddRange(annotations.Where(annotation => annotation is not CommentAnnotation)
                .Select(annotation => Describe(index, page, annotation, highlightedText)));
        }

        return entries;
    }

    /// <summary>
    /// Ob ein Eintrag zum Filter passt.
    /// </summary>
    /// <param name="entry">Der Eintrag.</param>
    /// <param name="filter">Gewählte Art oder <c>null</c> für alle.</param>
    /// <returns><c>true</c>, wenn er angezeigt wird.</returns>
    public static bool Matches(AnnotationEntry entry, AnnotationCategory? filter) => filter is null || entry.Category == filter;

    /// <summary>
    /// Kürzt Text auf eine Zeile: Zeilenumbrüche und mehrfacher Leerraum werden zu einem Leerzeichen, zu lange Texte
    /// enden mit „…“.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <param name="maxLength">Höchstlänge einschließlich „…“.</param>
    /// <returns>Der Kurztext.</returns>
    public static string Shorten(string text, int maxLength = MaxTextLength)
    {
        var single = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return single.Length <= maxLength ? single : single[..(maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>
    /// Beschreibt eine Anmerkung, die kein Kommentar ist.
    /// </summary>
    private static AnnotationEntry Describe(int index, PageEntry page, Annotation annotation, Func<PageEntry, PdfRect, string>? highlightedText)
    {
        var (category, label, text) = annotation switch
        {
            HighlightAnnotation highlight => (AnnotationCategory.Highlight, AnnotationLabel.Highlight, Shorten(highlightedText?.Invoke(page, highlight.Bounds) ?? string.Empty)),
            InkAnnotation => (AnnotationCategory.Freehand, AnnotationLabel.Freehand, string.Empty),
            SignatureAnnotation => (AnnotationCategory.Freehand, AnnotationLabel.Signature, string.Empty),
            RectangleAnnotation => (AnnotationCategory.Shape, AnnotationLabel.Rectangle, string.Empty),
            EllipseAnnotation => (AnnotationCategory.Shape, AnnotationLabel.Ellipse, string.Empty),
            LineAnnotation line => (AnnotationCategory.Shape, line.Arrow == ArrowStyle.None ? AnnotationLabel.Line : AnnotationLabel.Arrow, string.Empty),
            TextAnnotation textAnnotation => (AnnotationCategory.Text, AnnotationLabel.Text, Shorten(textAnnotation.Text)),
            StampAnnotation stamp => (AnnotationCategory.Shape, AnnotationLabel.Stamp, Shorten(stamp.Label)),
            ImageAnnotation => (AnnotationCategory.Shape, AnnotationLabel.Image, string.Empty),
            RedactionAnnotation => (AnnotationCategory.Shape, AnnotationLabel.Redaction, string.Empty),
            _ => throw new NotSupportedException($"Unbekannte Annotation {annotation.GetType().Name}."),
        };
        return new AnnotationEntry(index, page, annotation, category, label, null, text);
    }
}
