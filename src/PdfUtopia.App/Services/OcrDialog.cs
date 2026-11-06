using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.Services;

/// <summary>
/// Fortschritt der Texterkennung.
/// </summary>
/// <param name="Done">Bereits erkannte Seiten.</param>
/// <param name="Total">Alle zu erkennenden Seiten.</param>
/// <param name="PageNumber">Seite, die gerade erkannt wird (1-basiert in der Seitenliste).</param>
public sealed record OcrProgress(int Done, int Total, int PageNumber);

/// <summary>
/// Angaben für den Dialog „Texterkennung“.
/// </summary>
/// <param name="EngineText">Hinweis auf die gefundene Engine, z. B. „Tesseract 5.3.4 gefunden · Sprachen: Deutsch, Englisch“.</param>
/// <param name="PageCount">Seiten der Seitenliste.</param>
/// <param name="PagesWithoutText">Seiten ohne Text (1-basiert).</param>
/// <param name="Languages">Wählbare Sprachen: Tesseract-Kürzel (z. B. „deu+eng“) und Anzeigename; der erste ist vorgewählt.</param>
/// <param name="Run">Erkennt die Seiten (1-basiert) in der Sprache; meldet den Fortschritt und liefert die Zahl der Wörter.</param>
public sealed record OcrRequest(
    string EngineText, int PageCount, IReadOnlyList<int> PagesWithoutText, IReadOnlyList<(string Code, string Name)> Languages,
    Func<IReadOnlyList<int>, string, IProgress<OcrProgress>, CancellationToken, Task<int>> Run);

/// <summary>
/// Ergebnis der Texterkennung.
/// </summary>
/// <param name="Pages">Erkannte Seiten.</param>
/// <param name="Words">Erkannte Wörter.</param>
/// <param name="Cancelled">Ob abgebrochen wurde.</param>
/// <param name="Error">Fehlermeldung, wenn die Erkennung fehlschlug; sonst <c>null</c>.</param>
public sealed record OcrOutcome(int Pages, int Words, bool Cancelled, string? Error = null);

/// <summary>
/// Dialog „Texterkennung“ (A53, Mockup v9): Seiten (ohne Text, alle, Auswahl) und Sprache wählen, dann Fortschritt je
/// Seite mit Abbrechen.
/// </summary>
internal sealed class OcrDialog
{
    private readonly DialogService _dialogs;
    private readonly OcrRequest _request;
    private readonly RadioButton _withoutText;
    private readonly RadioButton _all;
    private readonly RadioButton _selection = new() { GroupName = "ocrPages", Content = Loc.T("Ocr.Selection"), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _selectionInput = new() { Width = 140, PlaceholderText = "2,4-8" };
    private readonly ComboBox _language = new() { MinWidth = 220 };
    private readonly TextBlock _hint = new() { Classes = { "hint" }, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
    private readonly StackPanel _choice;
    private readonly StackPanel _progress = new() { Spacing = 8, IsVisible = false };
    private readonly TextBlock _progressText = new();
    private readonly ProgressBar _progressBar = new() { Minimum = 0, Maximum = 1, Height = 8, MinWidth = 440 };
    private readonly Button _start = new() { Content = Loc.T("Ocr.Run"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button _cancel = new() { Content = Loc.T("Dialog.Cancel"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Window _window;
    private CancellationTokenSource? _running;
    private OcrOutcome? _outcome;

    /// <summary>
    /// Baut den Dialog auf.
    /// </summary>
    /// <param name="dialogs">Dialogdienst.</param>
    /// <param name="request">Angaben und Erkennung.</param>
    public OcrDialog(DialogService dialogs, OcrRequest request)
    {
        _dialogs = dialogs;
        _request = request;
        var without = request.PagesWithoutText;
        _withoutText = new RadioButton
        {
            GroupName = "ocrPages", IsEnabled = without.Count > 0, IsChecked = without.Count > 0,
            Content = Loc.F("Ocr.WithoutText", without.Count, without.Count == 0 ? "–" : InsertPlacement.FormatSelection(without)),
        };
        _all = new RadioButton { GroupName = "ocrPages", IsChecked = without.Count == 0, Content = Loc.F("Ocr.All", request.PageCount) };
        _language.ItemsSource = request.Languages.Select(language => language.Name).ToList();
        _language.SelectedIndex = 0;
        _selectionInput.GotFocus += (_, _) => _selection.IsChecked = true;
        _selectionInput.TextChanged += (_, _) => Update();
        foreach (var radio in new[] { _withoutText, _all, _selection })
        {
            radio.IsCheckedChanged += (_, _) => Update();
        }

        _choice = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#DCFCE7")), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 7),
                    Child = new TextBlock { Text = "✓ " + request.EngineText, Foreground = new SolidColorBrush(Color.Parse("#166534")), FontSize = 12.5, TextWrapping = TextWrapping.Wrap },
                },
                new TextBlock { Text = Loc.T("Ocr.Pages"), Margin = new Thickness(0, 6, 0, 0) },
                _withoutText, _all,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _selection, _selectionInput } },
                new TextBlock { Text = Loc.T("Ocr.Language"), Margin = new Thickness(0, 6, 0, 0) },
                _language,
                _hint,
            },
        };
        _progress.Children.Add(_progressText);
        _progress.Children.Add(_progressBar);

        _window = DialogService.CreateDialog(Loc.T("Ocr.Title"), new StackPanel { Children = { _choice, _progress } });
        _cancel.Click += (_, _) =>
        {
            if (_running is { } running)
            {
                running.Cancel();
            }
            else
            {
                _window.Close();
            }
        };
        _start.Click += async (_, _) => await StartAsync();
        DialogService.MarkPrimary(_start);
        var row = DialogService.ButtonRow();
        row.Children.Add(_cancel);
        row.Children.Add(_start);
        ((StackPanel)_window.Content!).Children.Add(row);
        _window.Closing += (_, e) =>
        {
            // Während der Erkennung schließt nur „Abbrechen“ (danach schließt der Dialog selbst)
            if (_running is not null)
            {
                e.Cancel = true;
                _running.Cancel();
            }
        };
        Update();
    }

    /// <summary>
    /// Zeigt den Dialog.
    /// </summary>
    /// <returns>Das Ergebnis oder <c>null</c>, wenn nicht gestartet wurde.</returns>
    public async Task<OcrOutcome?> ShowAsync()
    {
        await _window.ShowDialog(_dialogs.Owner);
        return _outcome;
    }

    /// <summary>Gewählte Seiten (1-basiert) oder <c>null</c> bei ungültiger Auswahl.</summary>
    private IReadOnlyList<int>? SelectedPages()
    {
        if (_withoutText.IsChecked == true)
        {
            return _request.PagesWithoutText;
        }

        if (_all.IsChecked == true)
        {
            return Enumerable.Range(1, _request.PageCount).ToList();
        }

        var parsed = PageSelectionParser.Parse(_selectionInput.Text ?? string.Empty, _request.PageCount);
        return parsed.HasPages ? parsed.Pages : null;
    }

    /// <summary>Hinweis und Start-Schaltfläche nach der Auswahl.</summary>
    private void Update()
    {
        var pages = SelectedPages();
        _start.IsEnabled = pages is { Count: > 0 };
        var withText = pages?.Count(page => !_request.PagesWithoutText.Contains(page)) ?? 0;
        _hint.Text = withText > 0 ? Loc.F("Ocr.HintDuplicate", withText) : Loc.T("Ocr.Hint");
    }

    /// <summary>Startet die Erkennung und zeigt den Fortschritt.</summary>
    private async Task StartAsync()
    {
        if (SelectedPages() is not { Count: > 0 } pages)
        {
            return;
        }

        var language = _request.Languages[Math.Max(0, _language.SelectedIndex)].Code;
        _choice.IsVisible = false;
        _progress.IsVisible = true;
        _start.IsVisible = false;
        _running = new CancellationTokenSource();
        var done = 0;
        var cancelled = false;
        string? error = null;
        var words = 0;
        var progress = new Progress<OcrProgress>(step =>
        {
            done = step.Done;
            _progressText.Text = Loc.F("Ocr.Running", step.PageNumber, step.Done + 1, step.Total);
            _progressBar.Value = (double)step.Done / step.Total;
        });
        try
        {
            words = await _request.Run(pages, language, progress, _running.Token);
            done = pages.Count;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            error = exception.Message;
        }
        finally
        {
            _running.Dispose();
            _running = null;
        }

        _outcome = new OcrOutcome(done, words, cancelled, error);
        _window.Close();
    }
}
