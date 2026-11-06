using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PdfUtopia.App.Controls;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Settings;

namespace PdfUtopia.App.Services;

/// <summary>
/// Dialog „Eigener Stempel“ (A51, Mockup v7): Text (höchstens 30 Zeichen), Farbe und Vorschau.
/// </summary>
internal sealed class StampDialog
{
    private readonly DialogService _dialogs;
    private readonly string _detail;
    private readonly TextBox _text = new() { MaxLength = StampAnnotation.MaxTextLength, MinWidth = 300 };
    private readonly StackPanel _colors = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StampPreview _preview = new() { Height = 60, Width = 320 };
    private readonly Button _save = new() { Content = Loc.T("Stamp.Save"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Window _window;
    private string _color = StampAnnotation.Colors[2];
    private CustomStamp? _result;

    /// <summary>
    /// Baut den Dialog auf.
    /// </summary>
    /// <param name="dialogs">Dialogdienst (Besitzerfenster).</param>
    /// <param name="detail">Zweite Zeile für die Vorschau (Datum, Name) oder leer.</param>
    public StampDialog(DialogService dialogs, string detail)
    {
        _dialogs = dialogs;
        _detail = detail;
        _text.TextChanged += (_, _) => Update();
        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = Loc.T("Stamp.Text") }, _text,
                new TextBlock { Text = Loc.T("Stamp.TextHelp"), Classes = { "hint" }, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 },
                new TextBlock { Text = Loc.T("Options.Color"), Margin = new Thickness(0, 6, 0, 0) }, _colors,
                new Border
                {
                    Background = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                    BorderBrush = DialogService.Resource("AppBorderBrush"), Padding = new Thickness(12), Margin = new Thickness(0, 6, 0, 0), Child = _preview,
                },
            },
        };
        _window = DialogService.CreateDialog(Loc.T("Stamp.NewTitle"), content);
        var cancel = new Button { Content = Loc.T("Dialog.Cancel"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => _window.Close();
        _save.Click += (_, _) =>
        {
            _result = new CustomStamp(_text.Text!.Trim(), _color);
            _window.Close();
        };
        DialogService.MarkPrimary(_save);
        var row = DialogService.ButtonRow();
        row.Children.Add(cancel);
        row.Children.Add(_save);
        ((StackPanel)_window.Content!).Children.Add(row);
        _window.Opened += (_, _) => _text.Focus();
        RenderColors();
        Update();
    }

    /// <summary>
    /// Zeigt den Dialog.
    /// </summary>
    /// <returns>Der neue Stempel oder <c>null</c> bei Abbruch.</returns>
    public async Task<CustomStamp?> ShowAsync()
    {
        await _window.ShowDialog(_dialogs.Owner);
        return _result;
    }

    /// <summary>Vorschau und Speichern-Schaltfläche nach dem Text.</summary>
    private void Update()
    {
        var text = _text.Text?.Trim() ?? string.Empty;
        _save.IsEnabled = text.Length > 0;
        _preview.Stamp = text.Length > 0 ? StampAnnotation.Create(text, _detail, _color, new PdfPoint(0, 0), StampAnnotation.DefaultWidth) : null;
    }

    /// <summary>Farbknöpfe; die gewählte Farbe ist in der Akzentfarbe umrandet.</summary>
    private void RenderColors()
    {
        _colors.Children.Clear();
        foreach (var hex in StampAnnotation.Colors)
        {
            var button = new Button
            {
                Width = 24, Height = 24, Padding = new Thickness(0), CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(2), BorderBrush = hex == _color ? DialogService.Resource("AccentBorderBrush") : Brushes.Transparent,
                Content = new Ellipse { Width = 16, Height = 16, Fill = new SolidColorBrush(Color.Parse(hex)) },
            };
            ToolTip.SetTip(button, Loc.T($"Stamp.Color.{hex.TrimStart('#')}"));
            button.Click += (_, _) =>
            {
                _color = hex;
                RenderColors();
                Update();
            };
            _colors.Children.Add(button);
        }
    }
}
