using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Forms;

/// <summary>Art eines Formularfelds (A50, 7b).</summary>
public enum FormFieldKind
{
    /// <summary>Textfeld, ein- oder mehrzeilig.</summary>
    Text,

    /// <summary>Kontrollkästchen.</summary>
    CheckBox,

    /// <summary>Gruppe von Optionsfeldern.</summary>
    Radio,

    /// <summary>Klappliste.</summary>
    ComboBox,

    /// <summary>Auswahlliste.</summary>
    ListBox,
}

/// <summary>
/// Sichtbarer Teil eines Felds auf einer Seite.
/// </summary>
/// <param name="SourcePageNumber">Seite der geöffneten Datei (1-basiert).</param>
/// <param name="Bounds">Lage in Koordinaten der ungedrehten Seite.</param>
/// <param name="OnState">Bei Kästchen und Optionsfeldern der Name des „an“-Zustands, sonst <c>null</c>.</param>
public sealed record FormWidget(int SourcePageNumber, PdfRect Bounds, string? OnState);

/// <summary>
/// Eintrag einer Auswahl- oder Klappliste.
/// </summary>
/// <param name="Value">Gespeicherter Wert.</param>
/// <param name="Label">Angezeigter Text.</param>
public sealed record FormOption(string Value, string Label);

/// <summary>
/// Ein ausfüllbares Feld der geöffneten Datei (A50, 7b).
/// </summary>
/// <param name="Name">Vollständiger Feldname (Teilnamen mit Punkt verbunden).</param>
/// <param name="Kind">Art.</param>
/// <param name="InitialValue">Wert in der Datei; bei Kästchen und Optionsfeldern der Zustandsname oder „Off“.</param>
/// <param name="ReadOnly">Schreibgeschützt.</param>
/// <param name="Required">Pflichtfeld.</param>
/// <param name="Multiline">Mehrzeiliges Textfeld.</param>
/// <param name="MaxLength">Höchstlänge (0 = unbegrenzt).</param>
/// <param name="FontSize">Schriftgröße aus der Standard-Darstellung (0 = an die Feldhöhe angepasst).</param>
/// <param name="Alignment">Ausrichtung: 0 links, 1 mittig, 2 rechts.</param>
/// <param name="Options">Einträge von Auswahl- und Klapplisten.</param>
/// <param name="Widgets">Sichtbare Teile.</param>
public sealed record FormField(
    string Name, FormFieldKind Kind, string InitialValue, bool ReadOnly, bool Required, bool Multiline, int MaxLength,
    double FontSize, int Alignment, IReadOnlyList<FormOption> Options, IReadOnlyList<FormWidget> Widgets)
{
    /// <summary>Zustand „aus“ von Kästchen und Optionsfeldern.</summary>
    public const string Off = "Off";

    /// <summary>
    /// Schriftgröße für ein Feld der angegebenen Höhe: aus der Darstellung oder passend zur Höhe (8–12 pt).
    /// </summary>
    /// <param name="height">Höhe des Felds.</param>
    /// <returns>Die Schriftgröße.</returns>
    public double FontSizeFor(double height) =>
        FontSize > 0 ? FontSize : Multiline ? 10 : Math.Clamp(Math.Round((height - 4) * 0.7, 1), 6, 12);
}
