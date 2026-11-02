using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Eine Farbfläche der Farbwahl.
/// </summary>
/// <param name="hex">Farbwert <c>#RRGGBB</c>.</param>
/// <param name="toolTip">Tooltip mit Name und Farbwert.</param>
/// <param name="isCustom">Ob die Farbe benutzerdefiniert (entfernbar) ist.</param>
public sealed partial class SwatchViewModel(string hex, string toolTip, bool isCustom = false) : ObservableObject
{
    /// <summary>Farbwert.</summary>
    public string Hex { get; } = hex;

    /// <summary>Tooltip.</summary>
    public string ToolTip { get; } = toolTip;

    /// <summary>Ob die Farbe entfernt werden kann.</summary>
    public bool IsCustom { get; } = isCustom;

    /// <summary>Ob die Farbe die aktuelle Farbe des Ziels ist.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Gemeinsame Farbwahl für Akzentfarbe, Freihand, Rechteck und Kommentar (A14, A15), aufgebaut wie in
/// FileRenamer: Pastell mit HSV-Sättigung, Kräftig, Neutral, Benutzerdefiniert, eigener Hex-Code, „Standard“.
/// Sättigung und benutzerdefinierte Farben gelten für alle Ziele gemeinsam.
/// </summary>
public sealed partial class ColorPickerViewModel : ObservableObject
{
    private readonly Func<ColorTarget, string?> _currentColor;

    /// <summary>
    /// Erzeugt die Farbwahl.
    /// </summary>
    /// <param name="saturation">Gespeicherte Pastell-Sättigung.</param>
    /// <param name="customColors">Gespeicherte benutzerdefinierte Farben.</param>
    /// <param name="currentColor">Liefert die aktuelle Farbe eines Ziels (<c>null</c> = Standard).</param>
    public ColorPickerViewModel(int saturation, IEnumerable<string> customColors, Func<ColorTarget, string?> currentColor)
    {
        _currentColor = currentColor;
        Saturation = AccentPalette.ClampSaturation(saturation);
        foreach (var hex in customColors)
        {
            Custom.Add(new SwatchViewModel(hex, $"{Loc.T("Accent.CustomName")} {hex}", isCustom: true));
        }

        Rebuild();
    }

    /// <summary>Wird ausgelöst, wenn eine Farbe gewählt wurde (Ziel, Farbe oder <c>null</c> für Standard).</summary>
    public event EventHandler<(ColorTarget Target, string? Hex)>? ColorChosen;

    /// <summary>Wird ausgelöst, wenn Sättigung oder benutzerdefinierte Farben sich ändern (zum Speichern).</summary>
    public event EventHandler? PaletteChanged;

    /// <summary>Pastellfarben bei der aktuellen Sättigung.</summary>
    public ObservableCollection<SwatchViewModel> Pastel { get; } = [];

    /// <summary>Kräftige Farben.</summary>
    public ObservableCollection<SwatchViewModel> Strong { get; } = [];

    /// <summary>Neutrale Farben.</summary>
    public ObservableCollection<SwatchViewModel> Neutral { get; } = [];

    /// <summary>Benutzerdefinierte Farben.</summary>
    public ObservableCollection<SwatchViewModel> Custom { get; } = [];

    /// <summary>Ob benutzerdefinierte Farben vorhanden sind.</summary>
    public bool HasCustomColors => Custom.Count > 0;

    /// <summary>Aktuelles Ziel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial ColorTarget Target { get; set; }

    /// <summary>Pastell-Sättigung in Prozent (10–90).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaturationByteText))]
    public partial int Saturation { get; set; }

    /// <summary>Eingabefeld für einen eigenen Hex-Code.</summary>
    [ObservableProperty]
    public partial string HexInput { get; set; } = string.Empty;

    /// <summary>Fehlermeldung zum Hex-Code oder <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHexError))]
    public partial string? HexError { get; set; }

    /// <summary>Ob eine Fehlermeldung angezeigt wird.</summary>
    public bool HasHexError => HexError is not null;

    /// <summary>Titel der Farbwahl, z. B. „Farbe – Kommentar“.</summary>
    public string Title => Target == ColorTarget.Accent
        ? Loc.T("Accent.Title")
        : Loc.F("Accent.TitleFor", Loc.T($"Tool.{(Target == ColorTarget.Freehand ? "Freehand" : Target.ToString())}"));

    /// <summary>Sättigung auf der Skala 0–255.</summary>
    public string SaturationByteText => $"≙ {(int)Math.Round(Saturation * 255 / 100.0, MidpointRounding.AwayFromZero)}";

    /// <summary>Wert des Schiebereglers.</summary>
    public double SliderValue
    {
        get => Saturation;
        set => SetSaturation((int)Math.Round(value));
    }

    /// <summary>
    /// Öffnet die Farbwahl für ein Ziel.
    /// </summary>
    /// <param name="target">Das Ziel.</param>
    public void OpenFor(ColorTarget target)
    {
        Target = target;
        HexError = null;
        Rebuild();
    }

    /// <summary>
    /// Übernimmt eine Farbfläche.
    /// </summary>
    /// <param name="swatch">Die Farbfläche.</param>
    [RelayCommand]
    private void Select(SwatchViewModel swatch) => Choose(swatch.Hex);

    /// <summary>
    /// Übernimmt den eingegebenen Hex-Code und nimmt ihn in „Benutzerdefiniert“ auf.
    /// </summary>
    [RelayCommand]
    private void ApplyHex()
    {
        if (!AccentPalette.IsValidHex(HexInput))
        {
            HexError = Loc.T("Accent.HexInvalid");
            return;
        }

        var hex = AccentPalette.Normalize(HexInput);
        var updated = AccentPalette.AddCustomColor(Custom.Select(swatch => swatch.Hex).ToList(), hex, Saturation);
        Custom.Clear();
        foreach (var color in updated)
        {
            Custom.Add(new SwatchViewModel(color, $"{Loc.T("Accent.CustomName")} {color}", isCustom: true));
        }

        OnPropertyChanged(nameof(HasCustomColors));
        PaletteChanged?.Invoke(this, EventArgs.Empty);
        Choose(hex);
    }

    /// <summary>
    /// Entfernt eine benutzerdefinierte Farbe.
    /// </summary>
    /// <param name="swatch">Die Farbfläche.</param>
    [RelayCommand]
    private void Remove(SwatchViewModel swatch)
    {
        Custom.Remove(swatch);
        OnPropertyChanged(nameof(HasCustomColors));
        PaletteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Setzt das Ziel auf seine Standardfarbe zurück.
    /// </summary>
    [RelayCommand]
    private void Reset() => Choose(null);

    /// <summary>
    /// Wählt eine Farbe und meldet sie.
    /// </summary>
    private void Choose(string? hex)
    {
        HexError = null;
        ColorChosen?.Invoke(this, (Target, hex));
        Rebuild();
    }

    /// <summary>
    /// Ändert die Sättigung; eine gewählte Pastellfarbe passt sich sofort an.
    /// </summary>
    private void SetSaturation(int value)
    {
        var clamped = AccentPalette.ClampSaturation(value);
        if (clamped == Saturation)
        {
            return;
        }

        var pastelIndex = Pastel.ToList().FindIndex(swatch => swatch.Hex == CurrentHex);
        Saturation = clamped;
        PaletteChanged?.Invoke(this, EventArgs.Empty);
        if (pastelIndex >= 0)
        {
            Choose(AccentPalette.Pastel(Saturation)[pastelIndex].Hex);
        }
        else
        {
            Rebuild();
        }
    }

    /// <summary>
    /// Wirksame Farbe des aktuellen Ziels.
    /// </summary>
    private string CurrentHex => _currentColor(Target) ?? DefaultHex;

    /// <summary>
    /// Standardfarbe des aktuellen Ziels.
    /// </summary>
    private string DefaultHex => ToolOptionsViewModel.DefaultColorOf(Target);

    /// <summary>
    /// Baut die Farbflächen neu auf und markiert die aktuelle Farbe.
    /// </summary>
    private void Rebuild()
    {
        Fill(Pastel, AccentPalette.Pastel(Saturation));
        Fill(Strong, AccentPalette.Strong);
        Fill(Neutral, AccentPalette.Neutral);
        var current = CurrentHex;
        foreach (var swatch in Pastel.Concat(Strong).Concat(Neutral).Concat(Custom))
        {
            swatch.IsSelected = swatch.Hex == current;
        }

        HexInput = current;
        OnPropertyChanged(nameof(SliderValue));
    }

    /// <summary>
    /// Füllt eine Gruppe; die Standardfarbe des Ziels wird im Tooltip markiert.
    /// </summary>
    private void Fill(ObservableCollection<SwatchViewModel> target, IEnumerable<ColorSwatch> palette)
    {
        target.Clear();
        foreach (var swatch in palette)
        {
            var mark = swatch.Hex == DefaultHex ? $" {Loc.T("Accent.DefaultMark")}" : string.Empty;
            target.Add(new SwatchViewModel(swatch.Hex, $"{Loc.T(swatch.NameKey)}{mark} {swatch.Hex}"));
        }
    }
}
