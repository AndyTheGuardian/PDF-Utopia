using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Eine Seite der Seitenansicht (A55): Bild, Anmerkungen, Fußnoten und Text. In den fortlaufenden Ansichten und bei
/// Doppelseite sind mehrere gleichzeitig zu sehen; jede hat ihren eigenen Seiteneditor. Die Seite wird über ihre
/// Kennung gefunden, damit Umsortieren, Drehen und Einfügen sie nicht verwechseln.
/// </summary>
public sealed partial class PageViewModel : ObservableObject
{
    private Guid? _pendingFocus;

    /// <summary>
    /// Legt die Ansicht einer Seite an.
    /// </summary>
    /// <param name="document">Das Dokument.</param>
    /// <param name="pageId">Kennung der Seite.</param>
    public PageViewModel(DocumentViewModel document, int pageId)
    {
        Document = document;
        PageId = pageId;
        RebuildFootnotes();
    }

    /// <summary>Wird ausgelöst, wenn sich Anmerkungen, Drehung, Suchtreffer oder erkannter Text der Seite ändern.</summary>
    public event EventHandler? Changed;

    /// <summary>Wird ausgelöst, wenn eine Anmerkung der Seite hervorgehoben werden soll (A45).</summary>
    public event EventHandler<Guid>? FocusRequested;

    /// <summary>Das Dokument.</summary>
    public DocumentViewModel Document { get; }

    /// <summary>Kennung der Seite.</summary>
    public int PageId { get; }

    /// <summary>Position der Seite in der Seitenliste (0-basiert); -1, wenn sie gelöscht wurde.</summary>
    public int Index => Document.Document.Pages.IndexOf(PageId);

    /// <summary>Die Seite; nach dem Löschen die zuletzt bekannte, bis die Ansicht sie entfernt.</summary>
    public PageEntry Page => Index is >= 0 and var index ? _page = Document.Document.Pages[index] : _page!;

    private PageEntry? _page;

    /// <summary>Größe der ungedrehten Seite in Punkten.</summary>
    public (double Width, double Height) PageSize => Document.SourceOf(Page).PageSize(Page.SourcePageNumber);

    /// <summary>Größe der angezeigten, ggf. gedrehten Seite in Punkten.</summary>
    public (double Width, double Height) DisplaySize
    {
        get
        {
            var (width, height) = PageSize;
            return PageTransform.RotatedSize(width, height, Page.Rotation);
        }
    }

    /// <summary>Angezeigte Breite (für die Lücke neben einer einzelnen Seite einer Doppelseite).</summary>
    public double DisplayWidth => DisplaySize.Width;

    /// <summary>Gerenderte Seite für den aktuellen Zoom.</summary>
    [ObservableProperty]
    public partial Bitmap? Bitmap { get; private set; }

    /// <summary>Ob die Seite als aktuelle Seite hervorgehoben wird (nur, wenn mehrere Seiten zu sehen sind).</summary>
    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    /// <summary>Fußnoten der Seite.</summary>
    public ObservableCollection<FootnoteViewModel> Footnotes { get; } = [];

    /// <summary>Ob die Seite Fußnoten hat.</summary>
    public bool HasFootnotes => Footnotes.Count > 0;

    /// <summary>Annotationen der Seite.</summary>
    public IReadOnlyList<Annotation> Annotations => Document.Document.Annotations.For(PageId);

    /// <summary>Erkannte Absätze (Koordinaten der ungedrehten Seite) für den Kommentar-Vorschlag.</summary>
    public IReadOnlyList<PdfRect> TextBlocks => Document.TextBlocks(Page);

    /// <summary>Fundstellen des Suchbegriffs (Koordinaten der ungedrehten Seite).</summary>
    public IReadOnlyList<PdfRect> SearchHits => Document.SearchHits(Page);

    /// <summary>
    /// Rendert die Seite für Zoom und Bildschirmauflösung.
    /// </summary>
    /// <param name="scale">Pixel je Punkt.</param>
    /// <returns>Ein Task, der nach dem Rendern endet.</returns>
    public async Task RenderAsync(double scale)
    {
        var page = Page;
        var bitmap = await Document.RenderAsync(page, scale);
        if (page == Page && Index >= 0)
        {
            Document.Renderer.Pin(bitmap);
            var previous = Bitmap;
            Bitmap = bitmap;
            if (previous is not null && previous != bitmap)
            {
                Document.Renderer.Unpin(previous);
            }
        }
    }

    /// <summary>
    /// Gibt das gerenderte Bild frei, sobald die Seite nicht mehr angezeigt wird.
    /// </summary>
    public void ReleaseBitmap()
    {
        if (Bitmap is { } bitmap)
        {
            Bitmap = null;
            Document.Renderer.Unpin(bitmap);
        }
    }

    /// <summary>
    /// Fügt der Seite eine Annotation hinzu.
    /// </summary>
    /// <param name="annotation">Die Annotation.</param>
    public void AddAnnotation(Annotation annotation) => Document.Document.AddAnnotation(PageId, annotation);

    /// <summary>
    /// Entfernt Annotationen der Seite als einen Schritt.
    /// </summary>
    /// <param name="ids">Kennungen.</param>
    public void RemoveAnnotations(IReadOnlyCollection<Guid> ids) => Document.Document.RemoveAnnotations(PageId, ids);

    /// <summary>
    /// Setzt einen Stempel oder ein Bild, das in Anzeige-Koordinaten angelegt wurde, auf die Seite.
    /// </summary>
    /// <param name="display">Stempel oder Bild in Anzeige-Koordinaten.</param>
    /// <returns>Die gesetzte Anmerkung (Koordinaten der ungedrehten Seite).</returns>
    public BoxAnnotation PlaceBox(BoxAnnotation display)
    {
        var page = ToPage(display);
        AddAnnotation(page);
        return page;
    }

    /// <summary>
    /// Rechnet einen Stempel oder ein Bild aus der Anzeige auf die ungedrehte Seite um.
    /// </summary>
    /// <param name="display">In Anzeige-Koordinaten.</param>
    /// <returns>In Koordinaten der ungedrehten Seite.</returns>
    public BoxAnnotation ToPage(BoxAnnotation display)
    {
        var (width, height) = PageSize;
        var rotation = Page.Rotation;
        var center = PageTransform.Unrotate(display.Center, rotation, width, height);
        return display with
        {
            Bounds = BoxAnnotation.Centered(center, display.Bounds.Width, display.Bounds.Height),
            Angle = BoxAnnotation.NormalizeAngle(display.Angle - (int)rotation),
        };
    }

    /// <summary>
    /// Lässt eine Anmerkung hervorheben; ist noch kein Editor für die Seite da, übernimmt ihn der nächste.
    /// </summary>
    /// <param name="annotationId">Die Anmerkung.</param>
    public void RequestFocus(Guid annotationId)
    {
        _pendingFocus = annotationId;
        FocusRequested?.Invoke(this, annotationId);
    }

    /// <summary>
    /// Holt eine noch nicht ausgeführte Hervorhebung ab.
    /// </summary>
    /// <returns>Die Anmerkung oder <c>null</c>.</returns>
    public Guid? TakePendingFocus()
    {
        var id = _pendingFocus;
        _pendingFocus = null;
        return id;
    }

    /// <summary>
    /// Anmerkungen, Drehung, Suchtreffer oder erkannter Text haben sich geändert: Fußnoten neu aufbauen, Editor
    /// benachrichtigen.
    /// </summary>
    internal void NotifyChanged()
    {
        RebuildFootnotes();
        OnPropertyChanged(nameof(DisplayWidth));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Baut die Fußnoten der Seite neu auf.
    /// </summary>
    private void RebuildFootnotes()
    {
        if (Index < 0)
        {
            return;
        }

        Footnotes.Clear();
        var comments = Document.Document.Annotations.CommentsFor(PageId);
        for (var i = 0; i < comments.Count; i++)
        {
            Footnotes.Add(new FootnoteViewModel(comments[i].Id, i + 1, comments[i].ColorHex, comments[i].Text, CommitFootnote));
        }

        OnPropertyChanged(nameof(HasFootnotes));
    }

    /// <summary>
    /// Übernimmt einen geänderten Fußnotentext (nur wenn er sich wirklich geändert hat).
    /// </summary>
    private void CommitFootnote(Guid commentId, string text)
    {
        var comment = Document.Document.Annotations.CommentsFor(PageId).FirstOrDefault(candidate => candidate.Id == commentId);
        if (comment is not null && comment.Text != text)
        {
            Document.Document.UpdateAnnotation(PageId, comment with { Text = text });
        }
    }
}

/// <summary>
/// Eine Zeile der Seitenansicht (A55): eine Seite oder ein Seitenpaar. In einem Seitenpaar bleibt neben einer
/// einzelnen ersten bzw. letzten Seite eine Lücke in deren Breite frei.
/// </summary>
/// <param name="row">Lage der Seiten.</param>
/// <param name="left">Linke Seite oder <c>null</c>.</param>
/// <param name="right">Rechte Seite oder <c>null</c>.</param>
public sealed class PageRowViewModel(PageRow row, PageViewModel? left, PageViewModel? right)
{
    /// <summary>Lage der Seiten.</summary>
    public PageRow Row { get; } = row;

    /// <summary>Linke Seite oder <c>null</c>.</summary>
    public PageViewModel? Left { get; } = left;

    /// <summary>Rechte Seite oder <c>null</c>.</summary>
    public PageViewModel? Right { get; } = right;

    /// <summary>Ob links eine Lücke bleibt (erste Seite allein rechts).</summary>
    public bool HasLeftGap => Row.IsPair && Left is null;

    /// <summary>Ob rechts eine Lücke bleibt (letzte Seite ohne Partner).</summary>
    public bool HasRightGap => Row.IsPair && Right is null;

    /// <summary>Seiten der Zeile.</summary>
    public IEnumerable<PageViewModel> Pages => new[] { Left, Right }.OfType<PageViewModel>();
}
