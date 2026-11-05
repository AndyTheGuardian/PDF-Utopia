using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.History;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.Core.Settings;

/// <summary>
/// Gewähltes Erscheinungsbild.
/// </summary>
public enum ThemePreference
{
    /// <summary>Wie das Betriebssystem.</summary>
    System,

    /// <summary>Hell.</summary>
    Light,

    /// <summary>Dunkel.</summary>
    Dark,
}

/// <summary>
/// Sprache der Oberfläche (F9).
/// </summary>
public enum UiLanguage
{
    /// <summary>Deutsch.</summary>
    German,

    /// <summary>Englisch.</summary>
    English,
}

/// <summary>
/// Dauerhaft gespeicherte Einstellungen. Fehlende oder ungültige Werte werden beim Laden durch die
/// Standardwerte ersetzt (<see cref="Normalized"/>), damit eine beschädigte Datei den Start nicht verhindert.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Erscheinungsbild.</summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    /// <summary>Sprache; <c>null</c> = nach der Sprache des Betriebssystems.</summary>
    public UiLanguage? Language { get; init; }

    /// <summary>Akzentfarbe; <c>null</c> = Standard des Themes.</summary>
    public string? AccentColor { get; init; }

    /// <summary>Sättigung der Pastellfarben in Prozent.</summary>
    public int PastelSaturation { get; init; } = AccentPalette.DefaultSaturation;

    /// <summary>Benutzerdefinierte Farben (höchstens 10).</summary>
    public IReadOnlyList<string> CustomColors { get; init; } = [];

    /// <summary>Höchstzahl der Rückgängig-Schritte.</summary>
    public int UndoLimit { get; init; } = UndoHistory.DefaultLimit;

    /// <summary>Farbe für Freihand.</summary>
    public string FreehandColor { get; init; } = DrawingColors.FreehandDefault;

    /// <summary>Farbe für Rechtecke.</summary>
    public string RectangleColor { get; init; } = DrawingColors.RectangleDefault;

    /// <summary>Farbe für Kommentare.</summary>
    public string CommentColor { get; init; } = DrawingColors.CommentDefault;

    /// <summary>Farbe des Text-Werkzeugs.</summary>
    public string TextColor { get; init; } = DrawingColors.TextDefault;

    /// <summary>Schriftgröße des Text-Werkzeugs (8–36).</summary>
    public double TextSize { get; init; } = TextAnnotation.DefaultFontSize;

    /// <summary>Farbe des Markers.</summary>
    public string MarkerColor { get; init; } = DrawingColors.MarkerColors[0];

    /// <summary>Strichstärke Freihand (1–12).</summary>
    public double FreehandWidth { get; init; } = 3;

    /// <summary>Strichstärke Rechteck (1–12).</summary>
    public double RectangleWidth { get; init; } = 2;

    /// <summary>Höhe des Markers (8–30).</summary>
    public double MarkerHeight { get; init; } = 18;

    /// <summary>Glättung Freihand in Prozent (0–100).</summary>
    public int Smoothing { get; init; } = 50;

    /// <summary>Ob Freihand dem Andruck folgt.</summary>
    public bool PressureSensitive { get; init; }

    /// <summary>Radius des Radierers in Punkten.</summary>
    public double EraserRadius { get; init; } = EraserHitTest.DefaultRadius;

    /// <summary>
    /// Ersetzt ungültige Werte durch gültige.
    /// </summary>
    /// <returns>Die bereinigten Einstellungen.</returns>
    public AppSettings Normalized() => this with
    {
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
        Language = Language is { } language && Enum.IsDefined(language) ? language : null,
        AccentColor = AccentPalette.IsValidHex(AccentColor) ? AccentPalette.Normalize(AccentColor!) : null,
        PastelSaturation = AccentPalette.ClampSaturation(PastelSaturation),
        CustomColors = (CustomColors ?? []).Where(AccentPalette.IsValidHex).Select(AccentPalette.Normalize).Distinct()
            .TakeLast(AccentPalette.MaxCustomColors).ToList(),
        UndoLimit = UndoHistory.ClampLimit(UndoLimit),
        FreehandColor = ValidColor(FreehandColor, DrawingColors.FreehandDefault),
        RectangleColor = ValidColor(RectangleColor, DrawingColors.RectangleDefault),
        CommentColor = ValidColor(CommentColor, DrawingColors.CommentDefault),
        TextColor = ValidColor(TextColor, DrawingColors.TextDefault),
        TextSize = TextAnnotation.ClampFontSize(TextSize),
        MarkerColor = DrawingColors.MarkerColors.Contains(MarkerColor) ? MarkerColor : DrawingColors.MarkerColors[0],
        FreehandWidth = Math.Clamp(FreehandWidth, 1, 12),
        RectangleWidth = Math.Clamp(RectangleWidth, 1, 12),
        MarkerHeight = Math.Clamp(MarkerHeight, 8, 30),
        Smoothing = Math.Clamp(Smoothing, 0, 100),
        EraserRadius = EraserHitTest.ClampRadius(EraserRadius),
    };

    /// <summary>
    /// Sprache aus der Einstellung oder, falls keine gewählt, aus der Sprache des Betriebssystems.
    /// </summary>
    /// <param name="systemLanguageName">Zweibuchstabiger ISO-Code der Systemsprache, z. B. „de“.</param>
    /// <returns>Deutsch bei deutscher Systemsprache, sonst Englisch.</returns>
    public UiLanguage ResolveLanguage(string systemLanguageName) =>
        Language ?? (string.Equals(systemLanguageName, "de", StringComparison.OrdinalIgnoreCase) ? UiLanguage.German : UiLanguage.English);

    /// <summary>
    /// Gültige Farbe oder Ersatz.
    /// </summary>
    private static string ValidColor(string? hex, string fallback) =>
        AccentPalette.IsValidHex(hex) ? AccentPalette.Normalize(hex!) : fallback;
}
