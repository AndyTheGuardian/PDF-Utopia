using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Forms;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Formularfelder der angezeigten Seite als Eingabefelder (A50, 7b, Mockup v6): Textfelder, Kästchen,
/// Optionsfelder, Klapp- und Auswahllisten liegen genau über ihrem Platz auf der Seite. Ausfüllen nur mit dem
/// Werkzeug „Auswählen“; jede Änderung ist ein Rückgängig-Schritt (Textfelder beim Verlassen des Felds).
/// </summary>
public sealed partial class PageEditor
{
    /// <summary>Ob Formularfelder dezent in der Akzentfarbe hinterlegt werden.</summary>
    public static readonly StyledProperty<bool> HighlightFieldsProperty =
        AvaloniaProperty.Register<PageEditor, bool>(nameof(HighlightFields), true);

    private readonly List<FormControl> _formControls = [];
    private (DocumentViewModel? Document, int PageId, PageRotation Rotation)? _formPage;
    private bool _updatingForm;

    /// <summary>Ob Formularfelder hervorgehoben werden.</summary>
    public bool HighlightFields
    {
        get => GetValue(HighlightFieldsProperty);
        set => SetValue(HighlightFieldsProperty, value);
    }

    /// <summary>Neues Aussehen aller Felder (Hervorhebung oder Akzentfarbe geändert).</summary>
    private void ApplyFormLook()
    {
        foreach (var control in _formControls)
        {
            ApplyFieldLook(control);
        }
    }

    /// <summary>
    /// Legt die Eingabefelder der angezeigten Seite an – nur bei Seitenwechsel, Drehung oder neuem Dokument, damit
    /// ein Feld beim Schreiben den Fokus behält.
    /// </summary>
    private void RebuildFormFields()
    {
        var document = Document;
        var page = document?.CurrentPage;
        var key = page is null ? ((DocumentViewModel?, int, PageRotation)?)null : (document, page.Id, page.Rotation);
        if (key == _formPage && (key is null || _formControls.Count > 0 || document?.IsFillable(page!) != true))
        {
            return;
        }

        CommitFormText();
        foreach (var control in _formControls)
        {
            VisualChildren.Remove(control.Host);
            LogicalChildren.Remove(control.Host);
        }

        _formControls.Clear();
        _formPage = key;
        if (document is null || page is null || !document.IsFillable(page))
        {
            return;
        }

        var tabIndex = 0;
        foreach (var (field, widget) in document.Document.Form.OnPage(page.SourcePageNumber))
        {
            var control = CreateFormControl(field, widget, document);
            KeyboardNavigation.SetTabIndex(control.Input, tabIndex++);
            ApplyFieldLook(control);
            _formControls.Add(control);
            LogicalChildren.Add(control.Host);
            VisualChildren.Add(control.Host);
        }

        UpdateFormValues(null);
        UpdateFormInteraction();
        InvalidateMeasure();
    }

    /// <summary>
    /// Baut das Eingabefeld zu einem Feld; es steht immer im hellen Theme, weil es auf weißem Papier liegt.
    /// </summary>
    private FormControl CreateFormControl(FormField field, FormWidget widget, DocumentViewModel document)
    {
        var height = widget.Bounds.Height;
        Control input;
        switch (field.Kind)
        {
            case FormFieldKind.Text:
                var text = new TextBox
                {
                    AcceptsReturn = field.Multiline, TextWrapping = field.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    MaxLength = field.MaxLength, IsReadOnly = field.ReadOnly, FontFamily = TextFont, FontSize = field.FontSizeFor(height),
                    Padding = new Thickness(2, 0), MinHeight = 0, MinWidth = 0, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(1),
                    VerticalContentAlignment = field.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                    TextAlignment = field.Alignment switch { 1 => TextAlignment.Center, 2 => TextAlignment.Right, _ => TextAlignment.Left },
                };
                text.LostFocus += (_, _) => document.Document.SetFieldValue(field.Name, text.Text ?? string.Empty);
                input = text;
                break;
            case FormFieldKind.CheckBox:
                var box = new CheckBox { IsEnabled = !field.ReadOnly, MinWidth = 0, MinHeight = 0, Padding = default };
                box.IsCheckedChanged += (_, _) =>
                {
                    if (!_updatingForm)
                    {
                        document.Document.SetFieldValue(field.Name, box.IsChecked == true ? widget.OnState ?? "Yes" : FormField.Off);
                    }
                };
                input = new Viewbox { Stretch = Stretch.Uniform, Child = box };
                break;
            case FormFieldKind.Radio:
                var radio = new RadioButton { GroupName = "pdf-form:" + field.Name, IsEnabled = !field.ReadOnly, MinWidth = 0, MinHeight = 0, Padding = default };
                radio.IsCheckedChanged += (_, _) =>
                {
                    if (!_updatingForm && radio.IsChecked == true && widget.OnState is { } state)
                    {
                        document.Document.SetFieldValue(field.Name, state);
                    }
                };
                input = new Viewbox { Stretch = Stretch.Uniform, Child = radio };
                break;
            case FormFieldKind.ComboBox:
                var combo = new ComboBox
                {
                    ItemsSource = field.Options.Select(option => option.Label).ToList(), IsEnabled = !field.ReadOnly,
                    FontFamily = TextFont, FontSize = field.FontSizeFor(height), MinHeight = 0, Padding = new Thickness(4, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                };
                combo.SelectionChanged += (_, _) =>
                {
                    if (!_updatingForm && combo.SelectedIndex >= 0)
                    {
                        document.Document.SetFieldValue(field.Name, field.Options[combo.SelectedIndex].Value);
                    }
                };
                input = combo;
                break;
            default:
                var list = new ListBox
                {
                    ItemsSource = field.Options.Select(option => option.Label).ToList(), IsEnabled = !field.ReadOnly,
                    FontFamily = TextFont, FontSize = field.FontSizeFor(20), Padding = default,
                };
                list.SelectionChanged += (_, _) =>
                {
                    if (!_updatingForm && list.SelectedIndex >= 0)
                    {
                        document.Document.SetFieldValue(field.Name, field.Options[list.SelectedIndex].Value);
                    }
                };
                input = list;
                break;
        }

        if (field.ReadOnly)
        {
            ToolTip.SetTip(input, Localization.Loc.T("Form.ReadOnly"));
        }

        var host = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Light, Child = input };
        return new FormControl(field, widget, host, input);
    }

    /// <summary>
    /// Übernimmt die aktuellen Werte in die Eingabefelder (alle oder nur die eines Felds), z. B. nach Rückgängig.
    /// </summary>
    private void UpdateFormValues(string? fieldName)
    {
        if (Document is not { } document)
        {
            return;
        }

        _updatingForm = true;
        try
        {
            foreach (var control in _formControls.Where(control => fieldName is null || control.Field.Name == fieldName))
            {
                var value = document.Document.Form.Value(control.Field.Name);
                switch (control.Input)
                {
                    case TextBox text when text.Text != value:
                        text.Text = value;
                        break;
                    case Viewbox { Child: RadioButton radio }:
                        radio.IsChecked = value == control.Widget.OnState;
                        break;
                    case Viewbox { Child: CheckBox box }:
                        box.IsChecked = value != FormField.Off && value.Length > 0;
                        break;
                    case SelectingItemsControl selector:
                        selector.SelectedIndex = control.Field.Options.ToList().FindIndex(option => option.Value == value);
                        break;
                }
            }
        }
        finally
        {
            _updatingForm = false;
        }
    }

    /// <summary>Wert eines Felds geändert (Eingabe, Rückgängig, Wiederholen): Eingabefelder nachziehen.</summary>
    private void OnFormValueChanged(object? sender, string fieldName) => UpdateFormValues(fieldName);

    /// <summary>
    /// Felder sind nur mit dem Werkzeug „Auswählen“ bedienbar; mit anderen Werkzeugen wird darüber gezeichnet.
    /// </summary>
    private void UpdateFormInteraction()
    {
        var interactive = Options?.Tool is Tool.Select or null;
        if (!interactive)
        {
            CommitFormText();
        }

        foreach (var control in _formControls)
        {
            control.Host.IsHitTestVisible = interactive;
            control.Input.Focusable = interactive;
        }
    }

    /// <summary>
    /// Übernimmt Text aus Textfeldern, deren Fokus nicht mehr regulär endet (Seitenwechsel, Werkzeugwechsel).
    /// </summary>
    private void CommitFormText()
    {
        if (Document is not { } document)
        {
            return;
        }

        foreach (var control in _formControls)
        {
            if (control.Input is TextBox { IsReadOnly: false } text)
            {
                document.Document.SetFieldValue(control.Field.Name, text.Text ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// Aussehen eines Felds: hervorgehoben dezent in der Akzentfarbe, sonst durchsichtig; schreibgeschützt grau.
    /// </summary>
    private void ApplyFieldLook(FormControl control)
    {
        var accent = this.TryFindResource("AccentBrush", ActualThemeVariant, out var resource) && resource is ISolidColorBrush brush
            ? brush.Color
            : Color.Parse(Core.Theming.AccentPalette.DefaultAccent);
        var tint = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B));
        var background = control.Field.ReadOnly ? new SolidColorBrush(Color.Parse("#EEF0F3")) : HighlightFields ? tint : (IBrush)Brushes.Transparent;
        var border = HighlightFields ? new SolidColorBrush(Color.FromArgb(110, accent.R, accent.G, accent.B)) : (IBrush)Brushes.Transparent;
        switch (control.Input)
        {
            case TextBox text:
                SetFieldBrushes(text, background, border, accent);
                break;
            case ComboBox or ListBox:
                ((TemplatedControl)control.Input).Background = background;
                ((TemplatedControl)control.Input).BorderBrush = border;
                break;
            case Viewbox viewbox:
                viewbox.Child!.Opacity = 1;
                break;
        }
    }

    /// <summary>
    /// Hintergrund und Rahmen eines Textfelds in allen Zuständen (Fluent setzt sie bei Fokus und Darüberfahren neu).
    /// </summary>
    private static void SetFieldBrushes(TextBox text, IBrush background, IBrush border, Color accent)
    {
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled" })
        {
            text.Resources[key] = background;
        }

        foreach (var key in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver" })
        {
            text.Resources[key] = border;
        }

        text.Resources["TextControlBorderBrushFocused"] = new SolidColorBrush(accent);
    }

    /// <summary>Misst die Eingabefelder in ihrer Feldgröße.</summary>
    private void MeasureFormFields()
    {
        foreach (var control in _formControls)
        {
            control.Host.Measure(new Size(control.Widget.Bounds.Width, control.Widget.Bounds.Height));
        }
    }

    /// <summary>
    /// Ordnet die Eingabefelder über ihrem Platz an; auf gedrehten Seiten mit der Seite gedreht.
    /// </summary>
    private void ArrangeFormFields()
    {
        if (Document is not { } document || _formControls.Count == 0)
        {
            return;
        }

        var (pageWidth, pageHeight) = document.CurrentPageSize;
        var rotation = document.CurrentPage.Rotation;
        foreach (var control in _formControls)
        {
            var bounds = control.Widget.Bounds;
            var center = PageTransform.Rotate(new PdfPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), rotation, pageWidth, pageHeight);
            control.Host.RenderTransformOrigin = RelativePoint.Center;
            control.Host.RenderTransform = rotation == PageRotation.None ? null : new RotateTransform((int)rotation);
            control.Host.Arrange(new Rect(center.X - bounds.Width / 2, center.Y - bounds.Height / 2, bounds.Width, bounds.Height));
        }
    }

    /// <summary>
    /// Eingabefeld eines Formularfelds.
    /// </summary>
    /// <param name="Field">Das Feld.</param>
    /// <param name="Widget">Der sichtbare Teil auf dieser Seite.</param>
    /// <param name="Host">Hülle im hellen Theme (wird angeordnet).</param>
    /// <param name="Input">Das eigentliche Eingabefeld.</param>
    private sealed record FormControl(FormField Field, FormWidget Widget, ThemeVariantScope Host, Control Input);
}
