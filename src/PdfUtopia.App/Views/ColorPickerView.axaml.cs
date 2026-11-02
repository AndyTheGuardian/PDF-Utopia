using Avalonia.Controls;

namespace PdfUtopia.App.Views;

/// <summary>
/// Gemeinsame Farbwahl (Akzentfarbe, Freihand, Rechteck, Kommentar).
/// </summary>
public sealed partial class ColorPickerView : UserControl
{
    /// <summary>
    /// Erzeugt die Ansicht.
    /// </summary>
    public ColorPickerView() => InitializeComponent();
}
