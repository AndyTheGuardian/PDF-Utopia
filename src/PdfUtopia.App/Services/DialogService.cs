using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.History;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Settings;
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
    public async Task<IReadOnlyList<int>?> AskPagesToExtractAsync(int pageCount)
    {
        var input = new TextBox { Text = pageCount > 1 ? $"1-{pageCount}" : "1", MinWidth = 380 };
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
    public async Task<SettingsChoice?> EditSettingsAsync(SettingsChoice current)
    {
        var theme = new ComboBox
        {
            ItemsSource = new[] { Loc.T("Menu.ThemeSystem"), Loc.T("Menu.ThemeLight"), Loc.T("Menu.ThemeDark") }.Select(Clean).ToList(),
            SelectedIndex = (int)current.Theme, MinWidth = 160,
        };
        var language = new ComboBox { ItemsSource = new[] { "Deutsch", "English" }, SelectedIndex = (int)current.Language, MinWidth = 160 };
        var undo = new NumericUpDown
        {
            Minimum = UndoHistory.MinLimit, Maximum = UndoHistory.MaxLimit, Value = current.UndoLimit, Increment = 1, FormatString = "0", MinWidth = 160,
        };
        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Labeled(Loc.T("Settings.Theme"), theme), Labeled(Loc.T("Settings.Language"), language),
                Labeled(Loc.T("Settings.UndoSteps"), undo), new TextBlock { Text = Loc.T("Settings.UndoHelp"), Classes = { "hint" } },
            },
        };
        var dialog = CreateDialog(Loc.T("Settings.Title"), content);
        AddButtons(dialog, (Loc.T("Dialog.Cancel"), false), (Loc.T("Dialog.Ok"), true));
        return await dialog.ShowDialog<bool>(Owner)
            ? new SettingsChoice((ThemePreference)theme.SelectedIndex, (UiLanguage)language.SelectedIndex, UndoHistory.ClampLimit((int)(undo.Value ?? current.UndoLimit)))
            : null;
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
    private static string HintText(PageSelectionHint hint) => hint.Kind switch
    {
        PageSelectionHintKind.ReversedRange => Loc.F("Extract.HintSwap", hint.Token, hint.RangeStart!, hint.RangeEnd!),
        PageSelectionHintKind.PageClamped => Loc.F("Extract.HintClamp", hint.Token, hint.Page!, hint.RangeEnd!),
        PageSelectionHintKind.DuplicatePage => Loc.F("Extract.HintDuplicate", hint.Page!),
        _ => Loc.F("Extract.HintInvalid", hint.Token),
    };

    /// <summary>Dateityp PDF für die Dateiauswahl.</summary>
    private static FilePickerFileType PdfFileType() =>
        new(Loc.T("Dialog.PdfFiles")) { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"], AppleUniformTypeIdentifiers = ["com.adobe.pdf"] };

    /// <summary>Entfernt Zugriffstasten-Unterstriche aus Menütexten.</summary>
    private static string Clean(string text) => text.Replace("_", string.Empty, StringComparison.Ordinal);

    /// <summary>Ressource aus dem aktuellen Theme.</summary>
    private static IBrush? Resource(string key) =>
        Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) ? value as IBrush : null;

    /// <summary>Erzeugt das Grundgerüst eines modalen Dialogs.</summary>
    private static Window CreateDialog(string title, Control content) => new()
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
    private static TextBlock Message(string message) => new() { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };

    /// <summary>Hinweis in Warnfarben (Extract).</summary>
    private static Border Warning(string text) => new()
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
    private static StackPanel ButtonRow() => new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

    /// <summary>Kennzeichnet die Standardschaltfläche.</summary>
    private static void MarkPrimary(Button button)
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
