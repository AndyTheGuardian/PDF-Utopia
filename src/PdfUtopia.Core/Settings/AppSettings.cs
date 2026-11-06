using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.History;
using PdfUtopia.Core.Pages;
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

    /// <summary>Gewählte Form des Werkzeugs „Formen“ (A44).</summary>
    public ShapeKind Shape { get; init; } = ShapeKind.Rectangle;

    /// <summary>Wo Pfeile ihre Spitzen tragen (Anfang, Ende, beide).</summary>
    public ArrowEnds ArrowEnds { get; init; } = ArrowEnds.End;

    /// <summary>Farbe für Formen (Rechteck, Ellipse, Linie, Pfeil; Name aus der Zeit vor A44).</summary>
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

    /// <summary>Strichstärke der Formen (1–12).</summary>
    public double RectangleWidth { get; init; } = 2;

    /// <summary>Höhe des Markers (8–30).</summary>
    public double MarkerHeight { get; init; } = 18;

    /// <summary>Glättung Freihand in Prozent (0–100).</summary>
    public int Smoothing { get; init; } = 50;

    /// <summary>Ob Freihand dem Andruck folgt.</summary>
    public bool PressureSensitive { get; init; }

    /// <summary>Kleinste Breite der linken Leiste.</summary>
    public const double MinSidebarWidth = 180;

    /// <summary>Größte Breite der linken Leiste.</summary>
    public const double MaxSidebarWidth = 420;

    /// <summary>Ob Formularfelder hervorgehoben werden (A50).</summary>
    public bool HighlightFormFields { get; init; } = true;

    /// <summary>Gewählte Unterschrift (Position in der Liste, A49).</summary>
    public int SelectedSignature { get; init; }

    /// <summary>Gewählter Stempel: Position der Vorlage, dahinter die eigenen Stempel (A51).</summary>
    public int SelectedStamp { get; init; }

    /// <summary>Farbe des nächsten Stempels.</summary>
    public string StampColor { get; init; } = StampAnnotation.Presets[0].ColorHex;

    /// <summary>Ob Stempel das Datum tragen.</summary>
    public bool StampDate { get; init; } = true;

    /// <summary>Ob Stempel den Namen tragen.</summary>
    public bool StampName { get; init; }

    /// <summary>Name für Stempel (F35).</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Eigene Stempel (F34).</summary>
    public IReadOnlyList<CustomStamp> CustomStamps { get; init; } = [];

    /// <summary>Standardbreite der linken Leiste.</summary>
    public const double DefaultSidebarWidth = 220;

    /// <summary>Breite der linken Leiste in Pixeln (F30).</summary>
    public double SidebarWidth { get; init; } = DefaultSidebarWidth;

    /// <summary>Seitenansicht (A55), gemeinsam für alle Dokumente.</summary>
    public PageViewMode ViewMode { get; init; } = PageViewMode.Single;

    /// <summary>Bei Doppelseite: erste Seite allein rechts (A55).</summary>
    public bool FirstPageAlone { get; init; } = true;

    /// <summary>Radius des Radierers in Punkten.</summary>
    public double EraserRadius { get; init; } = EraserHitTest.DefaultRadius;

    /// <summary>
    /// Ersetzt ungültige Werte durch gültige.
    /// </summary>
    /// <returns>Die bereinigten Einstellungen.</returns>
    public AppSettings Normalized() => this with
    {
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
        Shape = Enum.IsDefined(Shape) ? Shape : ShapeKind.Rectangle,
        ArrowEnds = Enum.IsDefined(ArrowEnds) ? ArrowEnds : ArrowEnds.End,
        ViewMode = Enum.IsDefined(ViewMode) ? ViewMode : PageViewMode.Single,
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
        SelectedSignature = Math.Clamp(SelectedSignature, 0, Signatures.Signature.MaxCount - 1),
        CustomStamps = (CustomStamps ?? []).Where(stamp => stamp is not null && !string.IsNullOrWhiteSpace(stamp.Text))
            .Select(stamp => new CustomStamp(Shorten(stamp.Text.Trim(), StampAnnotation.MaxTextLength), ValidColor(stamp.ColorHex, StampAnnotation.Colors[2])))
            .Take(CustomStamp.MaxCount).ToList(),
        StampColor = ValidColor(StampColor, StampAnnotation.Presets[0].ColorHex),
        UserName = (UserName ?? string.Empty).Trim(),
        SidebarWidth = double.IsFinite(SidebarWidth) ? Math.Clamp(SidebarWidth, MinSidebarWidth, MaxSidebarWidth) : DefaultSidebarWidth,
    };

    /// <summary>
    /// Sprache aus der Einstellung oder, falls keine gewählt, aus der Sprache des Betriebssystems.
    /// </summary>
    /// <param name="systemLanguageName">Zweibuchstabiger ISO-Code der Systemsprache, z. B. „de“.</param>
    /// <returns>Deutsch bei deutscher Systemsprache, sonst Englisch.</returns>
    public UiLanguage ResolveLanguage(string systemLanguageName) =>
        Language ?? (string.Equals(systemLanguageName, "de", StringComparison.OrdinalIgnoreCase) ? UiLanguage.German : UiLanguage.English);

    /// <summary>Kürzt einen Text auf eine Höchstlänge.</summary>
    private static string Shorten(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];

    /// <summary>
    /// Gültige Farbe oder Ersatz.
    /// </summary>
    private static string ValidColor(string? hex, string fallback) =>
        AccentPalette.IsValidHex(hex) ? AccentPalette.Normalize(hex!) : fallback;
}

/// <summary>
/// Eigener Stempel (F34).
/// </summary>
/// <param name="Text">Stempeltext (höchstens 30 Zeichen).</param>
/// <param name="ColorHex">Farbe.</param>
public sealed record CustomStamp(string Text, string ColorHex)
{
    /// <summary>Höchstzahl eigener Stempel.</summary>
    public const int MaxCount = 10;
}
