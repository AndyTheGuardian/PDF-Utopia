using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.Services;

/// <summary>
/// Setzt Erscheinungsbild (System, Hell, Dunkel) und Akzentfarbe. Alle von der Akzentfarbe abhängigen
/// Ressourcen werden bei jedem Theme-Wechsel neu berechnet, weil Hinterlegung und Rahmen vom Hintergrund
/// abhängen (A14, A16): Bei zu geringem Kontrast wird der Rahmen in einer satteren Variante der Akzentfarbe
/// gezeichnet, bei neutralen Farben in Textfarbe.
/// </summary>
public sealed class ThemeService
{
    private const string LightSurface = "#F7F8FA";
    private const string DarkSurface = "#1F1F1F";
    private const string LightText = "#1D2330";
    private const string DarkText = "#E6E6E6";
    private const double SoftAccentAmount = 0.18;

    private readonly Application _application;
    private string? _accentHex;

    /// <summary>
    /// Erzeugt den Dienst und reagiert auf Theme-Wechsel (auch durch das Betriebssystem).
    /// </summary>
    /// <param name="application">Die Anwendung.</param>
    public ThemeService(Application application)
    {
        _application = application;
        _application.ActualThemeVariantChanged += (_, _) => UpdateAccentResources();
    }

    /// <summary>Ob gerade das dunkle Theme aktiv ist.</summary>
    public bool IsDark => _application.ActualThemeVariant == ThemeVariant.Dark;

    /// <summary>
    /// Wendet die gewünschte Theme-Variante an.
    /// </summary>
    /// <param name="theme">Die Auswahl.</param>
    public void Apply(ThemePreference theme)
    {
        _application.RequestedThemeVariant = theme switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        UpdateAccentResources();
    }

    /// <summary>
    /// Wendet eine Akzentfarbe an.
    /// </summary>
    /// <param name="hex">Die Farbe oder <c>null</c> für den Standard des Themes.</param>
    public void ApplyAccent(string? hex)
    {
        _accentHex = hex;
        UpdateAccentResources();
    }

    /// <summary>
    /// Wirksame Akzentfarbe im aktuellen Theme.
    /// </summary>
    public string CurrentAccent => _accentHex ?? (IsDark ? AccentPalette.DefaultAccentDark : AccentPalette.DefaultAccent);

    /// <summary>
    /// Schreibt alle von der Akzentfarbe abhängigen Ressourcen.
    /// </summary>
    private void UpdateAccentResources()
    {
        var dark = IsDark;
        var accentHex = CurrentAccent;
        var surface = dark ? DarkSurface : LightSurface;
        var text = dark ? DarkText : LightText;
        var lowContrast = AccentPalette.NeedsOutline(accentHex, surface);
        var border = lowContrast ? AccentPalette.OutlineColor(accentHex, surface, dark) ?? text : accentHex;

        var accent = Color.Parse(accentHex);
        var resources = _application.Resources;
        resources["AccentBrush"] = new SolidColorBrush(accent);
        resources["AccentSoftBrush"] = new SolidColorBrush(Color.Parse(AccentPalette.Mix(surface, accentHex, SoftAccentAmount)));
        resources["AccentForegroundBrush"] = new SolidColorBrush(Color.Parse(AccentPalette.ForegroundFor(accentHex)));
        resources["AccentBorderBrush"] = new SolidColorBrush(Color.Parse(border));
        resources["AccentTextBrush"] = new SolidColorBrush(Color.Parse(lowContrast ? text : accentHex));

        // Fluent-Steuerelemente (Checkboxen, Schieberegler, Auswahl) übernehmen die Akzentfarbe
        resources["SystemAccentColor"] = accent;
        for (var step = 1; step <= 3; step++)
        {
            resources[$"SystemAccentColorLight{step}"] = Color.Parse(AccentPalette.Mix(accentHex, "#FFFFFF", step * 0.15));
            resources[$"SystemAccentColorDark{step}"] = Color.Parse(AccentPalette.Mix(accentHex, "#000000", step * 0.15));
        }
    }
}
