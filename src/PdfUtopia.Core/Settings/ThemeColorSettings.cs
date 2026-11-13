using System.Text.Json.Serialization;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.Core.Settings;

/// <summary>
/// Farben eines Designs: Akzentfarbe, Sättigung der Pastell-Reihe, Farbstich und Helligkeit des Hintergrunds.
/// Hell und Dunkel haben je einen eigenen Satz (A56); die eigenen Farben gelten für beide.
/// </summary>
public sealed record ThemeColorSettings
{
    /// <summary>Akzentfarbe; <c>null</c> = Standard des Designs.</summary>
    public string? AccentColor { get; init; }

    /// <summary>Sättigung der Pastell-Reihe in Prozent (10–90); gilt für alle Pastellfarben zugleich (A56).</summary>
    public int PastelSaturation { get; init; } = AccentPalette.DefaultSaturation;

    /// <summary>Farbton des Farbstichs in Grad (A56).</summary>
    public int TintHue { get; init; } = ThemeTint.Neutral.Hue;

    /// <summary>Stärke des Farbstichs in Prozent; 0 = neutral (A56).</summary>
    public int TintSaturation { get; init; }

    /// <summary>Helligkeit in Prozentpunkten, −25 bis +25 (A56).</summary>
    public int Brightness { get; init; }

    /// <summary>Farbstich und Helligkeit als ein Wert.</summary>
    [JsonIgnore]
    public ThemeTint Tint => new ThemeTint(TintHue, TintSaturation, Brightness).Normalized();

    /// <summary>Ersetzt ungültige Werte durch gültige.</summary>
    /// <returns>Die bereinigten Farben.</returns>
    public ThemeColorSettings Normalized()
    {
        var tint = Tint;
        return this with
        {
            AccentColor = AccentPalette.IsValidHex(AccentColor) ? AccentPalette.Normalize(AccentColor!) : null,
            PastelSaturation = AccentPalette.ClampSaturation(PastelSaturation),
            TintHue = tint.Hue,
            TintSaturation = tint.Saturation,
            Brightness = tint.Brightness,
        };
    }
}
