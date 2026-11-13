using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.Services;

/// <summary>
/// Setzt Design und Farben der Oberfläche; für Tests austauschbar.
/// </summary>
public interface IThemeService
{
    /// <summary>Ob gerade das dunkle Design gilt (bei „System“ das des Betriebssystems).</summary>
    bool IsDark { get; }

    /// <summary>
    /// Wendet Design und Farben beider Designs an.
    /// </summary>
    /// <param name="settings">Die Einstellungen.</param>
    void Apply(AppSettings settings);
}

/// <summary>
/// Setzt das Erscheinungsbild (System/Hell/Dunkel) und schreibt die Farben als Anwendungsressourcen: Flächen, Rahmen,
/// Texte mit Farbstich und Helligkeit des Designs sowie die Akzentfarbe mit abgeleiteten Pinseln (A56, übernommen aus
/// dem AI Asset Manager). Hell und Dunkel haben je eigene Farben; bei „System“ gelten die des Designs, das das
/// Betriebssystem gerade zeigt, auch nach einem Wechsel im laufenden Betrieb. Die Flächen stehen deshalb nicht in den
/// ThemeDictionaries von <c>App.axaml</c>, sonst hätten diese Vorrang.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const double SoftAccentAmount = 0.18;

    /// <summary>Mindestkontrast für Schrift in Akzentfarbe (WCAG AA für normalen Text).</summary>
    private const double TextContrastRatio = 4.5;

    private readonly Application _application;
    private ThemeColorSettings _light = new();
    private ThemeColorSettings _dark = new();

    /// <summary>
    /// Erzeugt den Dienst für eine Anwendung.
    /// </summary>
    /// <param name="application">Die Anwendung.</param>
    public ThemeService(Application application)
    {
        _application = application;
        _application.ActualThemeVariantChanged += (_, _) => UpdateResources();
        UpdateResources();
    }

    /// <inheritdoc />
    public bool IsDark => _application.ActualThemeVariant == ThemeVariant.Dark;

    /// <summary>Wirksame Akzentfarbe des gerade geltenden Designs.</summary>
    private string CurrentAccent => (IsDark ? _dark : _light).AccentColor ?? (IsDark ? AccentPalette.DefaultAccentDark : AccentPalette.DefaultAccent);

    /// <inheritdoc />
    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _light = settings.ColorsFor(dark: false);
        _dark = settings.ColorsFor(dark: true);
        var variant = settings.Theme switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        if (_application.RequestedThemeVariant != variant)
        {
            _application.RequestedThemeVariant = variant;
        }

        UpdateResources();
    }

    /// <summary>
    /// Schreibt Flächen-, Text- und Akzentfarben des geltenden Designs. Ist der Kontrast der Akzentfarbe zur Fläche zu
    /// gering, bekommen akzentfarbige Elemente einen satteren Rahmen; Akzent-Text ist nur bei ausreichendem Kontrast
    /// akzentfarbig, sonst in der normalen Textfarbe.
    /// </summary>
    private void UpdateResources()
    {
        var dark = IsDark;
        var colors = ThemePalette.For(dark, (dark ? _dark : _light).Tint);
        var resources = _application.Resources;
        void Set(string key, string hex) => resources[key] = new SolidColorBrush(Color.Parse(hex));

        Set("AppBackgroundBrush", colors.AppBackground);
        Set("SurfaceBrush", colors.Surface);
        Set("Surface2Brush", colors.Surface2);
        Set("AppBorderBrush", colors.Border);
        Set("TextBrush", colors.Text);
        Set("MutedTextBrush", colors.MutedText);
        Set("HoverBrush", colors.Hover);
        Set("CanvasBrush", colors.Canvas);
        // Eingabefelder, Auswahllisten, Aufklappfelder und Menüs der Fluent-Steuerelemente auf dieselben Flächen
        Set("TextControlBackground", colors.Surface2);
        Set("TextControlBackgroundPointerOver", colors.Hover);
        Set("TextControlBackgroundFocused", colors.Surface);
        Set("ComboBoxBackground", colors.Surface2);
        Set("ComboBoxBackgroundPointerOver", colors.Hover);
        Set("ComboBoxDropDownBackground", colors.Surface);
        Set("MenuFlyoutPresenterBackground", colors.Surface);
        Set("FlyoutPresenterBackground", colors.Surface);

        var accentHex = CurrentAccent;
        var lowContrast = AccentPalette.NeedsOutline(accentHex, colors.Surface);
        var border = lowContrast ? AccentPalette.OutlineColor(accentHex, colors.Surface, dark) ?? colors.Text : accentHex;
        var accent = Color.Parse(accentHex);
        resources["AccentBrush"] = new SolidColorBrush(accent);
        Set("AccentSoftBrush", AccentPalette.Mix(colors.Surface, accentHex, SoftAccentAmount));
        Set("AccentForegroundBrush", AccentPalette.ForegroundFor(accentHex));
        Set("AccentBorderBrush", border);
        // Schrift in Akzentfarbe nur, wenn sie lesbar ist (4,5 : 1); sonst die normale Textfarbe (P21)
        Set("AccentTextBrush", AccentPalette.ContrastRatio(accentHex, colors.Surface) >= TextContrastRatio ? accentHex : colors.Text);

        // Fluent-Steuerelemente (Checkboxen, Schieberegler, Auswahl) übernehmen die Akzentfarbe
        resources["SystemAccentColor"] = accent;
        for (var step = 1; step <= 3; step++)
        {
            resources[$"SystemAccentColorLight{step}"] = Color.Parse(AccentPalette.Mix(accentHex, "#FFFFFF", step * 0.15));
            resources[$"SystemAccentColorDark{step}"] = Color.Parse(AccentPalette.Mix(accentHex, "#000000", step * 0.15));
        }
    }
}
