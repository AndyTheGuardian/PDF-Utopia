using System.Globalization;

namespace PdfUtopia.Core.Theming;

/// <summary>
/// Farbstich und Helligkeit des Themes (A56). Ohne Sättigung und mit Helligkeit 0 gelten die
/// Grundfarben.
/// </summary>
/// <param name="Hue">Farbton in Grad (0–359).</param>
/// <param name="Saturation">Stärke des Farbstichs in Prozent (0 = neutral, 100 = kräftigster Stich).</param>
/// <param name="Brightness">Helligkeit in Prozentpunkten (−25 bis +25).</param>
public sealed record ThemeTint(int Hue, int Saturation, int Brightness)
{
    /// <summary>Kleinste Helligkeit.</summary>
    public const int MinBrightness = -25;

    /// <summary>Größte Helligkeit.</summary>
    public const int MaxBrightness = 25;

    /// <summary>Neutral: kein Farbstich, Grundfarben.</summary>
    public static ThemeTint Neutral { get; } = new(220, 0, 0);

    /// <summary>Begrenzt alle Werte auf den erlaubten Bereich.</summary>
    /// <returns>Die bereinigten Werte.</returns>
    public ThemeTint Normalized() => new(
        ((Hue % 360) + 360) % 360,
        Math.Clamp(Saturation, 0, 100),
        Math.Clamp(Brightness, MinBrightness, MaxBrightness));
}

/// <summary>
/// Flächen- und Textfarben eines Themes.
/// </summary>
/// <param name="AppBackground">Fensterhintergrund.</param>
/// <param name="Surface">Fläche von Leisten und Bereichen.</param>
/// <param name="Surface2">Zweite Fläche, z. B. Eingabefelder.</param>
/// <param name="Border">Rahmen und Trennlinien.</param>
/// <param name="Text">Text.</param>
/// <param name="MutedText">Nebentext.</param>
/// <param name="Hover">Hinterlegung unter dem Mauszeiger.</param>
/// <param name="Canvas">Hintergrund der Vorschau.</param>
public sealed record ThemeColors(
    string AppBackground,
    string Surface,
    string Surface2,
    string Border,
    string Text,
    string MutedText,
    string Hover,
    string Canvas);

/// <summary>
/// Berechnet die Theme-Farben aus neutralen Grundfarben (95 % Weiß bzw. 95 % Schwarz), einem Farbstich und der
/// Helligkeit. Mit der Helligkeit sind reines Weiß und reines Schwarz erreichbar (A56).
/// </summary>
public static class ThemePalette
{
    /// <summary>
    /// Farbstärke (Chroma, 0–1) bei Farbstich 100 %; darüber wirken Flächen bunt statt getönt. Gerechnet wird mit
    /// Chroma statt HSL-Sättigung, weil dieselbe Sättigung auf sehr hellen und sehr dunklen Flächen kaum sichtbar wäre.
    /// </summary>
    public const double MaxTintChroma = 0.08;

    /// <summary>
    /// Hellste erreichbare Fläche: reines Weiß. Standard ist 95 % Weiß (<see cref="LightBase"/>); mit der Helligkeit
    /// kann der Anwender bis 100 % gehen (A56).
    /// </summary>
    public const double MaxLightness = 1.0;

    /// <summary>
    /// Dunkelste erreichbare Fläche: reines Schwarz. Standard ist 95 % Schwarz (<see cref="DarkBase"/>); mit der
    /// Helligkeit kann der Anwender bis 100 % gehen (A56).
    /// </summary>
    public const double MinLightness = 0.0;

    /// <summary>
    /// Grundfarben des hellen Themes: die bisherigen Farben von PDF Utopia, damit ohne Farbstich alles aussieht wie
    /// vorher (A56).
    /// </summary>
    public static ThemeColors LightBase { get; } =
        new("#ECEEF2", "#F7F8FA", "#F1F2F5", "#DCDFE6", "#1D2330", "#6B7280", "#ECEEF3", "#E4E6EC");

    /// <summary>
    /// Grundfarben des dunklen Themes: die bisherigen Farben von PDF Utopia (A56).
    /// </summary>
    public static ThemeColors DarkBase { get; } =
        new("#181818", "#1F1F1F", "#2B2B2B", "#3A3A3A", "#E6E6E6", "#A0A0A0", "#303030", "#252525");

    /// <summary>
    /// Farben für ein Theme. Flächen, Rahmen und Hinterlegung bekommen Farbstich und Helligkeit; Texte nur den
    /// halben Farbstich und keine Helligkeitsänderung, damit sie lesbar bleiben.
    /// </summary>
    /// <param name="dark">Ob das dunkle Theme gilt.</param>
    /// <param name="tint">Farbstich und Helligkeit.</param>
    /// <returns>Die Farben.</returns>
    public static ThemeColors For(bool dark, ThemeTint tint)
    {
        ArgumentNullException.ThrowIfNull(tint);
        var t = tint.Normalized();
        var source = dark ? DarkBase : LightBase;
        if (t.Saturation == 0 && t.Brightness == 0)
        {
            return source;
        }

        string Area(string hex) => Adjust(hex, t, t.Saturation, t.Brightness);
        string Ink(string hex) => Adjust(hex, t, t.Saturation / 2, 0);

        return new ThemeColors(
            Area(source.AppBackground),
            Area(source.Surface),
            Area(source.Surface2),
            Area(source.Border),
            Ink(source.Text),
            Ink(source.MutedText),
            Area(source.Hover),
            Area(source.Canvas));
    }

    private static string Adjust(string hex, ThemeTint tint, int saturationPercent, int brightness)
    {
        var (hue, saturation, lightness) = ToHsl(hex);
        lightness = Math.Clamp(lightness + brightness / 100.0, MinLightness, MaxLightness);
        if (saturationPercent > 0)
        {
            hue = tint.Hue;
            var chroma = saturationPercent / 100.0 * MaxTintChroma;
            var maxChroma = 1 - Math.Abs(2 * lightness - 1);
            saturation = maxChroma <= 0 ? 0 : Math.Min(1, chroma / maxChroma);
        }

        return FromHsl(hue, saturation, lightness);
    }

    /// <summary>Wandelt <c>#RRGGBB</c> in Farbton (Grad), Sättigung und Helligkeit (0–1).</summary>
    public static (double Hue, double Saturation, double Lightness) ToHsl(string hex)
    {
        var value = hex.Trim().TrimStart('#');
        var r = int.Parse(value.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(value.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(value.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var delta = max - min;
        if (delta == 0)
        {
            return (0, 0, lightness);
        }

        var saturation = delta / (1 - Math.Abs(2 * lightness - 1));
        double hue;
        if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }

        return ((hue + 360) % 360, saturation, lightness);
    }

    /// <summary>Farbe eines Farbstichs zum Anzeigen und als Hex-Code: Farbton und Stärke als HSL-Sättigung, Helligkeit 50 %.</summary>
    /// <param name="hue">Farbton in Grad.</param>
    /// <param name="saturationPercent">Stärke in Prozent.</param>
    /// <returns>Die Farbe als <c>#RRGGBB</c>.</returns>
    public static string TintColor(int hue, int saturationPercent) =>
        FromHsl(((hue % 360) + 360) % 360, Math.Clamp(saturationPercent, 0, 100) / 100.0, 0.5);

    /// <summary>Umkehrung von <see cref="TintColor"/>: Farbton und Stärke aus einem Hex-Code (A56).</summary>
    /// <param name="hex">Die Farbe als <c>#RRGGBB</c>.</param>
    /// <returns>Farbton (0–359) und Stärke (0–100).</returns>
    public static (int Hue, int Saturation) TintFromColor(string hex)
    {
        var (hue, saturation, _) = ToHsl(hex);
        return ((int)Math.Round(hue) % 360, (int)Math.Round(saturation * 100));
    }

    /// <summary>Farbstärke einer Farbe (Chroma, 0–1): Abstand zwischen hellstem und dunkelstem Kanal.</summary>
    /// <param name="hex">Die Farbe.</param>
    /// <returns>Chroma.</returns>
    public static double Chroma(string hex)
    {
        var (_, saturation, lightness) = ToHsl(hex);
        return saturation * (1 - Math.Abs(2 * lightness - 1));
    }

    /// <summary>Wandelt Farbton (Grad), Sättigung und Helligkeit (0–1) in <c>#RRGGBB</c>.</summary>
    public static string FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = lightness - chroma / 2;
        var (r, g, b) = (hue % 360) switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };

        static string Channel(double value) =>
            Math.Clamp((int)Math.Round(value * 255, MidpointRounding.AwayFromZero), 0, 255).ToString("X2", CultureInfo.InvariantCulture);

        return "#" + Channel(r + m) + Channel(g + m) + Channel(b + m);
    }
}
