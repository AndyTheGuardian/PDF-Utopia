namespace PdfUtopia.Core.Theming;

/// <summary>
/// Standardfarben der Werkzeuge und Regeln für Farben auf der (weißen) PDF-Seite (A15).
/// </summary>
public static class DrawingColors
{
    /// <summary>Standardfarbe für Freihand (Rot).</summary>
    public const string FreehandDefault = "#C93434";

    /// <summary>Standardfarbe für Rechtecke (Blau).</summary>
    public const string RectangleDefault = "#2F6FDF";

    /// <summary>Standardfarbe für Kommentare (Indigo).</summary>
    public const string CommentDefault = AccentPalette.DefaultAccent;

    /// <summary>Feste Textmarker-Farben (Gelb, Grün, Rosa, Blau, Orange).</summary>
    public static IReadOnlyList<string> MarkerColors { get; } = ["#FDE047", "#86EFAC", "#F9A8D4", "#7DD3FC", "#FDBA74"];

    /// <summary>Papierfarbe, auf der Kommentare dargestellt werden.</summary>
    public const string Paper = "#FFFFFF";

    /// <summary>Grauer Ersatzrahmen für Kommentarfarben, die sich kaum vom Papier abheben.</summary>
    public const string FallbackCommentBorder = "#8A8F99";

    /// <summary>
    /// Rahmenfarbe eines Kommentars: die Kommentarfarbe, bei zu geringem Kontrast zum Papier grau.
    /// </summary>
    /// <param name="commentHex">Kommentarfarbe.</param>
    /// <returns>Die Rahmenfarbe.</returns>
    public static string CommentBorder(string commentHex) =>
        AccentPalette.NeedsOutline(commentHex, Paper) ? FallbackCommentBorder : AccentPalette.Normalize(commentHex);

    /// <summary>Höchste Deckkraft der Hinterlegung von Suchtreffern.</summary>
    public const double MaxSearchHighlightOpacity = 0.35;

    /// <summary>Kleinste Deckkraft der Hinterlegung von Suchtreffern.</summary>
    public const double MinSearchHighlightOpacity = 0.1;

    /// <summary>
    /// Mindesthelligkeit (relative Leuchtdichte) der hinterlegten Fläche, damit dunkler Text lesbar bleibt
    /// (Kontrast zu Schwarz ≥ 10:1).
    /// </summary>
    public const double MinSearchHighlightLuminance = 0.45;

    /// <summary>Unter diesem Kontrast zum Papier ist eine Hinterlegung kaum sichtbar und bekommt einen Rahmen.</summary>
    public const double MinSearchHighlightVisibility = 1.12;

    private const double OpacityStep = 0.05;

    /// <summary>
    /// Hinterlegung von Suchtreffern in der Akzentfarbe, lesbar bei jeder Farbe: Die Deckkraft wird so weit
    /// verringert, dass dunkler Text auf der Fläche lesbar bleibt; ist die Fläche auf weißem Papier kaum zu
    /// sehen (sehr helle Farben), kommt ein satterer Rahmen hinzu.
    /// </summary>
    /// <param name="accentHex">Die Akzentfarbe.</param>
    /// <returns>Deckkraft der Fläche und Rahmenfarbe (oder <c>null</c> ohne Rahmen).</returns>
    public static (double Opacity, string? OutlineHex) SearchHighlight(string accentHex)
    {
        var opacity = MaxSearchHighlightOpacity;
        while (opacity > MinSearchHighlightOpacity + 1e-9 &&
               AccentPalette.RelativeLuminance(AccentPalette.Mix(Paper, accentHex, opacity)) < MinSearchHighlightLuminance)
        {
            opacity -= OpacityStep;
        }

        opacity = Math.Round(Math.Max(opacity, MinSearchHighlightOpacity), 2);
        var visible = AccentPalette.ContrastRatio(AccentPalette.Mix(Paper, accentHex, opacity), Paper) >= MinSearchHighlightVisibility;
        return (opacity, visible ? null : AccentPalette.OutlineColor(accentHex, Paper, isDarkTheme: false) ?? FallbackCommentBorder);
    }

    /// <summary>
    /// Farbe der Fußnotenzahl im Kreis der Kommentarfarbe, je nach Helligkeit dunkel oder hell.
    /// </summary>
    /// <param name="commentHex">Kommentarfarbe.</param>
    /// <returns>Die Schriftfarbe.</returns>
    public static string FootnoteNumberForeground(string commentHex) => AccentPalette.ForegroundFor(commentHex);
}
