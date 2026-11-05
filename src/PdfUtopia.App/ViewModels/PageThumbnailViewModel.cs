using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Eintrag der Seitenliste links: Miniatur, Position und – nach dem Umsortieren – die ursprüngliche Seitennummer;
/// bei eingefügten Seiten deren Herkunft (A41).
/// </summary>
/// <param name="entry">Die Seite.</param>
/// <param name="position">Position in der Liste (1-basiert).</param>
/// <param name="sourceName">Dateiname der Quelle bei eingefügten Seiten, sonst <c>null</c>.</param>
/// <param name="isBlank">Ob es eine eingefügte leere Seite ist (A48).</param>
public sealed partial class PageThumbnailViewModel(PageEntry entry, int position, string? sourceName = null, bool isBlank = false) : ObservableObject
{
    /// <summary>Die Seite.</summary>
    public PageEntry Entry { get; } = entry;

    /// <summary>Position in der Liste (1-basiert).</summary>
    public int Position { get; } = position;

    /// <summary>Beschriftung unter der Miniatur.</summary>
    public string Label => Position.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Herkunft: bei leeren Seiten „(leer)“, bei eingefügten Seiten Datei und Seite (z. B. „anhang.pdf S. 2“), sonst die ursprüngliche
    /// Seitennummer, falls die Seite verschoben wurde; sonst leer.
    /// </summary>
    public string OriginalHint =>
        isBlank ? $"({Loc.T("Pages.Blank")})"
        : sourceName is not null ? $"({Loc.F("Pages.InsertedFrom", sourceName, Entry.SourcePageNumber)})"
        : Entry.SourcePageNumber == Position ? string.Empty
        : $"({Loc.F("Pages.Original", Entry.SourcePageNumber)})";

    /// <summary>Ob die Seite aus einer anderen Datei eingefügt wurde (Rahmen in der Akzentfarbe); leere Seiten nicht.</summary>
    public bool IsInserted => sourceName is not null;

    /// <summary>Miniatur; wird im Hintergrund gerendert.</summary>
    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    /// <summary>Ob die Seite gerade angezeigt wird.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Ob die Seite zur Mehrfachauswahl gehört (A48).</summary>
    [ObservableProperty]
    public partial bool IsMarked { get; set; }

    /// <summary>Ob die Seite Kommentare enthält (Symbol in der Seitenliste, A39).</summary>
    [ObservableProperty]
    public partial bool HasComments { get; set; }

    /// <summary>Ob die Seite Markierungen enthält.</summary>
    [ObservableProperty]
    public partial bool HasHighlights { get; set; }

    /// <summary>Ob die Seite Freihandlinien enthält.</summary>
    [ObservableProperty]
    public partial bool HasInk { get; set; }

    /// <summary>Ob die Seite Formen enthält (Rechteck, Ellipse, Linie, Pfeil).</summary>
    [ObservableProperty]
    public partial bool HasShapes { get; set; }

    /// <summary>Ob die Seite freien Text enthält (Text-Werkzeug, A43).</summary>
    [ObservableProperty]
    public partial bool HasText { get; set; }

    /// <summary>
    /// Übernimmt, welche Arten von Annotationen die Seite enthält.
    /// </summary>
    /// <param name="annotations">Annotationen der Seite.</param>
    public void ShowAnnotationKinds(IReadOnlyCollection<Annotation> annotations)
    {
        HasComments = annotations.OfType<CommentAnnotation>().Any();
        HasHighlights = annotations.OfType<HighlightAnnotation>().Any();
        HasInk = annotations.OfType<InkAnnotation>().Any();
        HasShapes = annotations.Any(annotation => annotation is RectangleAnnotation or EllipseAnnotation or LineAnnotation);
        HasText = annotations.OfType<TextAnnotation>().Any();
    }
}

/// <summary>
/// Fußnote eines Kommentars unter der Seite; der Text ist direkt bearbeitbar.
/// </summary>
/// <param name="commentId">Kennung des Kommentars.</param>
/// <param name="number">Fußnotenzahl.</param>
/// <param name="colorHex">Kommentarfarbe.</param>
/// <param name="text">Fußnotentext.</param>
/// <param name="commit">Übernimmt einen geänderten Text ins Dokument (rückgängig machbar).</param>
public sealed partial class FootnoteViewModel(Guid commentId, int number, string colorHex, string text, Action<Guid, string> commit)
    : ObservableObject
{
    /// <summary>Kennung des Kommentars.</summary>
    public Guid CommentId { get; } = commentId;

    /// <summary>Fußnotenzahl.</summary>
    public int Number { get; } = number;

    /// <summary>Kommentarfarbe.</summary>
    public string ColorHex { get; } = colorHex;

    /// <summary>Rahmenfarbe des Zahlenkreises (grau bei zu hellen Farben).</summary>
    public string BorderHex { get; } = Core.Theming.DrawingColors.CommentBorder(colorHex);

    /// <summary>Schriftfarbe der Zahl.</summary>
    public string NumberForegroundHex { get; } = Core.Theming.DrawingColors.FootnoteNumberForeground(colorHex);

    /// <summary>Fußnotentext.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = text;

    /// <summary>
    /// Übernimmt den bearbeiteten Text, sobald das Feld verlassen wird.
    /// </summary>
    public void Commit() => commit(CommentId, Text);
}
