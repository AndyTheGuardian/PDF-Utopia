using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.Services;

/// <summary>
/// Dialog „Seiten aus anderer PDF-Datei einfügen“ (A41, Mockup v3): Quelle wählen (Datei oder geöffnetes Dokument),
/// bei geschützter Datei das Passwort eingeben, Seiten per Eingabe oder Klick auf die Miniaturen wählen und die
/// Position bestimmen. Der Dialog öffnet die Quelle selbst; nur die bestätigte Quelle geht an den Aufrufer über,
/// alle anderen werden wieder freigegeben.
/// </summary>
internal sealed class InsertPagesDialog
{
    private const double SheetWidth = 44;
    private const double SheetHeight = 62;

    private readonly DialogService _dialogs;
    private readonly InsertRequest _request;
    private readonly List<InsertSourceOption> _options;
    private readonly List<Bitmap> _bitmaps = [];
    private readonly Dictionary<int, Border> _sheets = [];

    private readonly ComboBox _sourceBox = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _sourceInfo = new() { Classes = { "hint" }, TextWrapping = TextWrapping.Wrap };
    private readonly Border _unsavedWarning;
    private readonly TextBox _password = new() { PasswordChar = '•', PlaceholderText = Loc.T("Password.Placeholder") };
    private readonly TextBlock _passwordError = new() { Text = Loc.T("Password.Wrong"), Foreground = DialogService.Resource("DangerBrush"), IsVisible = false };
    private readonly StackPanel _passwordPanel;
    private readonly TextBox _selectionBox = new();
    private readonly WrapPanel _grid = new();
    private readonly StackPanel _hints = new() { Spacing = 4 };
    private readonly ComboBox _positionBox;
    private readonly NumericUpDown _afterPage;
    private readonly TextBlock _positionHint = new() { Classes = { "hint" } };
    private readonly StackPanel _pagesPanel;
    private readonly Button _confirm = new() { MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Window _window;

    private PdfSource? _source;
    private PageSelection _selection = new([], []);
    private bool _locked;
    private bool _keepSource;
    private bool _updatingSources;
    private int _selectedOption;
    private int _version;
    private Task _thumbnails = Task.CompletedTask;

    /// <summary>
    /// Baut den Dialog auf.
    /// </summary>
    /// <param name="dialogs">Dialogdienst (Dateiauswahl, Besitzerfenster).</param>
    /// <param name="request">Quellen und Vorgaben.</param>
    public InsertPagesDialog(DialogService dialogs, InsertRequest request)
    {
        _dialogs = dialogs;
        _request = request;
        _options = request.Sources.ToList();

        _unsavedWarning = DialogService.Warning(Loc.T("Insert.Unsaved"));
        _passwordPanel = new StackPanel { Spacing = 6, Children = { DialogService.Message(Loc.T("Insert.Password")), _password, _passwordError } };
        _positionBox = new ComboBox
        {
            ItemsSource = new[] { "Insert.AfterCurrent", "Insert.Start", "Insert.End", "Insert.AfterPage" }.Select(Loc.T).ToList(),
            SelectedIndex = (int)request.Position,
            MinWidth = 240,
        };
        _afterPage = new NumericUpDown
        {
            Minimum = 0, Maximum = request.PageCount, Increment = 1, FormatString = "0", MinWidth = 130,
            Value = request.Position == InsertPosition.AfterPage ? request.AfterPage : request.CurrentIndex + 1,
        };

        _pagesPanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new DockPanel
                {
                    Children =
                    {
                        SelectionButtons(),
                        new TextBlock { Text = Loc.T("Insert.Pages"), VerticalAlignment = VerticalAlignment.Center },
                    },
                },
                _selectionBox,
                new TextBlock { Text = Loc.T("Insert.Help"), Classes = { "hint" }, TextWrapping = TextWrapping.Wrap },
                new Border
                {
                    Padding = new Thickness(6), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1),
                    BorderBrush = DialogService.Resource("AppBorderBrush"), Background = DialogService.Resource("Surface2Brush"),
                    Child = new ScrollViewer { MaxHeight = 230, Content = _grid },
                },
                _hints,
                new TextBlock { Text = Loc.T("Insert.Position"), Margin = new Thickness(0, 8, 0, 0) },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _positionBox, _afterPage } },
                _positionHint,
            },
        };

        var content = new StackPanel
        {
            Spacing = 8,
            Width = 520,
            Children = { new TextBlock { Text = Loc.T("Insert.Source") }, _sourceBox, _sourceInfo, _unsavedWarning, _passwordPanel, _pagesPanel },
        };
        _window = DialogService.CreateDialog(Loc.T("Insert.Title"), content);
        AddButtons();

        _sourceBox.SelectionChanged += async (_, _) => await OnSourceChangedAsync();
        _selectionBox.TextChanged += (_, _) => Update();
        _positionBox.SelectionChanged += (_, _) => Update();
        _afterPage.ValueChanged += (_, _) => Update();
        _window.Closed += async (_, _) => await ReleaseAsync();
    }

    /// <summary>
    /// Zeigt den Dialog.
    /// </summary>
    /// <returns>Die Auswahl oder <c>null</c> bei Abbruch.</returns>
    public async Task<InsertChoice?> ShowAsync()
    {
        FillSources(0);
        _window.Opened += async (_, _) => await LoadAsync(_options[0], null);
        return await _window.ShowDialog<bool>(_dialogs.Owner) && _source is { } source
            ? new InsertChoice(source, _selection.Pages, InsertIndex())
            : null;
    }

    /// <summary>
    /// Abbrechen und Einfügen (bzw. Entsperren bei geschützter Quelle).
    /// </summary>
    private void AddButtons()
    {
        var cancel = new Button { Content = Loc.T("Dialog.Cancel"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => _window.Close(false);
        _confirm.Click += async (_, _) => await ConfirmAsync();
        DialogService.MarkPrimary(_confirm);
        var row = DialogService.ButtonRow();
        row.Children.Add(cancel);
        row.Children.Add(_confirm);
        ((StackPanel)_window.Content!).Children.Add(row);
    }

    /// <summary>
    /// Füllt die Quellenauswahl; der letzte Eintrag öffnet die Dateiauswahl.
    /// </summary>
    private void FillSources(int selectedIndex)
    {
        _updatingSources = true;
        _sourceBox.ItemsSource = _options
            .Select(option => option.IsOpenDocument ? Loc.F("Insert.OpenDocument", option.DisplayName) : option.DisplayName)
            .Append(Loc.T("Insert.ChooseFile"))
            .ToList();
        _sourceBox.SelectedIndex = selectedIndex;
        _selectedOption = selectedIndex;
        _updatingSources = false;
    }

    /// <summary>
    /// Neue Quelle gewählt; „Datei wählen…“ zeigt die Dateiauswahl.
    /// </summary>
    private async Task OnSourceChangedAsync()
    {
        if (_updatingSources || _sourceBox.SelectedIndex < 0 || _sourceBox.SelectedIndex == _selectedOption)
        {
            return;
        }

        if (_sourceBox.SelectedIndex == _options.Count)
        {
            var path = await _dialogs.PickPdfFileAsync();
            if (path is null)
            {
                FillSources(_selectedOption);
                return;
            }

            var existing = _options.FindIndex(option => option.Path == path);
            if (existing < 0)
            {
                _options.Add(new InsertSourceOption(path, Path.GetFileName(path), null, false, false));
                existing = _options.Count - 1;
            }

            FillSources(existing);
            await LoadAsync(_options[existing], null);
            return;
        }

        _selectedOption = _sourceBox.SelectedIndex;
        await LoadAsync(_options[_selectedOption], null);
    }

    /// <summary>
    /// Öffnet eine Quelle; bei geschützten Dateien wird nach dem Passwort gefragt.
    /// </summary>
    private async Task LoadAsync(InsertSourceOption option, string? password)
    {
        var version = ++_version;
        await ReleaseSourceAsync();
        _passwordError.IsVisible = false;
        _unsavedWarning.IsVisible = option.HasUnsavedChanges;
        try
        {
            var source = await Task.Run(() => PdfSource.Open(option.Path, password ?? option.KnownPassword));
            if (version != _version)
            {
                source.Dispose();
                return;
            }

            _source = source;
            ShowLocked(false);
            _sourceInfo.Text = Loc.F("Insert.PageCount", source.PageCount);
            _selectionBox.Text = InsertPlacement.FormatSelection(InsertPlacement.AllPages(source.PageCount));
            BuildGrid(source);
            Update();
        }
        catch (PdfOpenException exception) when (exception.Error == PdfOpenError.PasswordRequired)
        {
            if (version != _version)
            {
                return;
            }

            ShowLocked(true);
            _sourceInfo.Text = string.Empty;
            _passwordError.IsVisible = password is not null;
            _password.Text = string.Empty;
            _password.Focus();
        }
        catch (PdfOpenException exception)
        {
            if (version != _version)
            {
                return;
            }

            ShowLocked(false);
            _pagesPanel.IsVisible = false;
            _sourceInfo.Text = Loc.F($"Error.{exception.Error}", option.DisplayName);
            _confirm.Content = Loc.T("Insert.None");
            _confirm.IsEnabled = false;
        }
    }

    /// <summary>
    /// Zeigt das Passwortfeld statt der Seitenauswahl (und umgekehrt).
    /// </summary>
    private void ShowLocked(bool locked)
    {
        _locked = locked;
        _passwordPanel.IsVisible = locked;
        _pagesPanel.IsVisible = !locked;
        if (locked)
        {
            _confirm.Content = Loc.T("Password.Unlock");
            _confirm.IsEnabled = true;
        }
    }

    /// <summary>
    /// Entsperrt die geschützte Quelle oder bestätigt die Auswahl.
    /// </summary>
    private async Task ConfirmAsync()
    {
        if (_locked)
        {
            if (!string.IsNullOrEmpty(_password.Text))
            {
                await LoadAsync(_options[_selectedOption], _password.Text);
            }

            return;
        }

        if (_source is not null && _selection.HasPages)
        {
            _keepSource = true;
            _window.Close(true);
        }
    }

    /// <summary>
    /// Wertet Seitenauswahl und Position aus und aktualisiert Miniaturen, Hinweise und Schaltfläche.
    /// </summary>
    private void Update()
    {
        if (_source is null || _locked)
        {
            return;
        }

        _selection = PageSelectionParser.Parse(_selectionBox.Text, _source.PageCount);
        var chosen = _selection.Pages.ToHashSet();
        foreach (var (page, sheet) in _sheets)
        {
            MarkSheet(sheet, chosen.Contains(page));
        }

        _hints.Children.Clear();
        foreach (var hint in _selection.Hints)
        {
            _hints.Children.Add(DialogService.Warning(DialogService.HintText(hint, "Insert.HintDuplicate")));
        }

        _afterPage.IsVisible = _positionBox.SelectedIndex == (int)InsertPosition.AfterPage;
        var count = _selection.Pages.Count;
        var first = InsertIndex() + 1;
        var total = _request.PageCount + count;
        _positionHint.Text = count == 0 ? string.Empty
            : count == 1 ? Loc.F("Insert.PositionHintOne", first, total)
            : Loc.F("Insert.PositionHint", first, first + count - 1, total);
        _confirm.Content = count == 0 ? Loc.T("Insert.None") : count == 1 ? Loc.T("Insert.ConfirmOne") : Loc.F("Insert.Confirm", count);
        _confirm.IsEnabled = count > 0;
    }

    /// <summary>Position der ersten neuen Seite im Zieldokument.</summary>
    private int InsertIndex() =>
        InsertPlacement.IndexFor((InsertPosition)Math.Max(0, _positionBox.SelectedIndex), _request.CurrentIndex, (int)(_afterPage.Value ?? 0), _request.PageCount);

    /// <summary>
    /// Legt für jede Seite der Quelle eine anklickbare Miniatur an und rendert sie im Hintergrund.
    /// </summary>
    private void BuildGrid(PdfSource source)
    {
        _grid.Children.Clear();
        _sheets.Clear();
        var images = new List<(int Page, Image Image)>();
        for (var page = 1; page <= source.PageCount; page++)
        {
            var image = new Image { Width = SheetWidth, Height = SheetHeight, Stretch = Stretch.Uniform };
            var sheet = new Border
            {
                Width = 64, Margin = new Thickness(2), Padding = new Thickness(4), CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(2), Cursor = new Cursor(StandardCursorType.Hand), Tag = page,
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new Border { Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 1 3 0 #4D000000"), Child = image, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = page.ToString(System.Globalization.CultureInfo.CurrentCulture), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center },
                    },
                },
            };
            sheet.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(sheet).Properties.IsLeftButtonPressed && sheet.Tag is int number)
                {
                    Toggle(number);
                }
            };
            _sheets[page] = sheet;
            _grid.Children.Add(sheet);
            images.Add((page, image));
        }

        _thumbnails = RenderThumbnailsAsync(source, images, _version);
    }

    /// <summary>
    /// Nimmt eine Seite in die Auswahl auf oder heraus.
    /// </summary>
    private void Toggle(int page)
    {
        var pages = _selection.Pages.ToHashSet();
        if (!pages.Remove(page))
        {
            pages.Add(page);
        }

        _selectionBox.Text = InsertPlacement.FormatSelection(pages);
    }

    /// <summary>
    /// Schaltflächen rechts über der Seitenauswahl: alle wählen, Auswahl aufheben, Auswahl umkehren.
    /// </summary>
    private StackPanel SelectionButtons()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(panel, Dock.Right);
        foreach (var (key, select) in new (string, Func<PdfSource, IEnumerable<int>>)[]
                 {
                     ("Insert.SelectAll", source => InsertPlacement.AllPages(source.PageCount)),
                     ("Insert.SelectNone", _ => []),
                     ("Insert.Invert", source => InsertPlacement.Invert(_selection.Pages, source.PageCount)),
                 })
        {
            var button = new Button { Content = Loc.T(key), Padding = new Thickness(10, 3), FontSize = 12 };
            button.Click += (_, _) =>
            {
                if (_source is { } source)
                {
                    _selectionBox.Text = InsertPlacement.FormatSelection(select(source));
                }
            };
            panel.Children.Add(button);
        }

        return panel;
    }

    /// <summary>
    /// Hebt eine gewählte Miniatur in der Akzentfarbe hervor.
    /// </summary>
    private static void MarkSheet(Border sheet, bool selected)
    {
        sheet.BorderBrush = selected ? DialogService.Resource("AccentBorderBrush") : Brushes.Transparent;
        sheet.Background = selected ? DialogService.Resource("AccentSoftBrush") : Brushes.Transparent;
        sheet.Opacity = selected ? 1 : 0.55;
    }

    /// <summary>
    /// Rendert die Miniaturen nacheinander; bricht ab, sobald eine andere Quelle gewählt wird.
    /// </summary>
    private async Task RenderThumbnailsAsync(PdfSource source, List<(int Page, Image Image)> images, int version)
    {
        foreach (var (page, image) in images)
        {
            if (version != _version)
            {
                return;
            }

            var (width, height) = source.PageSize(page);
            var scale = 2 * Math.Min(SheetWidth / width, SheetHeight / height);
            var rendered = await Task.Run(() => source.Render(page, scale));
            var bitmap = PageRenderer.ToBitmap(rendered);
            _bitmaps.Add(bitmap);
            image.Source = bitmap;
        }
    }

    /// <summary>
    /// Gibt die bisher gewählte Quelle frei, sobald ihre Miniaturen nicht mehr gerendert werden.
    /// </summary>
    private async Task ReleaseSourceAsync()
    {
        await _thumbnails;
        _source?.Dispose();
        _source = null;
        _grid.Children.Clear();
        _sheets.Clear();
    }

    /// <summary>
    /// Gibt nach dem Schließen Miniaturen und – ohne Bestätigung – die Quelle frei.
    /// </summary>
    private async Task ReleaseAsync()
    {
        _version++;
        await _thumbnails;
        foreach (var bitmap in _bitmaps)
        {
            bitmap.Dispose();
        }

        _bitmaps.Clear();
        if (!_keepSource)
        {
            _source?.Dispose();
        }
    }
}
