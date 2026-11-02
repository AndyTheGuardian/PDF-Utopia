using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfUtopia.App.Localization;
using PdfUtopia.App.Services;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Settings;
using PdfUtopia.Pdf;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.ViewModels;

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
    private AppSettings _settings;

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
        ToolOptions.Changed += (_, _) => SaveSettings(ToolOptions.ApplyTo(_settings));
        ToolOptions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToolOptionsViewModel.Tool))
            {
                OnPropertyChanged(nameof(CurrentTool));
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
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand), nameof(ExtractCommand), nameof(PrintCommand), nameof(CloseDocumentCommand),
        nameof(RotateCurrentPageCommand), nameof(DeleteCurrentPageCommand), nameof(UndoCommand), nameof(RedoCommand))]
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
    private async Task ExtractAsync()
    {
        var document = ActiveDocument!;
        var positions = await _dialogs.AskPagesToExtractAsync(document.Document.Pages.Count);
        if (positions is null || await AskSaveOptionsAsync(document) is not { } options)
        {
            return;
        }

        var target = await _dialogs.PickSaveFileAsync(SuggestedName(document, "Save.ExtractSuffix"));
        if (target is not null)
        {
            await RunExportAsync(() => PdfExporter.Extract(document.Source, document.Document, positions, target, options));
        }
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
        if (await RunExportAsync(() => PdfExporter.Save(document.Source, document.Document, path, options), markSaved: false, document))
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
    private void RotateCurrentPage() => ActiveDocument?.RotatePage(ActiveDocument.CurrentIndex);

    /// <summary>Löscht die angezeigte Seite.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void DeleteCurrentPage() => ActiveDocument?.DeletePage(ActiveDocument.CurrentIndex);

    /// <summary>Dreht eine Seite aus der Seitenliste.</summary>
    /// <param name="page">Die Seite.</param>
    [RelayCommand]
    private void RotatePage(PageThumbnailViewModel page) => ActiveDocument?.RotatePage(page.Position - 1);

    /// <summary>Löscht eine Seite aus der Seitenliste.</summary>
    /// <param name="page">Die Seite.</param>
    [RelayCommand]
    private void DeletePage(PageThumbnailViewModel page) => ActiveDocument?.DeletePage(page.Position - 1);

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
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Zeigt die Einstellungen.</summary>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var choice = await _dialogs.EditSettingsAsync(new SettingsChoice(_settings.Theme, Loc.Language, _settings.UndoLimit));
        if (choice is null)
        {
            return;
        }

        SaveSettings(_settings with { UndoLimit = choice.UndoLimit });
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
                Add(new DocumentViewModel(source, _settings.UndoLimit));
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
        if (await AskSaveOptionsAsync(document) is not { } options)
        {
            return false;
        }

        var target = await _dialogs.PickSaveFileAsync(SuggestedName(document, "Save.SuggestedSuffix"));
        return target is not null && await RunExportAsync(() => PdfExporter.Save(document.Source, document.Document, target, options), true, document);
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
            yield return (i, PdfSource.CountOccurrences(document.Source.PageText(document.Document.Pages[i].SourcePageNumber), SearchText));
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
    /// Aktualisiert abhängige Anzeigen beim Wechsel des Dokuments.
    /// </summary>
    partial void OnActiveDocumentChanged(DocumentViewModel? value)
    {
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
