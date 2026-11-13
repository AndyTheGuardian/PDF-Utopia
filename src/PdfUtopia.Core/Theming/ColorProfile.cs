using PdfUtopia.Core.Settings;

namespace PdfUtopia.Core.Theming;

/// <summary>
/// Benanntes Farbprofil für ein Design (A56): Akzentfarbe, Pastell-Sättigung, Farbstich und Helligkeit des Hintergrunds.
/// </summary>
/// <param name="Name">
/// Anzeigename; bei mitgelieferten Profilen ein Schlüssel der Oberflächentexte (z. B. <c>Profile.Sage</c>).
/// </param>
/// <param name="IsDark">Ob das Profil zum dunklen Design gehört.</param>
/// <param name="Colors">Die Farben.</param>
public sealed record ColorProfile(string Name, bool IsDark, ThemeColorSettings Colors)
{
    /// <summary>Höchstzahl eigener Profile.</summary>
    public const int MaxCustomProfiles = 50;

    /// <summary>Größte Länge eines Namens.</summary>
    public const int MaxNameLength = 60;

    /// <summary>Ob das Profil mitgeliefert und damit schreibgeschützt ist (A56).</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// Ob die Farben dieselbe Darstellung ergeben wie <paramref name="other"/>. Der Farbton des Hintergrunds zählt nur,
    /// wenn es einen Farbstich gibt; eine fehlende Akzentfarbe entspricht der Standardfarbe des Designs.
    /// </summary>
    /// <param name="other">Die zu vergleichenden Farben.</param>
    /// <returns><c>true</c> bei gleicher Darstellung.</returns>
    public bool Matches(ThemeColorSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var mine = Colors.Normalized();
        var theirs = other.Normalized();
        return EffectiveAccent(mine) == EffectiveAccent(theirs)
               && mine.PastelSaturation == theirs.PastelSaturation
               && mine.TintSaturation == theirs.TintSaturation
               && (mine.TintSaturation == 0 || mine.TintHue == theirs.TintHue)
               && mine.Brightness == theirs.Brightness;
    }

    /// <summary>Wirksame Akzentfarbe: die gespeicherte oder die Standardfarbe des Designs.</summary>
    /// <returns>Die Farbe als <c>#RRGGBB</c>.</returns>
    public string EffectiveAccent() => EffectiveAccent(Colors);

    /// <summary>Farbe des Hintergrunds als Hex-Code, wie sie das Farbfeld der Einstellungen zeigt.</summary>
    /// <returns>Die Farbe als <c>#RRGGBB</c>; ohne Farbstich grau.</returns>
    public string BackgroundHex() => ThemePalette.TintColor(Colors.TintHue, Colors.TintSaturation);

    /// <summary>Kürzt und trimmt einen Profilnamen.</summary>
    /// <param name="name">Der eingegebene Name.</param>
    /// <returns>Der bereinigte Name; leer, wenn nichts übrig bleibt.</returns>
    public static string CleanName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength].TrimEnd();
    }

    /// <summary>
    /// Liefert einen freien Namen: den gewünschten oder mit angehängter Nummer, z. B. „Salbei (2)“. Groß- und
    /// Kleinschreibung zählen nicht.
    /// </summary>
    /// <param name="taken">Belegte Namen.</param>
    /// <param name="name">Gewünschter Name.</param>
    /// <returns>Ein freier Name.</returns>
    public static string UniqueName(IEnumerable<string> taken, string name)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var clean = CleanName(name);
        if (!used.Contains(clean))
        {
            return clean;
        }

        for (var number = 2; ; number++)
        {
            var suffix = $" ({number})";
            var candidate = CleanName(clean[..Math.Min(clean.Length, MaxNameLength - suffix.Length)]) + suffix;
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Bereinigt eigene Profile: ohne Namen oder mitgeliefert entfallen, Farben werden gültig gemacht, doppelte Namen
    /// je Design entfallen (der erste bleibt), höchstens <see cref="MaxCustomProfiles"/>.
    /// </summary>
    /// <param name="profiles">Die gespeicherten Profile.</param>
    /// <returns>Die bereinigten Profile.</returns>
    public static IReadOnlyList<ColorProfile> NormalizeCustom(IEnumerable<ColorProfile?>? profiles)
    {
        var result = new List<ColorProfile>();
        var seen = new HashSet<(bool, string)>();
        foreach (var profile in profiles ?? [])
        {
            var name = CleanName(profile?.Name);
            if (profile is null || name.Length == 0 || !seen.Add((profile.IsDark, name.ToUpperInvariant())))
            {
                continue;
            }

            result.Add(profile with { Name = name, IsBuiltIn = false, Colors = (profile.Colors ?? new()).Normalized() });
            if (result.Count == MaxCustomProfiles)
            {
                break;
            }
        }

        return result;
    }

    private string EffectiveAccent(ThemeColorSettings colors) =>
        colors.AccentColor ?? (IsDark ? AccentPalette.DefaultAccentDark : AccentPalette.DefaultAccent);
}

/// <summary>
/// Mitgelieferte Farbprofile (A56): je Design „Standard“ (die bisherigen Farben von PDF Utopia, Startprofil),
/// „Neutral“ (neutrale Akzentfarbe) und fünf abgestimmte Profile wie im AI Asset Manager.
/// </summary>
public static class BuiltInColorProfiles
{
    /// <summary>Profile des hellen Designs.</summary>
    public static IReadOnlyList<ColorProfile> Light { get; } =
    [
        Standard(dark: false),
        Neutral(dark: false),
        Create("Profile.Sage", dark: false, "#779E88", "#70A15E", 0),
        Create("Profile.SandTerracotta", dark: false, "#B8613F", "#B59A6E", 2),
        Create("Profile.Fjord", dark: false, "#3C7DA6", "#6E92B8", 0),
        Create("Profile.LavenderPlum", dark: false, "#875A9C", "#9C86B8", 0),
        Create("Profile.GraphiteCoral", dark: false, "#D9604F", "#808080", 0),
    ];

    /// <summary>Profile des dunklen Designs.</summary>
    public static IReadOnlyList<ColorProfile> Dark { get; } =
    [
        Standard(dark: true),
        Neutral(dark: true),
        Create("Profile.Sage", dark: true, "#779E88", "#70A15E", 0),
        Create("Profile.Midnight", dark: true, "#6FA8DC", "#4F73A8", 3),
        Create("Profile.Amber", dark: true, "#E0A458", "#94704A", 2),
        Create("Profile.Aubergine", dark: true, "#C58BD8", "#7E5594", 2),
        Create("Profile.PetrolCopper", dark: true, "#D9895A", "#3F8282", 2),
    ];

    /// <summary>Die mitgelieferten Profile eines Designs.</summary>
    /// <param name="dark">Ob das dunkle Design gemeint ist.</param>
    /// <returns>Die Profile, „Standard“ zuerst.</returns>
    public static IReadOnlyList<ColorProfile> For(bool dark) => dark ? Dark : Light;

    /// <summary>„Standard“: Akzentfarbe Indigo des Designs, kein Farbstich – so sah PDF Utopia bisher aus.</summary>
    private static ColorProfile Standard(bool dark) => new("Profile.Standard", dark, new ThemeColorSettings()) { IsBuiltIn = true };

    /// <summary>Helligkeit des Hintergrunds der Profile „Neutral“.</summary>
    public const int NeutralBrightness = 8;

    /// <summary>
    /// „Neutral“: neutrale Akzentfarbe (hell 95 % Schwarz, dunkel 95 % Weiß), kein Farbstich, Helligkeit
    /// <see cref="NeutralBrightness"/>.
    /// </summary>
    private static ColorProfile Neutral(bool dark) => new(
        "Profile.Neutral",
        dark,
        new ThemeColorSettings { AccentColor = AccentPalette.Neutral[dark ? 1 : 0].Hex, Brightness = NeutralBrightness }.Normalized())
    {
        IsBuiltIn = true,
    };

    private static ColorProfile Create(string nameKey, bool dark, string accent, string background, int brightness)
    {
        var (hue, saturation) = ThemePalette.TintFromColor(background);
        var colors = new ThemeColorSettings
        {
            AccentColor = accent,
            TintHue = hue,
            TintSaturation = saturation,
            Brightness = brightness,
        };
        return new ColorProfile(nameKey, dark, colors.Normalized()) { IsBuiltIn = true };
    }
}
