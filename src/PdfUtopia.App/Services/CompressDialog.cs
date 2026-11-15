using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Compression;

namespace PdfUtopia.App.Services;

/// <summary>
/// Angaben für den Dialog „Komprimieren“.
/// </summary>
/// <param name="FileName">Name der geöffneten Datei.</param>
/// <param name="OriginalBytes">Größe der geöffneten Datei in Bytes.</param>
/// <param name="Compress">Komprimiert das Dokument mit einer Stufe; meldet den Fortschritt von 0 bis 1.</param>
public sealed record CompressRequest(
    string FileName, long OriginalBytes, Func<CompressionPreset, IProgress<double>, CancellationToken, Task<byte[]>> Compress);

/// <summary>
/// Ergebnis des Dialogs „Komprimieren“.
/// </summary>
/// <param name="Preset">Gewählte Stufe; <c>null</c> bei einem Fehler.</param>
/// <param name="Data">Komprimierte, ungeschützte Datei; <c>null</c> bei einem Fehler.</param>
/// <param name="Error">Fehlermeldung, wenn das Berechnen fehlschlug; sonst <c>null</c>.</param>
public sealed record CompressOutcome(CompressionPreset? Preset, byte[]? Data, string? Error = null);

/// <summary>
/// Dialog „Komprimieren“ (A57, Mockup v11): zeigt die Originalgröße, berechnet beide Stufen vorab im Hintergrund und
/// zeigt je Stufe die neue Größe mit Ersparnis. „Speichern unter…“ ist erst nach dem Berechnen und nur bei einer
/// Ersparnis möglich.
/// </summary>
internal sealed class CompressDialog
{
    private static readonly CompressionPreset[] Presets = [CompressionPreset.Quality, CompressionPreset.Economy];

    private readonly DialogService _dialogs;
    private readonly CompressRequest _request;
    private readonly Window _window;
    private readonly Option[] _options;
    private readonly byte[]?[] _results = new byte[]?[Presets.Length];
    private readonly StackPanel _busy = new() { Spacing = 6 };
    private readonly ProgressBar _progressBar = new() { Minimum = 0, Maximum = 1, Height = 8, MinWidth = 440 };
    private readonly Button _save = new() { Content = Loc.T("Compress.Save"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button _cancel = new() { Content = Loc.T("Dialog.Cancel"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly CancellationTokenSource _running = new();
    private int _selected;
    private bool _calculated;
    private CompressOutcome? _outcome;

    /// <summary>
    /// Baut den Dialog auf.
    /// </summary>
    /// <param name="dialogs">Dialogdienst.</param>
    /// <param name="request">Angaben und Komprimierung.</param>
    public CompressDialog(DialogService dialogs, CompressRequest request)
    {
        _dialogs = dialogs;
        _request = request;
        _options =
        [
            new Option(Loc.T("Compress.Quality"), Loc.T("Compress.QualityDesc")),
            new Option(Loc.T("Compress.Economy"), Loc.T("Compress.EconomyDesc")),
        ];
        for (var index = 0; index < _options.Length; index++)
        {
            var chosen = index;
            _options[index].Card.PointerPressed += (_, _) => Select(chosen);
            _options[index].Radio.IsCheckedChanged += (_, _) =>
            {
                if (_options[chosen].Radio.IsChecked == true)
                {
                    Select(chosen);
                }
            };
        }

        _busy.Children.Add(new TextBlock { Text = Loc.T("Compress.Busy"), FontSize = 12, Foreground = DialogService.Resource("MutedTextBrush") });
        _busy.Children.Add(_progressBar);
        var content = new StackPanel
        {
            Spacing = 8,
            Width = 460,
            Children =
            {
                FileRow(),
                _options[0].Card,
                _options[1].Card,
                _busy,
                new TextBlock
                {
                    Text = Loc.T("Compress.Note"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
                    Foreground = DialogService.Resource("MutedTextBrush"),
                },
            },
        };

        _window = DialogService.CreateDialog(Loc.T("Compress.Title"), content);
        _cancel.Click += (_, _) => _window.Close();
        _save.Click += (_, _) =>
        {
            if (_results[_selected] is { } data)
            {
                _outcome = new CompressOutcome(Presets[_selected], data);
                _window.Close();
            }
        };
        DialogService.MarkPrimary(_save);
        var row = DialogService.ButtonRow();
        row.Children.Add(_cancel);
        row.Children.Add(_save);
        ((StackPanel)_window.Content!).Children.Add(row);
        _window.Closing += (_, _) => _running.Cancel();
        _window.Opened += async (_, _) => await CalculateAsync();
        Select(0);
    }

    /// <summary>
    /// Zeigt den Dialog.
    /// </summary>
    /// <returns>Die gewählte Stufe mit Ergebnis, ein Fehler oder <c>null</c> bei Abbruch.</returns>
    public async Task<CompressOutcome?> ShowAsync()
    {
        await _window.ShowDialog(_dialogs.Owner);
        return _outcome;
    }

    /// <summary>Zeile mit Dateiname und Originalgröße.</summary>
    private Border FileRow()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new TextBlock { Text = _request.FileName, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 12, 0) });
        var size = new TextBlock { Foreground = DialogService.Resource("MutedTextBrush") };
        size.Inlines!.Add(new Run(Loc.T("Compress.Original") + ": "));
        size.Inlines.Add(new Run(FileSize.Format(_request.OriginalBytes, Loc.Culture)) { FontWeight = FontWeight.SemiBold, Foreground = DialogService.Resource("TextBrush") });
        Grid.SetColumn(size, 1);
        grid.Children.Add(size);
        return new Border
        {
            Background = DialogService.Resource("Surface2Brush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 0, 0, 6), Child = grid,
        };
    }

    /// <summary>Berechnet beide Stufen nacheinander und zeigt danach die Ergebnisse.</summary>
    private async Task CalculateAsync()
    {
        try
        {
            for (var index = 0; index < Presets.Length; index++)
            {
                var step = index;
                var progress = new Progress<double>(value => _progressBar.Value = (step + value) / Presets.Length);
                _results[index] = await _request.Compress(Presets[index], progress, _running.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _outcome = new CompressOutcome(null, null, exception.Message);
            _window.Close();
            return;
        }

        _calculated = true;
        _busy.IsVisible = false;
        Update();
    }

    /// <summary>Wählt eine Stufe.</summary>
    private void Select(int index)
    {
        _selected = index;
        Update();
    }

    /// <summary>Hervorhebung, Ergebnisse und Speichern-Schaltfläche nach Auswahl und Berechnung.</summary>
    private void Update()
    {
        for (var index = 0; index < _options.Length; index++)
        {
            var option = _options[index];
            var selected = index == _selected;
            option.Radio.IsChecked = selected;
            option.Card.BorderBrush = DialogService.Resource(selected ? "AccentBrush" : "AppBorderBrush");
            option.Card.Background = selected ? DialogService.Resource("AccentSoftBrush") : Brushes.Transparent;
            option.ShowResult(_calculated ? _results[index]?.LongLength : null, _request.OriginalBytes);
        }

        _save.IsEnabled = _calculated && _results[_selected] is { } data && CompressionPreset.IsWorthwhile(_request.OriginalBytes, data.LongLength);
    }

    /// <summary>
    /// Karte einer Stufe: Auswahl, Titel, Beschreibung und Ergebnis.
    /// </summary>
    private sealed class Option
    {
        private readonly StackPanel _result = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 0, 0) };

        public Option(string title, string description)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
            Grid.SetRowSpan(Radio, 2);
            grid.Children.Add(Radio);
            var titleText = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold };
            Grid.SetColumn(titleText, 1);
            grid.Children.Add(titleText);
            var descriptionText = new TextBlock
            {
                Text = description, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
                Foreground = DialogService.Resource("MutedTextBrush"),
            };
            Grid.SetColumn(descriptionText, 1);
            Grid.SetRow(descriptionText, 1);
            grid.Children.Add(descriptionText);
            Grid.SetColumn(_result, 2);
            Grid.SetRowSpan(_result, 2);
            grid.Children.Add(_result);
            Card = new Border
            {
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Child = grid,
            };
        }

        /// <summary>Rahmen der Karte (anklickbar).</summary>
        public Border Card { get; }

        /// <summary>Auswahlknopf.</summary>
        public RadioButton Radio { get; } = new() { GroupName = "compressLevel", MinWidth = 0, Padding = new Thickness(0), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };

        /// <summary>
        /// Zeigt „wird berechnet …“, „keine Ersparnis“ oder Größe und Ersparnis.
        /// </summary>
        /// <param name="bytes">Komprimierte Größe oder <c>null</c> während des Berechnens.</param>
        /// <param name="original">Originalgröße.</param>
        public void ShowResult(long? bytes, long original)
        {
            _result.Children.Clear();
            var muted = DialogService.Resource("MutedTextBrush");
            if (bytes is not { } size || !CompressionPreset.IsWorthwhile(original, size))
            {
                _result.Children.Add(new TextBlock { Text = Loc.T(bytes is null ? "Compress.Calculating" : "Compress.None"), FontSize = 12.5, Foreground = muted });
                return;
            }

            _result.Children.Add(new TextBlock { Text = FileSize.Format(size, Loc.Culture), FontWeight = FontWeight.SemiBold, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Right });
            _result.Children.Add(new TextBlock
            {
                Text = $"−{CompressionPreset.SavingPercent(original, size)} %", FontSize = 12, Foreground = muted, HorizontalAlignment = HorizontalAlignment.Right,
            });
        }
    }
}
