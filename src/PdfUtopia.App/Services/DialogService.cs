using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PdfUtopia.App.Localization;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.History;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Signatures;
using PdfUtopia.Pdf.Export;

namespace PdfUtopia.App.Services;

/// <summary>
/// Implementiert die Dialoge als schlichte modale Fenster und nutzt den Speicheranbieter des Hauptfensters
/// für die Dateiauswahl (wie FileRenamer).
/// </summary>
/// <param name="owner">Das Hauptfenster.</param>
public sealed class DialogService(Window owner) : IDialogService
{
    /// <summary>Fenster, über dem die Dialoge erscheinen; nach einem Sprachwechsel das neu aufgebaute Hauptfenster.</summary>
    public Window Owner { get; set; } = owner;

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickPdfFilesAsync()
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Menu.Open").Replace("_", string.Empty, StringComparison.Ordinal),
            AllowMultiple = true,
            FileTypeFilter = [PdfFileType()],
        });
        return files.Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string suggestedName)
    {
        var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.T("Save.Title"),
            SuggestedFileName = suggestedName,
            DefaultExtension = "pdf",
            FileTypeChoices = [PdfFileType()],
        });
        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PromptPasswordAsync(string fileName, bool previousAttemptWrong)
    {
        var input = new TextBox { PasswordChar = '•', PlaceholderText = Loc.T("Password.Placeholder"), MinWidth = 300 };
        var content = new StackPanel { Spacing = 8, Children = { Message(Loc.F("Password.Message", fileName)), input } };
        if (previousAttemptWrong)
        {
            content.Children.Add(new TextBlock { Text = Loc.T("Password.Wrong"), Foreground = Resource("DangerBrush") });
        }

        var dialog = CreateDialog(Loc.T("Password.Title"), content);
        AddButtons(dialog, (Loc.T("Dialog.Cancel"), false), (Loc.T("Password.Unlock"), true));
        dialog.Opened += (_, _) => input.Focus();
        return await dialog.ShowDialog<bool>(Owner) && !string.IsNullOrEmpty(input.Text) ? input.Text : null;
    }

    /// <inheritdoc />
    public async Task<(PasswordMode Mode, string? NewPassword)?> AskPasswordModeAsync(bool sourceIsProtected)
    {
        var keep = new RadioButton { Content = Loc.T(sourceIsProtected ? "Save.KeepProtected" : "Save.KeepNone"), IsChecked = true, GroupName = "pw" };
        var change = new RadioButton { Content = Loc.T("Save.Change"), GroupName = "pw" };
        var remove = new RadioButton { Content = Loc.T("Save.Remove"), GroupName = "pw", IsVisible = sourceIsProtected };
        var password = new TextBox { PasswordChar = '•', PlaceholderText = Loc.T("Save.NewPassword"), IsEnabled = false };
        var repeat = new TextBox { PasswordChar = '•', PlaceholderText = Loc.T("Save.RepeatPassword"), IsEnabled = false };
        var error = new TextBlock { Foreground = Resource("DangerBrush"), IsVisible = false };
        change.IsCheckedChanged += (_, _) => password.IsEnabled = repeat.IsEnabled = change.IsChecked == true;

        var content = new StackPanel
        {
            Spacing = 8,
            MinWidth = 320,
            Children = { new TextBlock { Text = Loc.T("Save.Protection"), Classes = { "title" } }, keep, change, password, repeat, remove, error },
        };
        var dialog = CreateDialog(Loc.T("Save.Title"), content);
        bool Validate()
        {
            var problem = change.IsChecked != true ? null
                : string.IsNullOrEmpty(password.Text) ? "Save.EmptyPassword"
                : password.Text != repeat.Text ? "Save.Mismatch"
                : null;
            error.Text = problem is null ? string.Empty : Loc.T(problem);
            error.IsVisible = problem is not null;
            return problem is null;
        }

        AddButtons(dialog, Validate, (Loc.T("Dialog.Cancel"), false), (Loc.T("Save.Continue"), true));

        if (!await dialog.ShowDialog<bool>(Owner))
        {
            return null;
        }

        return change.IsChecked == true ? (PasswordMode.Change, password.Text)
            : remove.IsChecked == true ? (PasswordMode.Remove, null)
            : (PasswordMode.KeepSource, null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>?> AskPagesToExtractAsync(int pageCount, string? preset = null)
    {
        var input = new TextBox { Text = preset ?? (pageCount > 1 ? $"1-{pageCount}" : "1"), MinWidth = 380 };
        var chips = new WrapPanel();
        var hints = new StackPanel { Spacing = 4 };
        var selection = PageSelectionParser.Parse(input.Text, pageCount);
        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = Loc.T("Extract.Label") }, input,
                new TextBlock { Text = Loc.T("Extract.Help"), Classes = { "hint" } },
                new TextBlock { Text = Loc.T("Extract.Preview"), Margin = new Thickness(0, 8, 0, 0) },
                new Border
                {
                    Padding = new Thickness(8), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1),
                    BorderBrush = Resource("AppBorderBrush"), Background = Resource("Surface2Brush"), Child = chips,
                },
                hints,
            },
        };
        var dialog = CreateDialog(Loc.T("Extract.Title"), content);
        var confirm = AddButtons(dialog, (Loc.T("Dialog.Cancel"), false), (Loc.T("Extract.Save"), true));

        void Update()
        {
            selection = PageSelectionParser.Parse(input.Text, pageCount);
            chips.Children.Clear();
            foreach (var page in selection.Pages)
            {
                chips.Children.Add(Chip(page.ToString(System.Globalization.CultureInfo.CurrentCulture)));
            }

            chips.Children.Add(new TextBlock
            {
                Text = selection.HasPages ? Loc.F("Extract.Count", selection.Pages.Count) : Loc.T("Extract.Empty"),
                Classes = { "hint" }, Margin = new Thickness(6, 3), VerticalAlignment = VerticalAlignment.Center,
            });
            hints.Children.Clear();
            foreach (var hint in selection.Hints)
            {
                hints.Children.Add(Warning(HintText(hint)));
            }

            confirm.IsEnabled = selection.HasPages;
        }

        input.TextChanged += (_, _) => Update();
        Update();
        dialog.Opened += (_, _) => input.Focus();
        return await dialog.ShowDialog<bool>(Owner) && selection.HasPages ? selection.Pages : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickPdfFileAsync()
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Insert.Title"),
            AllowMultiple = false,
            FileTypeFilter = [PdfFileType()],
        });
        return files.Select(file => file.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<InsertChoice?> AskPagesToInsertAsync(InsertRequest request) =>
        await new InsertPagesDialog(this, request).ShowAsync();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Signature>> EditSignaturesAsync(IReadOnlyList<Signature> signatures, int startSlot, Action<IReadOnlyList<Signature>> persist) =>
        await new SignatureDialog(this, signatures, startSlot, persist).ShowAsync();

    /// <inheritdoc />
    public async Task<OcrOutcome?> RecognizeTextAsync(OcrRequest request) => await new OcrDialog(this, request).ShowAsync();

    /// <inheritdoc />
    public async Task<CompressOutcome?> CompressAsync(CompressRequest request) => await new CompressDialog(this, request).ShowAsync();

    /// <inheritdoc />
    public async Task<bool> ShowTesseractMissingAsync()
    {
        static TextBox Command(string text) => new()
        {
            Text = text, IsReadOnly = true, FontFamily = new FontFamily("monospace"), FontSize = 12, Margin = new Thickness(0, 2, 0, 8),
        };
        var content = new StackPanel
        {
            Spacing = 4, MaxWidth = 480,
            Children =
            {
                Message(Loc.T("Ocr.MissingText")),
                new TextBlock { Text = Loc.T("Ocr.Linux"), Margin = new Thickness(0, 8, 0, 0) }, Command("sudo apt install tesseract-ocr tesseract-ocr-deu"),
                new TextBlock { Text = Loc.T("Ocr.Windows"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) },
                new TextBlock { Text = Loc.T("Ocr.Mac") }, Command("brew install tesseract tesseract-lang"),
                new TextBlock { Text = Loc.T("Ocr.After"), Classes = { "hint" }, TextWrapping = TextWrapping.Wrap },
            },
        };
        var dialog = CreateDialog(Loc.T("Ocr.MissingTitle"), content);
        AddButtons(dialog, (Loc.T("Ocr.Close"), false), (Loc.T("Ocr.Retry"), true));
        return await dialog.ShowDialog<bool>(Owner);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmRedactionAsync(IReadOnlyList<int> pageNumbers, int areaCount, string confirmText)
    {
        const int ShownPages = 12;
        var list = string.Join(", ", pageNumbers.Take(ShownPages)) + (pageNumbers.Count > ShownPages ? ", …" : string.Empty);
        var icon = new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = new SolidColorBrush(Color.Parse("#FEE2E2")),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = "!", FontWeight = FontWeight.Bold, FontSize = 18, Foreground = new SolidColorBrush(Color.Parse("#B91C1C")),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var text = new StackPanel
        {
            Spacing = 10, MaxWidth = 440,
            Children =
            {
                Message(Loc.F("Redact.Message", pageNumbers.Count, areaCount)),
                new Border
                {
                    Background = Resource("Surface2Brush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8),
                    Child = new TextBlock { Text = Loc.F("Redact.Pages", list), TextWrapping = TextWrapping.Wrap, FontSize = 12.5 },
                },
                new TextBlock { Text = Loc.T("Redact.Note"), Classes = { "hint" }, TextWrapping = TextWrapping.Wrap, FontSize = 12 },
            },
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { icon, text } };
        var dialog = CreateDialog(Loc.T("Redact.Title"), content);
        AddButtons(dialog, (Loc.T("Dialog.Cancel"), false), (confirmText, true));
        return await dialog.ShowDialog<bool>(Owner);
    }

    /// <inheritdoc />
    public async Task<string?> PickImageFileAsync()
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Stamp.ImageTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("Stamp.ImageFiles"))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"], MimeTypes = ["image/png", "image/jpeg"], AppleUniformTypeIdentifiers = ["public.png", "public.jpeg"],
                },
            ],
        });
        return files.Select(file => file.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<CustomStamp?> AskCustomStampAsync(string detail) => await new StampDialog(this, detail).ShowAsync();

    /// <inheritdoc />
    public async Task<LoadedImage?> ReadClipboardImageAsync()
    {
        if (Owner.Clipboard is not { } clipboard)
        {
            return null;
        }

        using var bitmap = await Avalonia.Input.Platform.ClipboardExtensions.TryGetBitmapAsync(clipboard);
        return bitmap is null ? null : ImageLoader.FromBitmap(bitmap);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Wechselt der Anwender die Sprache, schließt sich der Dialog ohne Ergebnis; nach dem Neuaufbau des Hauptfensters
    /// öffnet er sich mit demselben ViewModel in der neuen Sprache über dem neuen Fenster (P22).
    /// </remarks>
    public async Task<bool> ShowSettingsAsync(SettingsViewModel viewModel)
    {
        Views.SettingsWindow? previous = null;
        while (true)
        {
            var window = new Views.SettingsWindow { DataContext = viewModel };
            if (previous is not null)
            {
                // Wieder geöffnet nach dem Sprachwechsel: Lage und Größe wie zuvor (P22). Nur aktivieren: Das kurze
                // „immer im Vordergrund“ ließ den Dialog unter Cinnamon kurz nach links oben springen; er gehört zum
                // Hauptfenster, das bereits vorn steht.
                window.SizeToContent = SizeToContent.Manual;
                window.Width = previous.ClosedSize.Width;
                window.Height = previous.ClosedSize.Height;
                // Ohne bekannte Lage (z. B. ohne Fenstermanager) wie beim ersten Öffnen über dem Hauptfenster zentrieren
                if (previous.ClosedPosition != default)
                {
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Position = previous.ClosedPosition;
                }

                window.Opened += (_, _) => window.Activate();
            }

            var result = await window.ShowDialog<bool?>(Parent);
            if (!window.ReopenRequested)
            {
                // „Übernehmen“, „Abbrechen“ oder ✕ (ohne Ergebnis = Abbrechen)
                return result == true;
            }

            previous = window;

            // Erst den (eingereihten) Neuaufbau des Hauptfensters abwarten, dann über dem neuen Fenster öffnen
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = CreateDialog(title, Message(message));
        AddButtons(dialog, (Loc.T("Dialog.Cancel"), false), (confirmText, true));
        return await dialog.ShowDialog<bool>(Parent);
    }

    /// <inheritdoc />
    public async Task<int?> ChooseAsync(string title, string message, IReadOnlyList<string> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var dialog = CreateDialog(title, Message(message));
        int? result = null;
        var panel = ButtonRow();
        var cancel = new Button { Content = Loc.T("Dialog.Cancel"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
        cancel.Click += (_, _) => dialog.Close();
        panel.Children.Add(cancel);
        // Die erste Antwort steht rechts und ist hervorgehoben
        for (var index = choices.Count - 1; index >= 0; index--)
        {
            var choice = index;
            var button = new Button { Content = choices[index], MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) =>
            {
                result = choice;
                dialog.Close();
            };
            panel.Children.Add(button);
            if (index == 0)
            {
                MarkPrimary(button);
            }
        }

        ((StackPanel)dialog.Content!).Children.Add(panel);
        await dialog.ShowDialog(Parent);
        return result;
    }

    /// <inheritdoc />
    public async Task<string?> PromptTextAsync(string title, string label, string initial, string confirmText)
    {
        var input = new TextBox { Text = initial, MinWidth = 300, MaxLength = Core.Theming.ColorProfile.MaxNameLength };
        var dialog = CreateDialog(title, Labeled(label, input));
        AddButtons(dialog, (Loc.T("Dialog.Cancel"), false), (confirmText, true));
        dialog.Opened += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };
        return await dialog.ShowDialog<bool>(Parent) && !string.IsNullOrWhiteSpace(input.Text) ? input.Text.Trim() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickOpenFileAsync(string title, string fileTypeName, string pattern)
    {
        var files = await Parent.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(fileTypeName) { Patterns = [pattern] }, FilePickerFileTypes.All],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string fileTypeName, string pattern)
    {
        var file = await Parent.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(fileTypeName) { Patterns = [pattern] }],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// Oberstes sichtbares Fenster: Dialoge aus dem Einstellungsdialog (Profilname, Export, Import) erscheinen über
    /// diesem, nicht über dem Hauptfenster.
    /// </summary>
    private Window Parent
    {
        get
        {
            var window = Owner;
            while (window.OwnedWindows.LastOrDefault(child => child.IsVisible) is { } child)
            {
                window = child;
            }

            return window;
        }
    }

    /// <inheritdoc />
    public async Task<UnsavedChangesChoice> AskUnsavedChangesAsync(string fileName)
    {
        var dialog = CreateDialog(Loc.T("Close.Title"), Message(Loc.F("Close.Message", fileName)));
        var panel = ButtonRow();
        var choice = UnsavedChangesChoice.Cancel;
        foreach (var (text, value) in new[]
                 {
                     (Loc.T("Dialog.Cancel"), UnsavedChangesChoice.Cancel),
                     (Loc.T("Dialog.Discard"), UnsavedChangesChoice.Discard),
                     (Loc.T("Dialog.Save"), UnsavedChangesChoice.Save),
                 })
        {
            var button = new Button { Content = text, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) =>
            {
                choice = value;
                dialog.Close();
            };
            panel.Children.Add(button);
        }

        MarkPrimary((Button)panel.Children[^1]);
        ((StackPanel)dialog.Content!).Children.Add(panel);
        await dialog.ShowDialog(Owner);
        return choice;
    }

    /// <inheritdoc />
    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = CreateDialog(title, Message(message));
        AddButtons(dialog, (Loc.T("Dialog.Ok"), true));
        await dialog.ShowDialog<bool>(Owner);
    }

    /// <summary>
    /// Text eines Hinweises der Seitenauswahl.
    /// </summary>
    /// <param name="hint">Der Hinweis.</param>
    /// <param name="duplicateKey">Text für doppelt angegebene Seiten (extrahieren bzw. einfügen).</param>
    internal static string HintText(PageSelectionHint hint, string duplicateKey = "Extract.HintDuplicate") => hint.Kind switch
    {
        PageSelectionHintKind.ReversedRange => Loc.F("Extract.HintSwap", hint.Token, hint.RangeStart!, hint.RangeEnd!),
        PageSelectionHintKind.PageClamped => Loc.F("Extract.HintClamp", hint.Token, hint.Page!, hint.RangeEnd!),
        PageSelectionHintKind.DuplicatePage => Loc.F(duplicateKey, hint.Page!),
        _ => Loc.F("Extract.HintInvalid", hint.Token),
    };

    /// <summary>Dateityp PDF für die Dateiauswahl.</summary>
    private static FilePickerFileType PdfFileType() =>
        new(Loc.T("Dialog.PdfFiles")) { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"], AppleUniformTypeIdentifiers = ["com.adobe.pdf"] };

    /// <summary>Entfernt Zugriffstasten-Unterstriche aus Menütexten.</summary>
    private static string Clean(string text) => text.Replace("_", string.Empty, StringComparison.Ordinal);

    /// <summary>Ressource aus dem aktuellen Theme.</summary>
    internal static IBrush? Resource(string key) =>
        Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) ? value as IBrush : null;

    /// <summary>Erzeugt das Grundgerüst eines modalen Dialogs.</summary>
    internal static Window CreateDialog(string title, Control content) => new()
    {
        Title = title,
        SizeToContent = SizeToContent.WidthAndHeight,
        CanResize = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        MinWidth = 380,
        MaxWidth = 600,
        Background = Resource("SurfaceBrush"),
        Content = new StackPanel { Margin = new Thickness(20), Spacing = 18, Children = { content } },
    };

    /// <summary>Umbrechender Meldungstext.</summary>
    internal static TextBlock Message(string message) => new() { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };

    /// <summary>Hinweis in Warnfarben (Extract).</summary>
    internal static Border Warning(string text) => new()
    {
        Background = Resource("WarnBackgroundBrush"), CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 5),
        Child = new TextBlock { Text = "⚠ " + text, Foreground = Resource("WarnTextBrush"), FontSize = 12, TextWrapping = TextWrapping.Wrap },
    };

    /// <summary>Seitenchip mit Rahmen in Akzentfarbe (A16).</summary>
    private static Border Chip(string text) => new()
    {
        Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 1), CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1),
        BorderBrush = Resource("AccentBorderBrush"), Background = Resource("AccentSoftBrush"),
        Child = new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, FontSize = 12, Foreground = Resource("AccentTextBrush") },
    };

    /// <summary>Beschriftetes Steuerelement in einer Zeile.</summary>
    private static Grid Labeled(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    /// <summary>Zeile für rechtsbündige Schaltflächen.</summary>
    internal static StackPanel ButtonRow() => new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

    /// <summary>Kennzeichnet die Standardschaltfläche.</summary>
    internal static void MarkPrimary(Button button)
    {
        button.IsDefault = true;
        button.Classes.Add("accent");
    }

    /// <summary>
    /// Fügt rechtsbündige Schaltflächen hinzu; die letzte ist die Standardschaltfläche und wird zurückgegeben.
    /// </summary>
    private static Button AddButtons(Window dialog, params (string Text, bool Result)[] buttons) => AddButtons(dialog, null, buttons);

    /// <summary>
    /// Fügt rechtsbündige Schaltflächen hinzu; bestätigende Schaltflächen schließen nur, wenn die Prüfung gelingt.
    /// </summary>
    private static Button AddButtons(Window dialog, Func<bool>? validate, params (string Text, bool Result)[] buttons)
    {
        var panel = ButtonRow();
        foreach (var (text, result) in buttons)
        {
            var button = new Button { Content = text, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) =>
            {
                if (!result || validate is null || validate())
                {
                    dialog.Close(result);
                }
            };
            panel.Children.Add(button);
        }

        var primary = (Button)panel.Children[^1];
        MarkPrimary(primary);
        ((StackPanel)dialog.Content!).Children.Add(panel);
        return primary;
    }
}
