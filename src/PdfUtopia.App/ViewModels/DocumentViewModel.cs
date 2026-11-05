using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.App.Localization;
using PdfUtopia.App.Services;
using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Ein geöffnetes Dokument (ein Tab): Datei, Bearbeitungsmodell, Seitenliste, aktuelle Seite und ihre Fußnoten.
/// Alle Änderungen laufen über <see cref="EditableDocument"/> und sind damit rückgängig machbar.
/// </summary>
public sealed partial class DocumentViewModel : ObservableObject, IDisposable
{
    /// <summary>Breite einer Miniatur bei der Standardbreite der linken Leiste.</summary>
    private const double ThumbnailWidth = 106;

    private readonly PageRenderer _renderer;
    private readonly List<PdfSource> _sources = [];
    private Dictionary<(int Source, int Page, PageRotation Rotation), Bitmap> _thumbnails = [];
    private Dictionary<(int Source, int Page, PageRotation Rotation), Bitmap> _staleThumbnails = [];
    private double _thumbnailScale;
    private int _thumbnailRun;
    private readonly Dictionary<(int Source, int Page), IReadOnlyList<PdfRect>> _textBlocks = [];
    private readonly Dictionary<(int Source, int Page), IReadOnlyList<PdfRect>> _searchHits = [];
    private readonly Dictionary<(int Source, int Page, PdfRect Area), string> _highlightedTexts = [];
    private string _searchTerm = string.Empty;

    /// <summary>
    /// Übernimmt eine geöffnete Datei.
    /// </summary>
    /// <param name="source">Die Datei.</param>
    /// <param name="undoLimit">Höchstzahl der Rückgängig-Schritte.</param>
    /// <param name="thumbnailScale">Größe der Miniaturen relativ zur Standardbreite der linken Leiste.</param>
    public DocumentViewModel(PdfSource source, int undoLimit, double thumbnailScale = 1)
    {
        Source = source;
        _thumbnailScale = thumbnailScale;
        _sources.Add(source);
        Document = source.CreateDocument(undoLimit);
        _renderer = new PageRenderer(SourceOf);
        AnnotationList = new AnnotationListViewModel(Document, HighlightedText);
        BookmarkList = new BookmarkListViewModel(Document, source.Bookmarks);
        Document.Pages.Changed += (_, _) => OnPagesChanged();
        Document.Annotations.Changed += (_, pageId) => OnAnnotationsChanged(pageId);
        Document.History.Changed += (_, _) => OnHistoryChanged();
        RebuildThumbnails();
        _ = RenderThumbnailsAsync();
    }

    /// <summary>Wird ausgelöst, wenn sich die Annotationen der angezeigten Seite ändern.</summary>
    public event EventHandler? CurrentPageAnnotationsChanged;

    /// <summary>Wird ausgelöst, wenn eine Anmerkung der angezeigten Seite sichtbar gemacht und hervorgehoben werden soll.</summary>
    public event EventHandler<Guid>? AnnotationFocusRequested;

    /// <summary>Anmerkungsübersicht (A45).</summary>
    public AnnotationListViewModel AnnotationList { get; }

    /// <summary>Lesezeichen der geöffneten Datei (A46).</summary>
    public BookmarkListViewModel BookmarkList { get; }

    /// <summary>Die geöffnete Datei.</summary>
    public PdfSource Source { get; }

    /// <summary>
    /// Alle Quelldateien nach <see cref="PageEntry.SourceIndex"/>: zuerst die geöffnete Datei, dann die Dateien,
    /// aus denen Seiten eingefügt wurden (A41).
    /// </summary>
    public IReadOnlyList<PdfSource> Sources => _sources;

    /// <summary>Das Bearbeitungsmodell.</summary>
    public EditableDocument Document { get; }

    /// <summary>Seitenliste links.</summary>
    public ObservableCollection<PageThumbnailViewModel> Thumbnails { get; } = [];

    /// <summary>Fußnoten der angezeigten Seite.</summary>
    public ObservableCollection<FootnoteViewModel> Footnotes { get; } = [];

    /// <summary>Dateiname.</summary>
    public string FileName => Path.GetFileName(Source.FilePath);

    /// <summary>Tab-Beschriftung, mit Stern bei ungespeicherten Änderungen.</summary>
    public string TabTitle => Document.IsModified ? $"{FileName} •" : FileName;

    /// <summary>Ob die Datei passwortgeschützt ist.</summary>
    public bool IsProtected => Source.Password is not null;

    /// <summary>Dateigröße für die Statusleiste.</summary>
    public string FileSizeText
    {
        get
        {
            var bytes = File.Exists(Source.FilePath) ? new FileInfo(Source.FilePath).Length : 0;
            return bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
        }
    }

    /// <summary>Position der angezeigten Seite (0-basiert).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage), nameof(PageStatus), nameof(CurrentDisplaySize))]
    public partial int CurrentIndex { get; set; }

    /// <summary>Ob das Dokument im aktiven Tab angezeigt wird.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>Ob die angezeigte Seite Fußnoten hat.</summary>
    public bool HasFootnotes => Footnotes.Count > 0;

    /// <summary>Gerenderte angezeigte Seite.</summary>
    [ObservableProperty]
    public partial Bitmap? CurrentBitmap { get; set; }

    /// <summary>Angezeigte Seite.</summary>
    public PageEntry CurrentPage => Document.Pages[Math.Clamp(CurrentIndex, 0, Document.Pages.Count - 1)];

    /// <summary>Größe der ungedrehten Seite in Punkten (wie die Datei sie anzeigt).</summary>
    public (double Width, double Height) CurrentPageSize => SourceOf(CurrentPage).PageSize(CurrentPage.SourcePageNumber);

    /// <summary>Größe der angezeigten, ggf. gedrehten Seite in Punkten.</summary>
    public (double Width, double Height) CurrentDisplaySize
    {
        get
        {
            var (width, height) = CurrentPageSize;
            return PageTransform.RotatedSize(width, height, CurrentPage.Rotation);
        }
    }

    /// <summary>Text für die Statusleiste.</summary>
    public string PageStatus => Loc.F("Status.Page", CurrentIndex + 1, Document.Pages.Count);

    /// <summary>Text des Verlaufs für die Statusleiste.</summary>
    public string UndoStatus => Loc.F("Status.Undo", Document.History.UndoCount, Document.History.Limit);

    /// <summary>
    /// Zeigt eine Seite an.
    /// </summary>
    /// <param name="index">Position (0-basiert).</param>
    public void Show(int index)
    {
        CurrentIndex = Math.Clamp(index, 0, Document.Pages.Count - 1);
        foreach (var thumbnail in Thumbnails)
        {
            thumbnail.IsSelected = thumbnail.Position == CurrentIndex + 1;
        }

        RebuildFootnotes();
        BookmarkList.MarkCurrent(CurrentIndex);
        CurrentPageAnnotationsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Rendert die angezeigte Seite für den Zoom und die Bildschirmauflösung.
    /// </summary>
    /// <param name="scale">Pixel je Punkt.</param>
    /// <returns>Ein Task, der nach dem Rendern endet.</returns>
    public async Task RenderCurrentAsync(double scale)
    {
        var page = CurrentPage;
        var bitmap = await _renderer.RenderAsync(page, scale);
        if (page == CurrentPage)
        {
            CurrentBitmap = bitmap;
        }
    }

    /// <summary>
    /// Quelldatei einer Seite.
    /// </summary>
    /// <param name="page">Die Seite.</param>
    /// <returns>Die Datei.</returns>
    public PdfSource SourceOf(PageEntry page) => SourceOf(page.SourceIndex);

    /// <summary>
    /// Fügt Seiten einer anderen Datei als einen Rückgängig-Schritt ein und zeigt die erste davon an (A41).
    /// Ihre gespeicherten Annotationen kommen mit (F25). Das Dokument übernimmt die Datei und gibt sie beim
    /// Schließen frei.
    /// </summary>
    /// <param name="source">Die Datei.</param>
    /// <param name="sourcePageNumbers">Seitennummern in der Datei (1-basiert), in Zielreihenfolge.</param>
    /// <param name="index">Position der ersten neuen Seite.</param>
    public void InsertPages(PdfSource source, IReadOnlyList<int> sourcePageNumbers, int index)
    {
        _sources.Add(source);
        Document.InsertPages(index, _sources.Count - 1, sourcePageNumbers, page => source.SavedAnnotations.GetValueOrDefault(page) ?? []);
        Show(index);
    }

    /// <summary>
    /// Rendert die Miniaturen für eine neue Breite der linken Leiste (F30) neu, damit sie scharf bleiben. Bis die
    /// neuen fertig sind, bleiben die bisherigen sichtbar.
    /// </summary>
    /// <param name="scale">Größe relativ zur Standardbreite.</param>
    public void SetThumbnailScale(double scale)
    {
        if (Math.Abs(scale - _thumbnailScale) < 0.02)
        {
            return;
        }

        _thumbnailScale = scale;
        foreach (var (key, bitmap) in _thumbnails)
        {
            _staleThumbnails.TryAdd(key, bitmap);
        }

        _thumbnails = [];
        _ = RenderThumbnailsAsync();
    }

    /// <summary>
    /// Springt zu einer Anmerkung der Übersicht: zeigt ihre Seite und lässt sie hervorheben (A45).
    /// </summary>
    /// <param name="entry">Der Eintrag.</param>
    public void ShowAnnotation(AnnotationEntry entry)
    {
        var index = Document.Pages.IndexOf(entry.Page.Id);
        if (index < 0)
        {
            return;
        }

        AnnotationList.Select(entry.Annotation.Id);
        Show(index);
        AnnotationFocusRequested?.Invoke(this, entry.Annotation.Id);
    }

    /// <summary>
    /// Verschiebt eine Seite und behält die angezeigte Seite bei.
    /// </summary>
    /// <param name="fromIndex">Bisherige Position.</param>
    /// <param name="toIndex">Neue Position.</param>
    public void MovePage(int fromIndex, int toIndex)
    {
        var shown = CurrentPage.Id;
        Document.MovePage(fromIndex, toIndex);
        Show(Document.Pages.IndexOf(shown));
    }

    /// <summary>
    /// Dreht eine Seite um 90° im Uhrzeigersinn (A19).
    /// </summary>
    /// <param name="index">Position.</param>
    public void RotatePage(int index) => Document.RotatePage(index);

    /// <summary>
    /// Löscht eine Seite, sofern es nicht die letzte ist.
    /// </summary>
    /// <param name="index">Position.</param>
    public void DeletePage(int index)
    {
        if (Document.CanDeletePage)
        {
            Document.DeletePage(index);
        }
    }

    /// <summary>
    /// Fügt der angezeigten Seite eine Annotation hinzu.
    /// </summary>
    /// <param name="annotation">Die Annotation.</param>
    public void AddAnnotation(Annotation annotation) => Document.AddAnnotation(CurrentPage.Id, annotation);

    /// <summary>
    /// Entfernt Annotationen der angezeigten Seite als einen Schritt.
    /// </summary>
    /// <param name="ids">Kennungen.</param>
    public void RemoveAnnotations(IReadOnlyCollection<Guid> ids) => Document.RemoveAnnotations(CurrentPage.Id, ids);

    /// <summary>
    /// Aktualisiert den Tab-Titel nach dem Speichern.
    /// </summary>
    public void NotifySaved() => OnPropertyChanged(nameof(TabTitle));

    /// <summary>
    /// Erkannte Absätze der angezeigten Seite (Koordinaten der ungedrehten Seite) für den Kommentar-Vorschlag.
    /// </summary>
    public IReadOnlyList<PdfRect> CurrentTextBlocks
    {
        get
        {
            var page = (CurrentPage.SourceIndex, CurrentPage.SourcePageNumber);
            if (!_textBlocks.TryGetValue(page, out var blocks))
            {
                blocks = SourceOf(CurrentPage).TextBlocks(page.SourcePageNumber);
                _textBlocks[page] = blocks;
            }

            return blocks;
        }
    }

    /// <summary>
    /// Suchbegriff; seine Fundstellen werden auf der Seite in der Akzentfarbe hinterlegt.
    /// </summary>
    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (_searchTerm == value)
            {
                return;
            }

            _searchTerm = value;
            _searchHits.Clear();
            CurrentPageAnnotationsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Fundstellen des Suchbegriffs auf der angezeigten Seite (Koordinaten der ungedrehten Seite).
    /// </summary>
    public IReadOnlyList<PdfRect> CurrentSearchHits
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_searchTerm))
            {
                return [];
            }

            var page = (CurrentPage.SourceIndex, CurrentPage.SourcePageNumber);
            if (!_searchHits.TryGetValue(page, out var hits))
            {
                hits = SourceOf(CurrentPage).FindText(page.SourcePageNumber, _searchTerm);
                _searchHits[page] = hits;
            }

            return hits;
        }
    }

    /// <summary>Annotationen der angezeigten Seite.</summary>
    public IReadOnlyList<Annotation> CurrentAnnotations => Document.Annotations.For(CurrentPage.Id);

    /// <inheritdoc />
    public void Dispose()
    {
        _renderer.Dispose();
        _thumbnailRun++;
        foreach (var bitmap in _thumbnails.Values.Concat(_staleThumbnails.Values).Distinct())
        {
            bitmap.Dispose();
        }

        foreach (var source in _sources)
        {
            source.Dispose();
        }
    }

    /// <summary>
    /// Text unter einer Markierung (zwischengespeichert, da das Lesen der Textebene Zeit kostet).
    /// </summary>
    private string HighlightedText(PageEntry page, PdfRect area)
    {
        var key = (page.SourceIndex, page.SourcePageNumber, area);
        if (!_highlightedTexts.TryGetValue(key, out var text))
        {
            text = SourceOf(page).TextInArea(page.SourcePageNumber, area);
            _highlightedTexts[key] = text;
        }

        return text;
    }

    /// <summary>
    /// Quelldatei zu einer Kennung aus <see cref="PageEntry.SourceIndex"/>.
    /// </summary>
    private PdfSource SourceOf(int sourceIndex) => _sources[sourceIndex];

    /// <summary>
    /// Übernimmt einen geänderten Fußnotentext (nur wenn er sich wirklich geändert hat).
    /// </summary>
    private void CommitFootnote(Guid commentId, string text)
    {
        var comment = Document.Annotations.CommentsFor(CurrentPage.Id).FirstOrDefault(candidate => candidate.Id == commentId);
        if (comment is not null && comment.Text != text)
        {
            Document.UpdateAnnotation(CurrentPage.Id, comment with { Text = text });
        }
    }

    /// <summary>
    /// Baut die Fußnoten der angezeigten Seite neu auf.
    /// </summary>
    private void RebuildFootnotes()
    {
        Footnotes.Clear();
        var comments = Document.Annotations.CommentsFor(CurrentPage.Id);
        for (var i = 0; i < comments.Count; i++)
        {
            Footnotes.Add(new FootnoteViewModel(comments[i].Id, i + 1, comments[i].ColorHex, comments[i].Text, CommitFootnote));
        }

        OnPropertyChanged(nameof(HasFootnotes));
    }

    /// <summary>
    /// Reagiert auf Umsortieren, Drehen und Löschen.
    /// </summary>
    private void OnPagesChanged()
    {
        AnnotationList.RequestRefresh();
        BookmarkList.Refresh(CurrentIndex);
        RebuildThumbnails();
        _ = RenderThumbnailsAsync();
        Show(CurrentIndex);
    }

    /// <summary>
    /// Reagiert auf geänderte Annotationen.
    /// </summary>
    private void OnAnnotationsChanged(int pageId)
    {
        AnnotationList.RequestRefresh();
        foreach (var thumbnail in Thumbnails.Where(item => item.Entry.Id == pageId))
        {
            thumbnail.ShowAnnotationKinds(Document.Annotations.For(pageId));
        }

        if (pageId != CurrentPage.Id)
        {
            return;
        }

        RebuildFootnotes();
        CurrentPageAnnotationsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Aktualisiert Statusleiste und Tab-Titel.
    /// </summary>
    private void OnHistoryChanged()
    {
        OnPropertyChanged(nameof(UndoStatus));
        OnPropertyChanged(nameof(TabTitle));
    }

    /// <summary>
    /// Baut die Seitenliste nach der aktuellen Reihenfolge auf; vorhandene Miniaturen werden übernommen.
    /// </summary>
    private void RebuildThumbnails()
    {
        Thumbnails.Clear();
        for (var i = 0; i < Document.Pages.Count; i++)
        {
            var entry = Document.Pages[i];
            var sourceName = entry.IsInserted ? Path.GetFileName(SourceOf(entry).FilePath) : null;
            var thumbnail = new PageThumbnailViewModel(entry, i + 1, sourceName)
            {
                Thumbnail = _thumbnails.GetValueOrDefault((entry.SourceIndex, entry.SourcePageNumber, entry.Rotation))
                    ?? _staleThumbnails.GetValueOrDefault((entry.SourceIndex, entry.SourcePageNumber, entry.Rotation)),
                IsSelected = i == CurrentIndex,
            };
            thumbnail.ShowAnnotationKinds(Document.Annotations.For(entry.Id));
            Thumbnails.Add(thumbnail);
        }

        OnPropertyChanged(nameof(PageStatus));
    }

    /// <summary>
    /// Rendert fehlende oder für eine andere Leistenbreite gerenderte Miniaturen im Hintergrund. Ein neuer Durchlauf
    /// beendet einen laufenden; danach werden nicht mehr gebrauchte Miniaturen freigegeben.
    /// </summary>
    private async Task RenderThumbnailsAsync()
    {
        var run = ++_thumbnailRun;
        var cache = _thumbnails;
        foreach (var thumbnail in Thumbnails.ToList())
        {
            var entry = thumbnail.Entry;
            var key = (entry.SourceIndex, entry.SourcePageNumber, entry.Rotation);
            if (!cache.TryGetValue(key, out var bitmap))
            {
                var source = SourceOf(entry);
                var (pageWidth, pageHeight) = source.PageSize(entry.SourcePageNumber);
                var (width, height) = PageTransform.RotatedSize(pageWidth, pageHeight, entry.Rotation);
                var scale = ThumbnailWidth * _thumbnailScale / Math.Max(width, height / 1.414);
                var rendered = await Task.Run(() => source.Render(entry.SourcePageNumber, scale, entry.Rotation));
                if (run != _thumbnailRun)
                {
                    return;
                }

                bitmap = PageRenderer.ToBitmap(rendered);
                cache[key] = bitmap;
            }

            thumbnail.Thumbnail = bitmap;
        }

        foreach (var stale in _staleThumbnails.Values.Except(cache.Values))
        {
            stale.Dispose();
        }

        _staleThumbnails.Clear();
    }
}
