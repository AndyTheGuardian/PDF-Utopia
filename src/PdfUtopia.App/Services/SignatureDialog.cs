using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PdfUtopia.App.Controls;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Signatures;

namespace PdfUtopia.App.Services;

/// <summary>
/// Dialog „Unterschriften“ (A49, Mockup v5): links fünf Plätze mit Vorschau, Name und Löschen, rechts das
/// Unterschriftenfeld mit Name, Farbe und Andruck. Jede Änderung wird sofort gespeichert.
/// </summary>
internal sealed class SignatureDialog
{
    private readonly DialogService _dialogs;
    private readonly List<Signature> _signatures;
    private readonly Action<IReadOnlyList<Signature>> _persist;
    private readonly StackPanel _slots = new() { Spacing = 6, Width = 170 };
    private readonly SignaturePad _pad = new();
    private readonly TextBox _name = new() { Width = 170 };
    private readonly StackPanel _colors = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _pressure = new() { Content = Loc.T("Options.Pressure"), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _save = new() { Content = Loc.T("Signature.Save"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Window _window;
    private int _slot;
    private string _color = Signature.Colors[0];

    /// <summary>
    /// Baut den Dialog auf.
    /// </summary>
    /// <param name="dialogs">Dialogdienst (Besitzerfenster, gemeinsame Bausteine).</param>
    /// <param name="signatures">Gespeicherte Unterschriften.</param>
    /// <param name="startSlot">Zuerst gewählter Platz (ein freier Platz für „+ Neue“).</param>
    /// <param name="persist">Speichert die Unterschriften nach jeder Änderung.</param>
    public SignatureDialog(DialogService dialogs, IReadOnlyList<Signature> signatures, int startSlot, Action<IReadOnlyList<Signature>> persist)
    {
        _dialogs = dialogs;
        _signatures = signatures.ToList();
        _persist = persist;

        var clear = new Button { Content = Loc.T("Signature.Clear"), HorizontalAlignment = HorizontalAlignment.Right };
        clear.Click += (_, _) => _pad.Clear();
        _pressure.IsCheckedChanged += (_, _) => _pad.PressureSensitive = _pressure.IsChecked == true;
        _pad.Changed += (_, _) => _save.IsEnabled = _pad.HasStrokes;

        var options = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 12, LineSpacing = 6 };
        options.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = Loc.T("Signature.Name"), VerticalAlignment = VerticalAlignment.Center }, _name },
        });
        options.Children.Add(_colors);
        options.Children.Add(_pressure);

        var editor = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new Border
                {
                    CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = DialogService.Resource("AppBorderBrush"),
                    ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, Child = _pad,
                },
                new DockPanel { Children = { WithDock(clear, Dock.Right), options } },
                DialogService.Warning(Loc.T("Signature.Note")),
            },
        };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16 };
        layout.Children.Add(_slots);
        Grid.SetColumn(editor, 1);
        layout.Children.Add(editor);

        _window = DialogService.CreateDialog(Loc.T("Signature.Title"), layout);
        _window.MaxWidth = 760;
        AddButtons();
        Load(Math.Clamp(startSlot, 0, Math.Min(_signatures.Count, Signature.MaxCount - 1)));
    }

    /// <summary>
    /// Zeigt den Dialog.
    /// </summary>
    /// <returns>Die Unterschriften nach allen Änderungen.</returns>
    public async Task<IReadOnlyList<Signature>> ShowAsync()
    {
        await _window.ShowDialog(_dialogs.Owner);
        return _signatures;
    }

    /// <summary>
    /// Schließen und Speichern.
    /// </summary>
    private void AddButtons()
    {
        var close = new Button { Content = Loc.T("Signature.Close"), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        close.Click += (_, _) => _window.Close();
        _save.Click += (_, _) => Save();
        DialogService.MarkPrimary(_save);
        var row = DialogService.ButtonRow();
        row.Children.Add(close);
        row.Children.Add(_save);
        ((StackPanel)_window.Content!).Children.Add(row);
    }

    /// <summary>
    /// Zeigt einen Platz zum Bearbeiten: belegte Plätze mit ihrer Unterschrift, der freie Platz leer.
    /// </summary>
    private void Load(int slot)
    {
        _slot = slot;
        var signature = slot < _signatures.Count ? _signatures[slot] : null;
        _pad.Load(signature?.Strokes ?? []);
        _name.Text = signature?.Name ?? Loc.F("Signature.DefaultName", slot + 1);
        _color = signature?.ColorHex ?? Signature.Colors[0];
        _pressure.IsChecked = signature?.PressureSensitive ?? true;
        _pad.ColorHex = _color;
        _pad.PressureSensitive = _pressure.IsChecked == true;
        _save.IsEnabled = _pad.HasStrokes;
        RenderSlots();
        RenderColors();
    }

    /// <summary>
    /// Speichert das Feld auf dem gewählten Platz.
    /// </summary>
    private void Save()
    {
        if (!_pad.HasStrokes)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(_name.Text) ? Loc.F("Signature.DefaultName", _slot + 1) : _name.Text.Trim();
        var signature = new Signature(name, _color, _pressure.IsChecked == true, _pad.Strokes);
        if (_slot < _signatures.Count)
        {
            _signatures[_slot] = signature;
        }
        else
        {
            _signatures.Add(signature);
        }

        _persist(_signatures);
        RenderSlots();
    }

    /// <summary>
    /// Löscht die Unterschrift eines Platzes.
    /// </summary>
    private void Delete(int slot)
    {
        _signatures.RemoveAt(slot);
        _persist(_signatures);
        Load(Math.Min(_slot, _signatures.Count));
    }

    /// <summary>
    /// Plätze links: belegte mit Vorschau, Name und ×, danach ein freier Platz (solange einer frei ist).
    /// </summary>
    private void RenderSlots()
    {
        _slots.Children.Clear();
        for (var i = 0; i < Signature.MaxCount; i++)
        {
            var slot = i;
            var selected = slot == _slot;
            if (slot < _signatures.Count)
            {
                var delete = new Button { Content = "×", Padding = new Thickness(6, 0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
                ToolTip.SetTip(delete, Loc.T("Signature.Delete"));
                delete.Click += (_, _) => Delete(slot);
                var preview = new Border
                {
                    Background = Brushes.White, CornerRadius = new CornerRadius(4), Height = 32,
                    Child = new SignaturePreview { Signature = _signatures[slot] },
                };
                _slots.Children.Add(SlotButton(slot, selected, new DockPanel
                {
                    Children =
                    {
                        WithDock(delete, Dock.Right),
                        new StackPanel { Spacing = 2, Children = { preview, new TextBlock { Text = _signatures[slot].Name, Classes = { "hint" }, FontSize = 11 } } },
                    },
                }));
            }
            else
            {
                var free = SlotButton(slot, selected, new TextBlock { Text = Loc.T("Signature.Free"), Classes = { "hint" }, HorizontalAlignment = HorizontalAlignment.Center });
                // Nur der erste freie Platz ist wählbar, damit die Plätze lückenlos belegt werden
                free.IsEnabled = slot == _signatures.Count;
                _slots.Children.Add(free);
            }
        }

        _slots.Children.Add(new TextBlock
        {
            Text = Loc.F("Signature.Count", _signatures.Count, Signature.MaxCount), Classes = { "hint" }, HorizontalAlignment = HorizontalAlignment.Center,
        });
    }

    /// <summary>Schaltfläche eines Platzes; der gewählte ist in der Akzentfarbe umrandet.</summary>
    private Button SlotButton(int slot, bool selected, Control content)
    {
        var button = new Button
        {
            Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6), MinHeight = 44, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(selected ? 2 : 1),
            BorderBrush = selected ? DialogService.Resource("AccentBorderBrush") : DialogService.Resource("AppBorderBrush"),
            Background = selected ? DialogService.Resource("AccentSoftBrush") : DialogService.Resource("Surface2Brush"),
        };
        button.Click += (_, _) => Load(slot);
        return button;
    }

    /// <summary>Farbknöpfe; die gewählte Farbe ist in der Akzentfarbe umrandet.</summary>
    private void RenderColors()
    {
        _colors.Children.Clear();
        foreach (var hex in Signature.Colors)
        {
            var button = new Button
            {
                Width = 24, Height = 24, Padding = new Thickness(0), CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(2), BorderBrush = hex == _color ? DialogService.Resource("AccentBorderBrush") : Brushes.Transparent,
                Content = new Ellipse { Width = 16, Height = 16, Fill = new SolidColorBrush(Color.Parse(hex)) },
            };
            ToolTip.SetTip(button, Loc.T($"Signature.Color.{hex.TrimStart('#')}"));
            button.Click += (_, _) =>
            {
                _color = hex;
                _pad.ColorHex = hex;
                RenderColors();
            };
            _colors.Children.Add(button);
        }
    }

    /// <summary>Setzt die Andockseite und gibt das Steuerelement zurück.</summary>
    private static T WithDock<T>(T control, Dock dock)
        where T : Control
    {
        DockPanel.SetDock(control, dock);
        return control;
    }
}
