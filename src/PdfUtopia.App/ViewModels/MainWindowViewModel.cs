using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfUtopia.App.Localization;
using PdfUtopia.App.Services;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Signatures;
using PdfUtopia.Pdf;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.ViewModels;

/// <summary>Inhalt der linken Leiste.</summary>
public enum SidebarView
{
    /// <summary>Seitenliste.</summary>
    Pages,

    /// <summary>Anmerkungsübersicht (A45).</summary>
    Annotations,

    /// <summary>Lesezeichen (A46).</summary>
    Bookmarks,
}

/// <summary>
/// ViewModel des Hauptfensters: geöffnete Dokumente (Tabs), Werkzeug, Zoom, Theme, Sprache, Farbwahl und alle
/// Befehle aus Menü und Werkzeugleiste. Einstellungen werden bei jeder Änderung gespeichert.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>Kleinster Zoom.</summary>
    public const double MinZoom = ZoomFit.MinZoom;

    /// <summary>Größter Zoom.</summary>
    public const double MaxZoom = ZoomFit.MaxZoom;

    private const double ZoomStep = 0.1;

    private readonly IDialogService _dialogs;
    private readonly ThemeService _theme;
    private readonly SettingsStore _settingsStore;
    private readonly SignatureStore _signatureStore;
    private AppSettings _settings;
    private Avalonia.Threading.DispatcherTimer? _thumbnailRenderDelay;

    /// <summary>
    /// Erzeugt das ViewModel.
    /// </summary>
    /// <param name="settingsStore">Ablage der Einstellungen.</param>
    /// <param name="dialogs">Dialoge.</param>
    /// <param name="theme">Theme-Dienst.</param>
    public MainWindowViewModel(SettingsStore settingsStore, IDialogService dialogs, ThemeService theme)
    {
        _settingsStore = settingsStore;
        _dialogs = dialogs;
        _theme = theme;
        _settings = settingsStore.Load();
        ToolOptions = new ToolOptionsViewModel(_settings);
        HighlightFormFields = _settings.HighlightFormFields;
        _signatureStore = new SignatureStore(SignatureStore.PathNextTo(settingsStore.FilePath));
        ToolOptions.SetSignatures(_signatureStore.Load());
        ToolOptions.Changed += (_, _) => SaveSettings(ToolOptions.ApplyTo(_settings));
        ToolOptions.SignatureDialogRequested += async (_, _) => await EditSignaturesAsync(0);
        ToolOptions.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(ToolOptionsViewModel.Tool))
            {
                OnPropertyChanged(nameof(CurrentTool));
                OnPropertyChanged(nameof(FormNotice));
                // Ohne gespeicherte Unterschrift öffnet die Wahl des Werkzeugs gleich das Unterschriftenfeld
                if (ToolOptions.Tool == Tool.Signature && ToolOptions.HasNoSignature)
                {
                    await EditSignaturesAsync(0);
                }
            }
        };
        ColorPicker = new ColorPickerViewModel(_settings.PastelSaturation, _settings.CustomColors, CurrentColorOf);
        ColorPicker.ColorChosen += (_, choice) => OnColorChosen(choice.Target, choice.Hex);
        ColorPicker.PaletteChanged += (_, _) => SaveSettings(_settings with
        {
            PastelSaturation = ColorPicker.Saturation,
            CustomColors = ColorPicker.Custom.Select(swatch => swatch.Hex).ToList(),
        });
        _theme.Apply(_settings.Theme);
        _theme.ApplyAccent(_settings.AccentColor);
    }

    /// <summary>Wird nach einem Sprachwechsel ausgelöst; die App baut das Fenster neu auf.</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Werkzeug und Werkzeugeinstellungen.</summary>
    public ToolOptionsViewModel ToolOptions { get; }

    /// <summary>Gemeinsame Farbwahl.</summary>
    public ColorPickerViewModel ColorPicker { get; }

    /// <summary>Geöffnete Dokumente.</summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    /// <summary>Angezeigtes Dokument.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(NoDocument))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand), nameof(ExtractCommand), nameof(InsertPagesCommand), nameof(PrintCommand), nameof(CloseDocumentCommand),
        nameof(RotateCurrentPageCommand), nameof(RotateCurrentPageClockwiseCommand), nameof(RotateCurrentPageCounterclockwiseCommand), nameof(DeleteCurrentPageCommand), nameof(InsertBlankPageCommand), nameof(UndoCommand), nameof(RedoCommand),
        nameof(RecognizeTextCommand), nameof(MarkRedactionsCommand))]
    public partial DocumentViewModel? ActiveDocument { get; set; }

    /// <summary>Zoom (1 = 100 %).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    public partial double Zoom { get; set; } = 1;

    /// <summary>Suchbegriff.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchHitsText), nameof(HasSearchText))]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Anpassung der Seite an den Anzeigebereich; wird bei jeder Fenstergröße neu angewandt (A33).
    /// Manuelles Zoomen schaltet sie aus.
    /// </summary>
    [ObservableProperty]
    public partial FitMode FitMode { get; set; } = FitMode.Page;

    /// <summary>
    /// Ob Strg gedrückt ist: Drehen-Knöpfe drehen dann gegen den Uhrzeigersinn und zeigen ein gespiegeltes Symbol.
    /// </summary>
    [ObservableProperty]
    public partial bool ReverseRotation { get; set; }

    /// <summary>Ob Formularfelder hervorgehoben werden (A50, Mockup v6); wird gespeichert.</summary>
    [ObservableProperty]
    public partial bool HighlightFormFields { get; set; }

    /// <summary>
    /// Hinweis über der Seite bei Formularen: mit dem Werkzeug „Auswählen“ wie man ausfüllt, sonst dass man dafür
    /// „Auswählen“ wählt.
    /// </summary>
    public string FormNotice => ActiveDocument is { FormFieldCount: > 0 } document
        ? Loc.F(ToolOptions.Tool == Tool.Select ? "Form.Notice" : "Form.NoticeOtherTool", document.FormFieldCount)
        : string.Empty;

    /// <summary>Was die linke Leiste zeigt: Seiten, Anmerkungen (A45) oder Lesezeichen (A46).</summary>
    [ObservableProperty]
    public partial SidebarView SidebarView { get; set; }

    /// <summary>Ob die linke Leiste Reiter zeigt (das Dokument hat Anmerkungen oder Lesezeichen).</summary>
    public bool ShowSidebarTabs => ActiveDocument is { } document && (document.AnnotationList.HasAny || document.BookmarkList.HasAny);

    /// <summary>Breite der linken Leiste in Pixeln (F30).</summary>
    public double SidebarWidth => _settings.SidebarWidth;

    /// <summary>Größe der Miniaturen relativ zur Standardbreite der linken Leiste; wächst und schrumpft mit ihr.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThumbnailBoxWidth), nameof(ThumbnailBoxHeight), nameof(ThumbnailImageMaxWidth), nameof(ThumbnailImageMaxHeight))]
    public partial double ThumbnailScale { get; set; } = 1;

    /// <summary>
    /// Neue Miniaturgröße: nach einer kurzen Pause scharf neu rendern lassen (nicht bei jedem Schritt des Ziehens).
    /// </summary>
    partial void OnThumbnailScaleChanged(double value)
    {
        _thumbnailRenderDelay ??= new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(300), Avalonia.Threading.DispatcherPriority.Background, (_, _) =>
        {
            _thumbnailRenderDelay?.Stop();
            foreach (var document in Documents)
            {
                document.SetThumbnailScale(ThumbnailScale);
            }
        });
        _thumbnailRenderDelay.Stop();
        _thumbnailRenderDelay.Start();
    }

    /// <summary>Breite des Miniaturfelds.</summary>
    public double ThumbnailBoxWidth => 112 * ThumbnailScale;

    /// <summary>Höhe des Miniaturfelds.</summary>
    public double ThumbnailBoxHeight => 158 * ThumbnailScale;

    /// <summary>Größte Breite des Miniaturbilds.</summary>
    public double ThumbnailImageMaxWidth => 106 * ThumbnailScale;

    /// <summary>Größte Höhe des Miniaturbilds.</summary>
    public double ThumbnailImageMaxHeight => 150 * ThumbnailScale;

    /// <summary>Ob ein Suchbegriff eingegeben ist (× zum Leeren sichtbar).</summary>
    public bool HasSearchText => SearchText.Length > 0;

    /// <summary>Ob ein Dokument geöffnet ist.</summary>
    public bool HasDocument => ActiveDocument is not null;

    /// <summary>Ob kein Dokument geöffnet ist (Startansicht).</summary>
    public bool NoDocument => ActiveDocument is null;

    /// <summary>Gewähltes Werkzeug.</summary>
    public Tool CurrentTool => ToolOptions.Tool;

    /// <summary>Seitenanzeige der Statusleiste.</summary>
    public string PageStatus => ActiveDocument?.PageStatus ?? Loc.T("Status.NoDocument");

    /// <summary>Verlaufsanzeige der Statusleiste.</summary>
    public string UndoStatus => ActiveDocument?.UndoStatus ?? string.Empty;

    /// <summary>Zoom als Text.</summary>
    public string ZoomText => $"{Math.Round(Zoom * 100)} %";

    /// <summary>
    /// Inhalt des Zoom-Eingabefelds in der Statusleiste; folgt dem Zoom, bis eine Eingabe übernommen wird.
    /// </summary>
    [ObservableProperty]
    public partial string ZoomInput { get; set; } = "100 %";

    /// <summary>Gewähltes Erscheinungsbild.</summary>
    public ThemePreference Theme => _settings.Theme;

    /// <summary>Gewählte Sprache.</summary>
    public UiLanguage Language => Loc.Language;

    /// <summary>Wirksame Akzentfarbe (für den Farbpunkt).</summary>
    public string AccentHex => _theme.CurrentAccent;

    /// <summary>Treffer der Suche im Dokument.</summary>
    public string SearchHitsText
    {
        get
        {
            if (ActiveDocument is null || string.IsNullOrWhiteSpace(SearchText))
            {
                return string.Empty;
            }

            var hits = SearchHits(ActiveDocument).Sum(hit => hit.Count);
            return hits == 0 ? Loc.T("Toolbar.NoHits") : Loc.F("Toolbar.Hits", hits);
        }
    }

    /// <summary>
    /// Öffnet als Programmargument oder per Drag &amp; Drop übergebene Dateien.
    /// </summary>
    /// <param name="paths">Die Pfade.</param>
    /// <returns>Ein Task.</returns>
    public async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            await OpenFileAsync(path);
        }
    }

    /// <summary>
    /// Fügt Seiten aus PDF-Dateien ein, die auf die Seitenliste gezogen wurden (A41, F26). Für jede Datei erscheint
    /// der Einfügen-Dialog; die Seiten mehrerer Dateien folgen in der Reihenfolge der Dateien aufeinander.
    /// </summary>
    /// <param name="paths">Die Pfade; andere Dateien als PDF werden übergangen.</param>
    /// <param name="index">Position, an der die Datei abgelegt wurde (0 bis Seitenanzahl).</param>
    /// <returns>Ein Task.</returns>
    public async Task InsertPagesFromFilesAsync(IEnumerable<string> paths, int index)
    {
        if (ActiveDocument is not { } document)
        {
            return;
        }

        foreach (var path in paths.Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            var inserted = await InsertPagesAsync(document, Path.GetFullPath(path), InsertPosition.AfterPage, index);
            index += inserted;
        }
    }

    /// <summary>
    /// Fragt vor dem Schließen des Fensters nach ungespeicherten Änderungen aller Dokumente.
    /// </summary>
    /// <returns><c>true</c>, wenn geschlossen werden darf.</returns>
    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (var document in Documents.ToList())
        {
            if (!await ConfirmCloseAsync(document))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Wählt ein Werkzeug.</summary>
    /// <param name="tool">Das Werkzeug.</param>
    [RelayCommand]
    private void SetTool(Tool tool) => ToolOptions.Tool = tool;

    /// <summary>Öffnet Dateien über die Dateiauswahl.</summary>
    [RelayCommand]
    private async Task OpenAsync() => await OpenFilesAsync(await _dialogs.PickPdfFilesAsync());

    /// <summary>Speichert das Dokument unter neuem Namen (F7, F22).</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SaveAsAsync() => await SaveAsync(ActiveDocument!);

    /// <summary>Extrahiert Seiten in eine neue Datei.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task ExtractAsync() => await ExtractAsync(null);

    /// <summary>
    /// Extrahiert Seiten; bei Mehrfachauswahl ist sie im Dialog vorbelegt (A48).
    /// </summary>
    /// <param name="clickedIndex">Angeklickte Miniatur oder <c>null</c>.</param>
    private async Task ExtractAsync(int? clickedIndex)
    {
        var document = ActiveDocument!;
        var preset = document.Selection.IsMultiple || clickedIndex is not null
            ? InsertPlacement.FormatSelection(document.TargetIndices(clickedIndex).Select(index => index + 1))
            : null;
        var positions = await _dialogs.AskPagesToExtractAsync(document.Document.Pages.Count, preset);
        if (positions is null
            || !await ConfirmRedactionsAsync(document, positions.Select(position => document.Document.Pages[position - 1]).ToList(), Loc.T("Redact.Save"))
            || await AskSaveOptionsAsync(document) is not { } options)
        {
            return;
        }

        var target = await _dialogs.PickSaveFileAsync(SuggestedName(document, "Save.ExtractSuffix"));
        if (target is not null)
        {
            await RunExportAsync(() => PdfExporter.Extract(document.Sources, document.Document, positions, target, options));
        }
    }

    /// <summary>Fügt Seiten aus einer anderen PDF-Datei ein (A41).</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task InsertPagesAsync() => await InsertPagesAsync(ActiveDocument!, null, InsertPosition.AfterCurrent, 0);

    /// <summary>
    /// Zeigt den Einfügen-Dialog und fügt die gewählten Seiten ein. Als Quellen stehen die übergebene Datei, die
    /// anderen geöffneten Dokumente (wie gespeichert) und die Dateiauswahl zur Verfügung.
    /// </summary>
    /// <param name="document">Zieldokument.</param>
    /// <param name="presetPath">Vorgewählte Datei (Drag &amp; Drop) oder <c>null</c>.</param>
    /// <param name="position">Vorgewählte Position.</param>
    /// <param name="afterPage">Seite bei <see cref="InsertPosition.AfterPage"/>.</param>
    /// <returns>Anzahl der eingefügten Seiten.</returns>
    private async Task<int> InsertPagesAsync(DocumentViewModel document, string? presetPath, InsertPosition position, int afterPage)
    {
        var sources = new List<InsertSourceOption>();
        if (presetPath is not null)
        {
            sources.Add(OptionFor(presetPath));
        }

        sources.AddRange(Documents
            .Where(other => other != document && other.Source.FilePath != presetPath)
            .Select(other => new InsertSourceOption(other.Source.FilePath, other.FileName, other.Source.Password, other.Document.IsModified, IsOpenDocument: true)));

        if (sources.Count == 0)
        {
            // Ohne andere Quellen gleich die Dateiauswahl zeigen
            if (await _dialogs.PickPdfFileAsync() is not { } picked)
            {
                return 0;
            }

            sources.Add(OptionFor(picked));
        }

        var request = new InsertRequest(sources, document.CurrentIndex, document.Document.Pages.Count, position, afterPage);
        if (await _dialogs.AskPagesToInsertAsync(request) is not { } choice)
        {
            return 0;
        }

        document.InsertPages(choice.Source, choice.Pages, choice.Index);
        return choice.Pages.Count;
    }

    /// <summary>
    /// Quelle für eine Datei; ist sie in einem Tab geöffnet, wird dessen Passwort übernommen.
    /// </summary>
    private InsertSourceOption OptionFor(string path)
    {
        var open = Documents.FirstOrDefault(document => document.Source.FilePath == path);
        return new InsertSourceOption(path, Path.GetFileName(path), open?.Source.Password, open?.Document.IsModified ?? false, open is not null);
    }

    /// <summary>
    /// Übergibt das Dokument an den PDF-Betrachter des Systems zum Drucken (F15, E3).
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task PrintAsync()
    {
        var document = ActiveDocument!;
        var path = Path.Combine(Path.GetTempPath(), $"PDF Utopia – {Path.GetFileNameWithoutExtension(document.FileName)}.pdf");
        var options = new ExportOptions { FootnoteHeading = Loc.T("Footnotes.Heading"), PasswordMode = PasswordMode.Remove };
        if (!await ConfirmRedactionsAsync(document, document.Document.Pages.ToList(), Loc.T("Redact.Print")))
        {
            return;
        }

        if (await RunExportAsync(() => PdfExporter.Save(document.Sources, document.Document, path, options), markSaved: false, document))
        {
            await _dialogs.ShowMessageAsync(Loc.T("Print.Title"), Loc.T("Print.Message"));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    /// <summary>Schließt das angezeigte Dokument.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task CloseDocumentAsync() => await CloseAsync(ActiveDocument!);

    /// <summary>Schließt ein Dokument (Tab).</summary>
    /// <param name="document">Das Dokument.</param>
    [RelayCommand]
    private async Task CloseTabAsync(DocumentViewModel document) => await CloseAsync(document);

    /// <summary>Zeigt ein Dokument an (Tab gewählt).</summary>
    /// <param name="document">Das Dokument.</param>
    [RelayCommand]
    private void ActivateTab(DocumentViewModel document) => ActiveDocument = document;

    /// <summary>Macht den letzten Schritt rückgängig.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void Undo() => ActiveDocument?.Document.History.Undo();

    /// <summary>Wiederholt den letzten Schritt.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void Redo() => ActiveDocument?.Document.History.Redo();

    /// <summary>Dreht die angezeigte Seite.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void RotateCurrentPage() => ActiveDocument?.RotatePage(ActiveDocument.CurrentIndex, clockwise: !ReverseRotation);

    /// <summary>Dreht die angezeigte bzw. gewählten Seiten im Uhrzeigersinn (Menü, unabhängig von Strg).</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void RotateCurrentPageClockwise() => ActiveDocument?.RotatePage(ActiveDocument.CurrentIndex, clockwise: true);

    /// <summary>Dreht eine Seite bzw. die Auswahl im Uhrzeigersinn (Kontextmenü der Seitenliste, unabhängig von Strg).</summary>
    /// <param name="page">Die angeklickte Seite.</param>
    [RelayCommand]
    private void RotatePageClockwise(PageThumbnailViewModel page) => ActiveDocument?.RotatePage(page.Position - 1, clockwise: true);

    /// <summary>Dreht die angezeigte bzw. gewählten Seiten gegen den Uhrzeigersinn (Menü).</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void RotateCurrentPageCounterclockwise() => ActiveDocument?.RotatePage(ActiveDocument.CurrentIndex, clockwise: false);

    /// <summary>Dreht eine Seite bzw. die Auswahl gegen den Uhrzeigersinn (Kontextmenü der Seitenliste).</summary>
    /// <param name="page">Die angeklickte Seite.</param>
    [RelayCommand]
    private void RotatePageCounterclockwise(PageThumbnailViewModel page) => ActiveDocument?.RotatePage(page.Position - 1, clockwise: false);

    /// <summary>Fügt nach der angezeigten Seite eine leere Seite ein (A48).</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void InsertBlankPage() => ActiveDocument?.InsertBlankPage(ActiveDocument.CurrentIndex);

    /// <summary>Fügt nach einer Seite der Seitenliste eine leere Seite ein (Kontextmenü).</summary>
    /// <param name="page">Die Seite davor.</param>
    [RelayCommand]
    private void InsertBlankPageAfter(PageThumbnailViewModel page) => ActiveDocument?.InsertBlankPage(page.Position - 1);

    /// <summary>Extrahiert die Seite bzw. die gewählten Seiten (Kontextmenü der Seitenliste).</summary>
    /// <param name="page">Die angeklickte Seite.</param>
    [RelayCommand]
    private async Task ExtractPageAsync(PageThumbnailViewModel page) => await ExtractAsync(page.Position - 1);

    /// <summary>Öffnet den Dialog „Unterschriften“ (A49).</summary>
    [RelayCommand]
    private async Task ManageSignaturesAsync() => await EditSignaturesAsync(ToolOptions.SelectedSignatureIndex);

    /// <summary>Öffnet den Dialog „Unterschriften“ auf dem nächsten freien Platz.</summary>
    [RelayCommand]
    private async Task NewSignatureAsync() => await EditSignaturesAsync(ToolOptions.Signatures.Count);

    /// <summary>Wählt eine Unterschrift in der Optionsleiste.</summary>
    /// <param name="choice">Die Unterschrift.</param>
    [RelayCommand]
    private void SelectSignature(SignatureChoice choice) => ToolOptions.SelectSignature(choice.Index);

    /// <summary>Suchbegriff für „Suchen und schwärzen“ (A52, F39).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RedactHitsText))]
    [NotifyCanExecuteChangedFor(nameof(MarkRedactionsCommand))]
    public partial string RedactTerm { get; set; } = string.Empty;

    /// <summary>Trefferzahl für „Suchen und schwärzen“, z. B. „13 Fundstellen auf 13 Seiten“.</summary>
    public string RedactHitsText
    {
        get
        {
            if (ActiveDocument is not { } document || string.IsNullOrWhiteSpace(RedactTerm))
            {
                return string.Empty;
            }

            var hits = RedactionHits(document);
            return hits.Count == 0 ? Loc.T("Redact.NoHits") : Loc.F("Redact.Hits", hits.Count, hits.Select(hit => hit.PageId).Distinct().Count());
        }
    }

    /// <summary>Anzahl der markierten Bereiche im Dokument als Hinweis.</summary>
    public string RedactPendingText => Loc.F("Redact.Pending", PendingRedactions(ActiveDocument).Count);

    /// <summary>Ob im Dokument Bereiche zum Schwärzen markiert sind.</summary>
    public bool HasPendingRedactions => PendingRedactions(ActiveDocument).Count > 0;

    /// <summary>Ob „Alle markieren“ möglich ist.</summary>
    private bool CanMarkRedactions => ActiveDocument is not null && !string.IsNullOrWhiteSpace(RedactTerm);

    /// <summary>Markiert jede Fundstelle des Suchbegriffs im Dokument zum Schwärzen (ein Rückgängig-Schritt).</summary>
    [RelayCommand(CanExecute = nameof(CanMarkRedactions))]
    private void MarkRedactions()
    {
        if (ActiveDocument is not { } document)
        {
            return;
        }

        document.Document.AddAnnotations(RedactionHits(document)
            .Select(hit => (hit.PageId, (Core.Annotations.Annotation)RedactionAnnotation.ForSearchHit(hit.Bounds))).ToList());
    }

    /// <summary>
    /// Fundstellen des Suchbegriffs je Seite in der aktuellen Reihenfolge (Koordinaten der ungedrehten Seite).
    /// </summary>
    private List<(int PageId, PdfRect Bounds)> RedactionHits(DocumentViewModel document)
    {
        var term = RedactTerm.Trim();
        return document.Document.Pages
            .SelectMany(page => document.FindText(page, term).Select(rect => (page.Id, rect)))
            .ToList();
    }

    /// <summary>Markierte Bereiche je Seite (Seite und Annotation).</summary>
    private static List<(PageEntry Page, RedactionAnnotation Redaction)> PendingRedactions(DocumentViewModel? document, IEnumerable<PageEntry>? pages = null) =>
        document is null
            ? []
            : (pages ?? document.Document.Pages)
                .SelectMany(page => document.Document.Annotations.For(page.Id).OfType<RedactionAnnotation>().Select(redaction => (page, redaction)))
                .ToList();

    /// <summary>
    /// Fragt vor dem Schreiben nach, wenn Seiten Schwärzungen enthalten (A52).
    /// </summary>
    /// <param name="document">Das Dokument.</param>
    /// <param name="pages">Die zu schreibenden Seiten in Zielreihenfolge.</param>
    /// <param name="confirmText">Text der bestätigenden Schaltfläche.</param>
    /// <returns><c>true</c>, wenn weitergemacht wird (auch ohne Schwärzungen).</returns>
    private async Task<bool> ConfirmRedactionsAsync(DocumentViewModel document, IReadOnlyList<PageEntry> pages, string confirmText)
    {
        var pending = PendingRedactions(document, pages);
        if (pending.Count == 0)
        {
            return true;
        }

        var numbers = pages.Select((page, index) => (page, index)).Where(item => pending.Any(entry => entry.Page.Id == item.page.Id))
            .Select(item => item.index + 1).ToList();
        return await _dialogs.ConfirmRedactionAsync(numbers, pending.Count, confirmText);
    }

    /// <summary>
    /// Texterkennung (A53): sucht Tesseract, zeigt den Dialog und übernimmt den erkannten Text als einen
    /// Rückgängig-Schritt (auch die bis zu einem Abbruch erkannten Seiten).
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task RecognizeTextAsync()
    {
        var document = ActiveDocument!;
        var engine = await Task.Run(Pdf.Ocr.TesseractEngine.Find);
        while (engine is null)
        {
            if (!await _dialogs.ShowTesseractMissingAsync())
            {
                return;
            }

            engine = await Task.Run(Pdf.Ocr.TesseractEngine.Find);
        }

        var results = new Dictionary<int, Core.Text.RecognizedPage>();
        var request = new OcrRequest(
            Loc.F("Ocr.Engine", engine.Version, string.Join(", ", engine.Languages.Select(LanguageName))),
            document.Document.Pages.Count,
            document.PagesWithoutText.Select(index => index + 1).ToList(),
            OcrLanguages(engine.Languages),
            (positions, language, progress, cancellation) => Task.Run(async () =>
            {
                var words = 0;
                for (var i = 0; i < positions.Count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    progress.Report(new OcrProgress(i, positions.Count, positions[i]));
                    var page = document.Document.Pages[positions[i] - 1];
                    var recognized = await engine.RecognizeAsync(document.SourceOf(page), page.SourcePageNumber, page.Rotation, language, cancellation);
                    lock (results)
                    {
                        results[page.Id] = recognized;
                    }

                    words += recognized.Words.Count;
                }

                return words;
            }, cancellation));

        if (await _dialogs.RecognizeTextAsync(request) is not { } outcome)
        {
            return;
        }

        // Auch bei Abbruch oder Fehler bleiben die bis dahin erkannten Seiten erhalten
        document.Document.SetRecognizedText(results);
        if (outcome.Error is { } error)
        {
            await _dialogs.ShowMessageAsync(Loc.T("Ocr.Title"), Loc.F("Ocr.Failed", error));
            return;
        }

        var wordCount = results.Values.Sum(page => page.Words.Count);
        ShowStatusNotice(outcome.Cancelled ? Loc.F("Ocr.Cancelled", results.Count) : Loc.F("Ocr.Done", results.Count, wordCount));
    }

    /// <summary>Anzeigename eines Tesseract-Sprachkürzels.</summary>
    private static string LanguageName(string code) => Loc.T($"Ocr.Lang.{code}") is var name && name != $"Ocr.Lang.{code}" ? name : code;

    /// <summary>
    /// Wählbare Sprachen: die Oberflächensprache zusammen mit Englisch zuerst, dann einzelne Sprachen.
    /// </summary>
    private static List<(string Code, string Name)> OcrLanguages(IReadOnlyList<string> installed)
    {
        var preferred = Loc.Language == UiLanguage.German ? "deu" : "eng";
        var other = preferred == "deu" ? "eng" : "deu";
        var codes = new List<string>();
        if (installed.Contains(preferred) && installed.Contains(other))
        {
            codes.Add($"{preferred}+{other}");
        }

        codes.AddRange(installed.OrderBy(code => code == preferred ? 0 : code == other ? 1 : 2).ThenBy(code => code, StringComparer.Ordinal));
        return codes.Select(code => (code, LanguageName(code))).ToList();
    }

    /// <summary>Wählt einen Stempel in der Optionsleiste (A51).</summary>
    /// <param name="choice">Der Stempel.</param>
    [RelayCommand]
    private void SelectStamp(StampChoice choice)
    {
        ToolOptions.SelectStamp(choice.Index);
        ToolOptions.PendingImage = null;
    }

    /// <summary>Setzt die Farbe des nächsten Stempels.</summary>
    /// <param name="hex">Die Farbe.</param>
    [RelayCommand]
    private void SetStampColor(string hex) => ToolOptions.StampColor = hex;

    /// <summary>Legt einen eigenen Stempel an und wählt ihn.</summary>
    [RelayCommand]
    private async Task NewStampAsync()
    {
        if (await _dialogs.AskCustomStampAsync(ToolOptions.StampDetail(DateTime.Today)) is not { } stamp)
        {
            return;
        }

        SaveCustomStamps([.. ToolOptions.CustomStamps, stamp]);
        ToolOptions.SelectStamp(ToolOptions.Stamps.Count - 1);
        ToolOptions.PendingImage = null;
    }

    /// <summary>Löscht einen eigenen Stempel.</summary>
    /// <param name="choice">Der Stempel.</param>
    [RelayCommand]
    private void DeleteStamp(StampChoice choice)
    {
        if (!choice.IsCustom)
        {
            return;
        }

        var customIndex = choice.Index - StampAnnotation.Presets.Count;
        var selected = ToolOptions.SelectedStampIndex;
        SaveCustomStamps(ToolOptions.CustomStamps.Where((_, i) => i != customIndex).ToList());
        if (selected == choice.Index)
        {
            ToolOptions.SelectStamp(0);
        }
        else if (selected > choice.Index)
        {
            ToolOptions.SelectStamp(selected - 1);
        }
    }

    /// <summary>Wählt ein Bild, das mit dem nächsten Klick auf die Seite gesetzt wird (A51).</summary>
    [RelayCommand]
    private async Task ChooseImageAsync()
    {
        if (await _dialogs.PickImageFileAsync() is not { } path)
        {
            return;
        }

        if (await LoadImageAsync(path) is { } loaded)
        {
            ToolOptions.PendingImage = new PendingImage(loaded.Image, Path.GetFileName(path));
        }
    }

    /// <summary>Verwirft das gewählte, noch nicht gesetzte Bild.</summary>
    [RelayCommand]
    private void CancelImage() => ToolOptions.PendingImage = null;

    /// <summary>Fügt ein Bild aus der Zwischenablage mittig auf der aktuellen Seite ein (Strg+V).</summary>
    [RelayCommand]
    private async Task PasteImageAsync()
    {
        if (ActiveDocument is not { } document || await _dialogs.ReadClipboardImageAsync() is not { } loaded)
        {
            return;
        }

        var (width, height) = document.CurrentDisplaySize;
        PlaceImage(document, loaded, new PdfPoint(width / 2, height / 2));
    }

    /// <summary>
    /// Liest eine Bilddatei; meldet nicht lesbare Bilder und eine Verkleinerung.
    /// </summary>
    /// <param name="path">Pfad der Datei.</param>
    /// <returns>Das Bild oder <c>null</c>.</returns>
    public async Task<LoadedImage?> LoadImageAsync(string path)
    {
        LoadedImage? loaded = null;
        try
        {
            loaded = ImageLoader.FromBytes(await File.ReadAllBytesAsync(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // wie ein nicht lesbares Bild behandeln
        }

        if (loaded is null)
        {
            await _dialogs.ShowMessageAsync(Loc.T("Stamp.ImageTitle"), Loc.F("Stamp.ImageFailed", Path.GetFileName(path)));
            return null;
        }

        ReportShrunk(loaded);
        return loaded;
    }

    /// <summary>
    /// Setzt ein Bild in Standardbreite mit der Mitte auf einen Punkt der Anzeige, schaltet auf das Werkzeug „Stempel“
    /// und wählt das Bild, damit es gleich verschoben werden kann (Drag &amp; Drop, Zwischenablage).
    /// </summary>
    /// <param name="document">Das Dokument.</param>
    /// <param name="loaded">Das Bild.</param>
    /// <param name="display">Mitte in Anzeige-Koordinaten der aktuellen Seite.</param>
    public void PlaceImage(DocumentViewModel document, LoadedImage loaded, PdfPoint display)
    {
        ReportShrunk(loaded);
        var image = document.PlaceBox(ImageAnnotation.Create(loaded.Image, display, ImageAnnotation.DefaultWidth));
        ToolOptions.Tool = Tool.Stamp;
        document.SelectedBox = image.Id;
    }

    /// <summary>Meldet in der Statusleiste, dass ein Bild verkleinert wurde (F36).</summary>
    private void ReportShrunk(LoadedImage loaded)
    {
        if (loaded.WasShrunk)
        {
            ShowStatusNotice(Loc.F("Stamp.ImageShrunk", loaded.Image.PixelWidth, loaded.Image.PixelHeight, loaded.OriginalWidth, loaded.OriginalHeight));
        }
    }

    /// <summary>Kurzer Hinweis in der Statusleiste.</summary>
    [ObservableProperty]
    public partial string StatusNotice { get; set; } = string.Empty;

    private Avalonia.Threading.DispatcherTimer? _noticeTimer;

    /// <summary>
    /// Zeigt einen Hinweis einige Sekunden lang in der Statusleiste.
    /// </summary>
    /// <param name="text">Der Hinweis.</param>
    private void ShowStatusNotice(string text)
    {
        StatusNotice = text;
        _noticeTimer?.Stop();
        _noticeTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromSeconds(6), Avalonia.Threading.DispatcherPriority.Background, (_, _) =>
        {
            StatusNotice = string.Empty;
            _noticeTimer?.Stop();
        });
        _noticeTimer.Start();
    }

    /// <summary>
    /// Speichert die eigenen Stempel und baut die Auswahl neu auf.
    /// </summary>
    private void SaveCustomStamps(IReadOnlyList<CustomStamp> stamps)
    {
        SaveSettings(_settings with { CustomStamps = stamps });
        ToolOptions.SetCustomStamps(_settings.CustomStamps);
    }

    /// <summary>
    /// Zeigt den Unterschriften-Dialog; Änderungen werden sofort gespeichert.
    /// </summary>
    private async Task EditSignaturesAsync(int startSlot)
    {
        var signatures = await _dialogs.EditSignaturesAsync(ToolOptions.Signatures.Select(choice => choice.Signature).ToList(), startSlot, SaveSignatures);
        ToolOptions.SetSignatures(signatures);
    }

    /// <summary>
    /// Speichert die Unterschriften; ein nicht beschreibbarer Ordner lässt das Programm bedienbar.
    /// </summary>
    private void SaveSignatures(IReadOnlyList<Signature> signatures)
    {
        try
        {
            _signatureStore.Save(signatures);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Unterschriften bleiben für diese Sitzung erhalten
        }

        ToolOptions.SetSignatures(signatures);
    }

    /// <summary>Hervorhebung der Formularfelder gewählt: merken.</summary>
    partial void OnHighlightFormFieldsChanged(bool value) => SaveSettings(_settings with { HighlightFormFields = value });

    /// <summary>Wählt alle Seiten der Seitenliste (Strg+A, A48).</summary>
    [RelayCommand]
    private void SelectAllPages() => ActiveDocument?.SelectAllPages();

    /// <summary>Hebt die Mehrfachauswahl auf (Esc).</summary>
    [RelayCommand]
    private void ClearPageSelection() => ActiveDocument?.ClearSelection();

    /// <summary>Löscht die angezeigte Seite.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void DeleteCurrentPage() => ActiveDocument?.DeletePage(ActiveDocument.CurrentIndex);

    /// <summary>Dreht eine Seite aus der Seitenliste.</summary>
    /// <param name="page">Die Seite.</param>
    [RelayCommand]
    private void RotatePage(PageThumbnailViewModel page) => ActiveDocument?.RotatePage(page.Position - 1, clockwise: !ReverseRotation);

    /// <summary>Löscht eine Seite aus der Seitenliste.</summary>
    /// <param name="page">Die Seite.</param>
    [RelayCommand]
    private void DeletePage(PageThumbnailViewModel page) => ActiveDocument?.DeletePage(page.Position - 1);

    /// <summary>Springt zu einer Anmerkung aus der Übersicht (A45).</summary>
    /// <param name="item">Die Zeile.</param>
    [RelayCommand]
    private void ShowAnnotation(AnnotationListItem item)
    {
        if (item.Entry is { } entry)
        {
            ActiveDocument?.ShowAnnotation(entry);
        }
    }

    /// <summary>Zeigt die Zielseite eines Lesezeichens (A46).</summary>
    /// <param name="item">Das Lesezeichen.</param>
    [RelayCommand]
    private void ShowBookmark(BookmarkItemViewModel item)
    {
        if (item.Bookmark.PageIndex is { } index)
        {
            ActiveDocument?.Show(index);
        }
    }

    /// <summary>Zeigt eine Seite aus der Seitenliste an.</summary>
    /// <param name="page">Die Seite.</param>
    [RelayCommand]
    private void ShowPage(PageThumbnailViewModel page) => ActiveDocument?.Show(page.Position - 1);

    /// <summary>Vergrößert (beendet die Anpassung an den Anzeigebereich).</summary>
    [RelayCommand]
    private void ZoomIn()
    {
        FitMode = FitMode.None;
        Zoom = Math.Min(MaxZoom, Math.Round(Zoom + ZoomStep, 1));
    }

    /// <summary>Verkleinert (beendet die Anpassung an den Anzeigebereich).</summary>
    [RelayCommand]
    private void ZoomOut()
    {
        FitMode = FitMode.None;
        Zoom = Math.Max(MinZoom, Math.Round(Zoom - ZoomStep, 1));
    }

    /// <summary>
    /// Wendet den Zoom einer Anpassung an, ohne sie zu beenden (aufgerufen von der Ansicht bei Größenänderung).
    /// </summary>
    /// <param name="zoom">Der berechnete Zoom.</param>
    public void ApplyFitZoom(double zoom) => Zoom = zoom;

    /// <summary>
    /// Übernimmt einen eingegebenen Zoom (beendet die Anpassung an den Anzeigebereich); ungültige Eingaben werden
    /// verworfen.
    /// </summary>
    [RelayCommand]
    private void ApplyZoomInput()
    {
        if (ZoomFit.ParsePercent(ZoomInput) is { } zoom)
        {
            FitMode = FitMode.None;
            Zoom = zoom;
        }

        ResetZoomInput();
    }

    /// <summary>Stellt im Zoom-Eingabefeld den aktuellen Zoom wieder her.</summary>
    [RelayCommand]
    private void ResetZoomInput() => ZoomInput = ZoomText;

    /// <summary>Neuer Zoom: Eingabefeld nachführen.</summary>
    partial void OnZoomChanged(double value) => ZoomInput = ZoomText;

    /// <summary>Beendet die Suche und leert das Suchfeld.</summary>
    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    /// <summary>Springt zur nächsten Seite mit Treffern der Suche.</summary>
    [RelayCommand]
    private void NextSearchHit()
    {
        if (ActiveDocument is not { } document || string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        var pages = SearchHits(document).Where(hit => hit.Count > 0).Select(hit => hit.Index).ToList();
        if (pages.Count > 0)
        {
            document.Show(pages.FirstOrDefault(index => index > document.CurrentIndex, pages[0]));
        }
    }

    /// <summary>Setzt das Erscheinungsbild.</summary>
    /// <param name="theme">Die Auswahl.</param>
    [RelayCommand]
    private void SetTheme(ThemePreference theme)
    {
        SaveSettings(_settings with { Theme = theme });
        _theme.Apply(theme);
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(AccentHex));
    }

    /// <summary>Setzt die Sprache und baut das Fenster neu auf.</summary>
    /// <param name="language">Die Sprache.</param>
    [RelayCommand]
    private void SetLanguage(UiLanguage language)
    {
        SaveSettings(_settings with { Language = language });
        if (Loc.Language != language)
        {
            Loc.Language = language;
            ToolOptions.SetCustomStamps(ToolOptions.CustomStamps);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Zeigt die Einstellungen.</summary>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var choice = await _dialogs.EditSettingsAsync(new SettingsChoice(_settings.Theme, Loc.Language, _settings.UndoLimit, _settings.UserName));
        if (choice is null)
        {
            return;
        }

        SaveSettings(_settings with { UndoLimit = choice.UndoLimit, UserName = choice.UserName });
        ToolOptions.UserName = _settings.UserName;
        foreach (var document in Documents)
        {
            document.Document.History.Limit = choice.UndoLimit;
        }

        SetTheme(choice.Theme);
        SetLanguage(choice.Language);
    }

    /// <summary>Bereitet die Farbwahl für ein Ziel vor (wird vor dem Öffnen des Flyouts aufgerufen).</summary>
    /// <param name="target">Das Ziel.</param>
    [RelayCommand]
    private void PrepareColorPicker(ColorTarget target) => ColorPicker.OpenFor(target);

    /// <summary>Setzt die Markerfarbe.</summary>
    /// <param name="hex">Die Farbe.</param>
    [RelayCommand]
    private void SetMarkerColor(string hex) => ToolOptions.MarkerColor = hex;

    /// <summary>
    /// Öffnet eine Datei; fragt bei geschützten Dateien nach dem Passwort, bis es stimmt oder abgebrochen wird.
    /// </summary>
    private async Task OpenFileAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (Documents.FirstOrDefault(document => document.Source.FilePath == fullPath) is { } open)
        {
            ActiveDocument = open;
            return;
        }

        string? password = null;
        var wrong = false;
        while (true)
        {
            try
            {
                var source = await Task.Run(() => PdfSource.Open(fullPath, password));
                Add(new DocumentViewModel(source, _settings.UndoLimit, ThumbnailScale));
                return;
            }
            catch (PdfOpenException exception) when (exception.Error == PdfOpenError.PasswordRequired)
            {
                password = await _dialogs.PromptPasswordAsync(Path.GetFileName(fullPath), wrong);
                wrong = true;
                if (password is null)
                {
                    return;
                }
            }
            catch (PdfOpenException exception)
            {
                await _dialogs.ShowMessageAsync(Loc.T("Error.OpenTitle"), Loc.F($"Error.{exception.Error}", Path.GetFileName(fullPath)));
                return;
            }
        }
    }

    /// <summary>
    /// Nimmt ein geöffnetes Dokument als Tab auf und zeigt es an.
    /// </summary>
    private void Add(DocumentViewModel document)
    {
        Documents.Add(document);
        document.PropertyChanged += (_, e) =>
        {
            if (document != ActiveDocument)
            {
                return;
            }

            if (e.PropertyName is nameof(DocumentViewModel.UndoStatus))
            {
                OnPropertyChanged(nameof(UndoStatus));
                OnPropertyChanged(nameof(SearchHitsText));
                OnPropertyChanged(nameof(RedactHitsText));
                OnPropertyChanged(nameof(RedactPendingText));
                OnPropertyChanged(nameof(HasPendingRedactions));
            }
            else if (e.PropertyName is nameof(DocumentViewModel.PageStatus))
            {
                OnPropertyChanged(nameof(PageStatus));
            }
        };
        ActiveDocument = document;
        document.Show(0);
    }

    /// <summary>
    /// Speichert unter neuem Namen; fragt vorher nach dem Passwortschutz.
    /// </summary>
    /// <returns><c>true</c>, wenn gespeichert wurde.</returns>
    private async Task<bool> SaveAsync(DocumentViewModel document)
    {
        if (!await ConfirmRedactionsAsync(document, document.Document.Pages.ToList(), Loc.T("Redact.Save"))
            || await AskSaveOptionsAsync(document) is not { } options)
        {
            return false;
        }

        var target = await _dialogs.PickSaveFileAsync(SuggestedName(document, "Save.SuggestedSuffix"));
        return target is not null && await RunExportAsync(() => PdfExporter.Save(document.Sources, document.Document, target, options), true, document);
    }

    /// <summary>
    /// Fragt nach dem Passwortschutz und baut die Exporteinstellungen.
    /// </summary>
    private async Task<ExportOptions?> AskSaveOptionsAsync(DocumentViewModel document)
    {
        var choice = await _dialogs.AskPasswordModeAsync(document.IsProtected);
        return choice is { } value
            ? new ExportOptions { FootnoteHeading = Loc.T("Footnotes.Heading"), PasswordMode = value.Mode, NewPassword = value.NewPassword }
            : null;
    }

    /// <summary>
    /// Führt einen Export im Hintergrund aus und meldet Fehler.
    /// </summary>
    /// <returns><c>true</c> bei Erfolg.</returns>
    private async Task<bool> RunExportAsync(Action export, bool markSaved = false, DocumentViewModel? document = null)
    {
        try
        {
            await Task.Run(export);
            if (markSaved && document is not null)
            {
                document.Document.MarkSaved();
                document.NotifySaved();
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            await _dialogs.ShowMessageAsync(Loc.T("Error.SaveTitle"), Loc.F("Error.SaveMessage", exception.Message));
            return false;
        }
    }

    /// <summary>
    /// Schließt ein Dokument nach Rückfrage bei ungespeicherten Änderungen.
    /// </summary>
    private async Task CloseAsync(DocumentViewModel document)
    {
        if (!await ConfirmCloseAsync(document))
        {
            return;
        }

        var index = Documents.IndexOf(document);
        Documents.Remove(document);
        document.Dispose();
        if (ActiveDocument == document)
        {
            ActiveDocument = Documents.Count == 0 ? null : Documents[Math.Min(index, Documents.Count - 1)];
        }
    }

    /// <summary>
    /// Fragt bei ungespeicherten Änderungen nach Speichern, Verwerfen oder Abbrechen.
    /// </summary>
    /// <returns><c>true</c>, wenn geschlossen werden darf.</returns>
    private async Task<bool> ConfirmCloseAsync(DocumentViewModel document)
    {
        if (!document.Document.IsModified)
        {
            return true;
        }

        ActiveDocument = document;
        return await _dialogs.AskUnsavedChangesAsync(document.FileName) switch
        {
            UnsavedChangesChoice.Discard => true,
            UnsavedChangesChoice.Save => await SaveAsync(document),
            _ => false,
        };
    }

    /// <summary>
    /// Trefferzahl je Seite in der aktuellen Reihenfolge (ohne Groß-/Kleinschreibung).
    /// </summary>
    private IEnumerable<(int Index, int Count)> SearchHits(DocumentViewModel document)
    {
        for (var i = 0; i < document.Document.Pages.Count; i++)
        {
            yield return (i, PdfSource.CountOccurrences(document.PageText(document.Document.Pages[i]), SearchText));
        }
    }

    /// <summary>
    /// Vorgeschlagener Dateiname, z. B. „bericht-bearbeitet.pdf“.
    /// </summary>
    private static string SuggestedName(DocumentViewModel document, string suffixKey) =>
        $"{Path.GetFileNameWithoutExtension(document.FileName)}{Loc.T(suffixKey)}.pdf";

    /// <summary>
    /// Aktuelle Farbe eines Ziels der Farbwahl (<c>null</c> = Standard).
    /// </summary>
    private string? CurrentColorOf(ColorTarget target) =>
        target == ColorTarget.Accent ? _settings.AccentColor : ToolOptions.ColorOf(target);

    /// <summary>
    /// Übernimmt eine in der Farbwahl gewählte Farbe.
    /// </summary>
    private void OnColorChosen(ColorTarget target, string? hex)
    {
        if (target == ColorTarget.Accent)
        {
            SaveSettings(_settings with { AccentColor = hex });
            _theme.ApplyAccent(hex);
            OnPropertyChanged(nameof(AccentHex));
            return;
        }

        ToolOptions.SetColor(target, hex ?? ToolOptionsViewModel.DefaultColorOf(target));
    }

    /// <summary>
    /// Merkt und speichert Einstellungen.
    /// </summary>
    private void SaveSettings(AppSettings settings)
    {
        _settings = settings.Normalized();
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Einstellungen nicht speicherbar (z. B. schreibgeschütztes Profil): Programm bleibt bedienbar
        }
    }

    /// <summary>
    /// Hinterlegt die Fundstellen des neuen Suchbegriffs.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        if (ActiveDocument is { } document)
        {
            document.SearchTerm = value;
        }
    }

    /// <summary>
    /// Beobachtet die Anmerkungsübersicht des angezeigten Dokuments.
    /// </summary>
    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.AnnotationList.PropertyChanged -= OnAnnotationListPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.AnnotationList.PropertyChanged += OnAnnotationListPropertyChanged;
        }

        LeaveEmptySidebarView();
    }

    /// <summary>
    /// Letzte Anmerkung entfernt: Reiter und Ansicht der linken Leiste prüfen.
    /// </summary>
    private void OnAnnotationListPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnnotationListViewModel.HasAny))
        {
            LeaveEmptySidebarView();
        }
    }

    /// <summary>
    /// Reiter gibt es nur für vorhandene Inhalte (A45, A46); ohne Inhalt zeigt die linke Leiste die Seitenliste.
    /// </summary>
    private void LeaveEmptySidebarView()
    {
        var document = ActiveDocument;
        if ((SidebarView == SidebarView.Annotations && document?.AnnotationList.HasAny != true)
            || (SidebarView == SidebarView.Bookmarks && document?.BookmarkList.HasAny != true))
        {
            SidebarView = SidebarView.Pages;
        }

        OnPropertyChanged(nameof(ShowSidebarTabs));
        OnPropertyChanged(nameof(FormNotice));
    }

    /// <summary>
    /// Merkt sich die per Ziehen gewählte Breite der linken Leiste (F30).
    /// </summary>
    /// <param name="width">Die Breite.</param>
    public void SaveSidebarWidth(double width) => SaveSettings(_settings with { SidebarWidth = width });

    /// <summary>
    /// Passt die Miniaturen beim Ziehen an die Breite der linken Leiste an; scharf neu gerendert wird, sobald sich die
    /// Breite kurz nicht mehr ändert.
    /// </summary>
    /// <param name="width">Aktuelle Breite der Leiste.</param>
    public void ResizeSidebar(double width) =>
        ThumbnailScale = Math.Clamp(width, AppSettings.MinSidebarWidth, AppSettings.MaxSidebarWidth) / AppSettings.DefaultSidebarWidth;

    /// <summary>
    /// Aktualisiert abhängige Anzeigen beim Wechsel des Dokuments.
    /// </summary>
    partial void OnActiveDocumentChanged(DocumentViewModel? value)
    {
        OnPropertyChanged(nameof(RedactHitsText));
        OnPropertyChanged(nameof(RedactPendingText));
        OnPropertyChanged(nameof(HasPendingRedactions));
        foreach (var document in Documents)
        {
            document.IsActive = document == value;
        }

        if (value is not null)
        {
            value.SearchTerm = SearchText;
        }

        OnPropertyChanged(nameof(SearchHitsText));
        OnPropertyChanged(nameof(PageStatus));
        OnPropertyChanged(nameof(UndoStatus));
    }
}
