using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PdfUtopia.App.Views;

/// <summary>
/// Einstellungsdialog; liefert beim Schließen <c>true</c> für „Übernehmen“.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    /// <summary>Lädt den Dialog.</summary>
    public SettingsWindow()
    {
        InitializeComponent();
        Services.WindowFocus.Track(this);

        // Ein geleertes Zahlenfeld bleibt beim Tippen leer; verlässt man es, zeigt es wieder den gültigen Wert (A56).
        AddHandler(InputElement.LostFocusEvent, OnFieldLostFocus, RoutingStrategies.Bubble);

        // Enter in einem Zahlenfeld übernimmt nur den Wert. Das Zahlenfeld lässt Enter danach weiterlaufen; ohne diesen
        // Abschluss würde der Standardknopf „Übernehmen“ den Dialog schließen.
        SliderGrid.AddHandler(KeyDownEvent, OnNumberFieldKeyDown, RoutingStrategies.Bubble);
    }

    private static void OnNumberFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.Source is Control source && (source as NumericUpDown ?? source.FindAncestorOfType<NumericUpDown>()) is not null)
        {
            e.Handled = true;
        }
    }

    private static void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control source && (source as NumericUpDown ?? source.FindAncestorOfType<NumericUpDown>()) is { Value: null } field)
        {
            BindingOperations.GetBindingExpressionBase(field, NumericUpDown.ValueProperty)?.UpdateTarget();
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    /// <summary>
    /// Ob der Dialog nur wegen einer neuen Sprache geschlossen wurde und in ihr wieder geöffnet werden soll (P22).
    /// Schließen mit ✕ zählt dagegen wie „Abbrechen“.
    /// </summary>
    public bool ReopenRequested { get; private set; }

    /// <summary>Größe beim Schließen.</summary>
    public Avalonia.Size ClosedSize { get; private set; }

    /// <summary>Lage beim Schließen (wie abgefragt, also samt Rahmen); nach dem Schließen meldet das Fenster sie nicht mehr.</summary>
    public Avalonia.PixelPoint ClosedPosition { get; private set; }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        ClosedSize = ClientSize;
        ClosedPosition = Services.WindowFocus.FramePosition(this);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.LanguageApplied += (_, _) =>
            {
                ReopenRequested = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Close(null));
            };
        }
    }
}
