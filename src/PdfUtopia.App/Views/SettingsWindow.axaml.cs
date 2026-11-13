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
}
