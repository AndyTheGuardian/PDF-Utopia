using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace PdfUtopia.App.Converters;

/// <summary>
/// Vergleicht einen Aufzählungswert mit dem Parameter (für Optionsfelder und Menü-Häkchen). Rückwärts wird nur
/// beim Anwählen der Parameter übernommen; das Abwählen ändert nichts.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && Equals(value.ToString(), parameter.ToString());

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? Enum.Parse(targetType, parameter.ToString()!) : BindingOperations.DoNothing;
}
