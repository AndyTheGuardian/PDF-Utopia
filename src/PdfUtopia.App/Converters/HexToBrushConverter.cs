using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.Converters;

/// <summary>
/// Wandelt einen Farbwert <c>#RRGGBB</c> in einen Pinsel; ungültige Werte ergeben einen transparenten Pinsel.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && AccentPalette.IsValidHex(hex) ? new SolidColorBrush(Color.Parse(hex)) : Brushes.Transparent;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
