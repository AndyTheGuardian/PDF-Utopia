using System.Globalization;
using System.Text.RegularExpressions;

namespace PdfUtopia.Core.Theming;

/// <summary>
/// Eine wählbare Farbe der Farbwahl.
/// </summary>
/// <param name="Hex">Farbwert im Format <c>#RRGGBB</c>.</param>
/// <param name="NameKey">Schlüssel des Anzeigenamens; die Oberfläche übersetzt ihn (z. B. „Color.Blue“).</param>
public sealed record ColorSwatch(string Hex, string NameKey);

/// <summary>
/// Farben und Farbberechnungen der gemeinsamen Farbwahl für Akzentfarbe, Freihand, Rechteck und Kommentar.
/// Übernommen aus FileRenamer (<c>AccentPalette</c>) mit den Abweichungen aus A14: Standard-Akzent Indigo,
/// zusätzlich Gelb, sattere Rahmenfarbe bei zu geringem Kontrast.
/// </summary>
public static partial class AccentPalette
{
    /// <summary>Standard-Akzentfarbe im hellen Theme (Indigo).</summary>
    public const string DefaultAccent = "#4F46E5";

    /// <summary>Standard-Akzentfarbe im Dark Theme.</summary>
    public const string DefaultAccentDark = "#8B8CF8";

    /// <summary>Kleinste einstellbare Pastell-Sättigung in Prozent.</summary>
    public const int MinSaturation = 10;

    /// <summary>Größte einstellbare Pastell-Sättigung in Prozent.</summary>
    public const int MaxSaturation = 90;

    /// <summary>Standard-Sättigung der Pastellfarben in Prozent.</summary>
    public const int DefaultSaturation = 30;

    /// <summary>Höchstzahl benutzerdefinierter Farben.</summary>
    public const int MaxCustomColors = 10;

    /// <summary>Unterhalb dieses Kontrastverhältnisses erhalten Akzentflächen einen Rahmen.</summary>
    public const double MinContrastRatio = 1.6;

    /// <summary>Kontrast, den eine sattere Rahmenfarbe zum Hintergrund mindestens erreichen soll.</summary>
    public const double OutlineContrastRatio = 2;

    /// <summary>Unter dieser HSV-Sättigung gilt eine Farbe als neutral; ihr Rahmen nimmt die Textfarbe.</summary>
    public const double NeutralSaturation = 0.15;

    private const string DarkText = "#1D2330";
    private const string LightText = "#F4F7FC";
    private const double MixStep = 0.05;

    /// <summary>
    /// Grundfarben der Pastell-Reihe (Farbton und Hellwert bleiben, die Sättigung wird eingestellt).
    /// </summary>
    public static IReadOnlyList<ColorSwatch> PastelBase { get; } =
    [
        new("#A7C7E7", "Color.PastelBlue"), new("#B5EAD7", "Color.Mint"), new("#FFE599", "Color.PastelYellow"),
        new("#FFDAC1", "Color.Peach"), new("#E2B6CF", "Color.Rose"), new("#C7CEEA", "Color.Lavender"),
    ];

    /// <summary>
    /// Kräftige Farben.
    /// </summary>
    public static IReadOnlyList<ColorSwatch> Strong { get; } =
    [
        new(DefaultAccent, "Color.Indigo"), new("#2F6FDF", "Color.Blue"), new("#1F9D55", "Color.Green"),
        new("#FACC15", "Color.Yellow"), new("#E8740C", "Color.Orange"), new("#C93434", "Color.Red"), new("#8B4FD6", "Color.Violet"),
    ];

    /// <summary>
    /// Neutrale Farben (95 % Schwarz, 95 % Weiß).
    /// </summary>
    public static IReadOnlyList<ColorSwatch> Neutral { get; } =
        [new("#0D0D0D", "Color.Black95"), new("#F2F2F2", "Color.White95")];

    /// <summary>
    /// Liefert die Pastellfarben für eine Sättigung.
    /// </summary>
    /// <param name="saturationPercent">Sättigung in Prozent (wird auf 10–90 begrenzt).</param>
    /// <returns>Die Pastellfarben.</returns>
    public static IReadOnlyList<ColorSwatch> Pastel(int saturationPercent) =>
        PastelBase.Select(swatch => swatch with { Hex = WithHsvSaturation(swatch.Hex, ClampSaturation(saturationPercent)) }).ToList();

    /// <summary>
    /// Begrenzt eine Sättigung auf den erlaubten Bereich.
    /// </summary>
    /// <param name="saturationPercent">Sättigung in Prozent.</param>
    /// <returns>Sättigung zwischen 10 und 90.</returns>
    public static int ClampSaturation(int saturationPercent) => Math.Clamp(saturationPercent, MinSaturation, MaxSaturation);

    /// <summary>
    /// Setzt die Sättigung nach dem HSV-Prinzip (S = (max − min) / max). Farbton und Hellwert V bleiben erhalten.
    /// </summary>
    /// <param name="hex">Ausgangsfarbe.</param>
    /// <param name="saturationPercent">Gewünschte Sättigung in Prozent.</param>
    /// <returns>Die neue Farbe.</returns>
    public static string WithHsvSaturation(string hex, int saturationPercent)
    {
        var rgb = ToRgb(hex);
        double max = rgb.Max(), min = rgb.Min();
        var newMin = max * (1 - saturationPercent / 100.0);
        var scaled = rgb.Select(channel => max == min ? max : newMin + (channel - min) / (max - min) * (max - newMin));
        return ToHex(scaled.Select(value => (int)Math.Round(value, MidpointRounding.AwayFromZero)).ToArray());
    }

    /// <summary>
    /// HSV-Sättigung einer Farbe.
    /// </summary>
    /// <param name="hex">Die Farbe.</param>
    /// <returns>Sättigung 0–1.</returns>
    public static double HsvSaturation(string hex)
    {
        var rgb = ToRgb(hex);
        double max = rgb.Max();
        return max == 0 ? 0 : (max - rgb.Min()) / max;
    }

    /// <summary>
    /// Prüft das Format <c>#RRGGBB</c>.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <returns><c>true</c>, wenn gültig.</returns>
    public static bool IsValidHex(string? text) => text is not null && HexPattern().IsMatch(text.Trim());

    /// <summary>
    /// Vereinheitlicht einen gültigen Hex-Code auf Großbuchstaben ohne Leerraum.
    /// </summary>
    /// <param name="hex">Der Hex-Code.</param>
    /// <returns>Der vereinheitlichte Hex-Code.</returns>
    public static string Normalize(string hex) => hex.Trim().ToUpperInvariant();

    /// <summary>
    /// Wählt dunkle oder helle Schrift, damit Text auf der Farbe lesbar bleibt.
    /// </summary>
    /// <param name="hex">Die Hintergrundfarbe der Schrift.</param>
    /// <returns>Die Schriftfarbe.</returns>
    public static string ForegroundFor(string hex) => RelativeLuminance(hex) > 0.45 ? DarkText : LightText;

    /// <summary>
    /// Prüft, ob sich eine Farbe zu wenig vom Hintergrund abhebt und einen Rahmen braucht.
    /// </summary>
    /// <param name="accentHex">Die Farbe.</param>
    /// <param name="backgroundHex">Der Hintergrund.</param>
    /// <returns><c>true</c> bei zu geringem Kontrast.</returns>
    public static bool NeedsOutline(string accentHex, string backgroundHex) =>
        ContrastRatio(accentHex, backgroundHex) < MinContrastRatio;

    /// <summary>
    /// Sattere Variante einer Farbe für Rahmen (z. B. Goldgelb zu Gelb): schrittweise zu Schwarz (helles Theme)
    /// bzw. Weiß (Dark Theme) gemischt, bis der Kontrast zum Hintergrund mindestens 2 erreicht.
    /// </summary>
    /// <param name="accentHex">Die Farbe.</param>
    /// <param name="backgroundHex">Der Hintergrund.</param>
    /// <param name="isDarkTheme">Ob das Dark Theme aktiv ist.</param>
    /// <returns>Die Rahmenfarbe, oder <c>null</c> für neutrale Farben (dann gilt die Textfarbe).</returns>
    public static string? OutlineColor(string accentHex, string backgroundHex, bool isDarkTheme)
    {
        if (HsvSaturation(accentHex) < NeutralSaturation)
        {
            return null;
        }

        var target = isDarkTheme ? "#FFFFFF" : "#000000";
        for (var step = 1; step * MixStep <= 1 + 1e-9; step++)
        {
            var candidate = Mix(accentHex, target, step * MixStep);
            if (ContrastRatio(candidate, backgroundHex) >= OutlineContrastRatio)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Mischt zwei Farben.
    /// </summary>
    /// <param name="fromHex">Ausgangsfarbe.</param>
    /// <param name="toHex">Zielfarbe.</param>
    /// <param name="amount">Anteil der Zielfarbe (0–1).</param>
    /// <returns>Die Mischfarbe.</returns>
    public static string Mix(string fromHex, string toHex, double amount)
    {
        var from = ToRgb(fromHex);
        var to = ToRgb(toHex);
        return ToHex(from.Select((channel, i) => (int)Math.Round(channel + (to[i] - channel) * amount, MidpointRounding.AwayFromZero)).ToArray());
    }

    /// <summary>
    /// Kontrastverhältnis zweier Farben nach WCAG (1 bis 21).
    /// </summary>
    /// <param name="firstHex">Erste Farbe.</param>
    /// <param name="secondHex">Zweite Farbe.</param>
    /// <returns>Das Kontrastverhältnis.</returns>
    public static double ContrastRatio(string firstHex, string secondHex)
    {
        var first = RelativeLuminance(firstHex);
        var second = RelativeLuminance(secondHex);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    /// <summary>
    /// Nimmt eine eigene Farbe in die benutzerdefinierte Liste auf: keine Farben aus den festen Reihen,
    /// keine Duplikate, höchstens 10 – bei mehr fällt die älteste weg.
    /// </summary>
    /// <param name="customColors">Bisherige Liste.</param>
    /// <param name="hex">Neue Farbe.</param>
    /// <param name="saturationPercent">Aktuelle Pastell-Sättigung (für den Vergleich mit der Pastell-Reihe).</param>
    /// <returns>Die neue Liste.</returns>
    public static IReadOnlyList<string> AddCustomColor(IReadOnlyList<string> customColors, string hex, int saturationPercent)
    {
        var color = Normalize(hex);
        var fixedColors = Pastel(saturationPercent).Concat(Strong).Concat(Neutral).Select(swatch => swatch.Hex);
        if (fixedColors.Contains(color) || customColors.Contains(color))
        {
            return customColors;
        }

        return customColors.Append(color).TakeLast(MaxCustomColors).ToList();
    }

    /// <summary>
    /// Relative Leuchtdichte nach WCAG.
    /// </summary>
    /// <param name="hex">Die Farbe.</param>
    /// <returns>Leuchtdichte zwischen 0 und 1.</returns>
    public static double RelativeLuminance(string hex)
    {
        var channels = ToRgb(hex).Select(value => value / 255.0)
            .Select(c => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4)).ToArray();
        return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
    }

    /// <summary>
    /// Zerlegt einen Hex-Code in Rot, Grün, Blau.
    /// </summary>
    /// <param name="hex">Der Hex-Code.</param>
    /// <returns>Die Kanäle (0–255).</returns>
    private static int[] ToRgb(string hex)
    {
        var value = hex.Trim().TrimStart('#');
        return [.. Enumerable.Range(0, 3).Select(i => int.Parse(value.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture))];
    }

    /// <summary>
    /// Setzt einen Hex-Code aus Kanälen zusammen.
    /// </summary>
    /// <param name="rgb">Rot, Grün, Blau (0–255).</param>
    /// <returns>Der Hex-Code.</returns>
    private static string ToHex(int[] rgb) => "#" + string.Concat(rgb.Select(c => Math.Clamp(c, 0, 255).ToString("X2", CultureInfo.InvariantCulture)));

    /// <summary>
    /// Format <c>#RRGGBB</c>.
    /// </summary>
    /// <returns>Der reguläre Ausdruck.</returns>
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexPattern();
}
