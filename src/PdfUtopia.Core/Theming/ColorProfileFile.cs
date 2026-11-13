using System.Text.Json;
using System.Text.Json.Nodes;
using PdfUtopia.Core.Settings;

namespace PdfUtopia.Core.Theming;

/// <summary>Die Datei ist keine gültige Farbprofil-Datei.</summary>
public sealed class ColorProfileFormatException : Exception
{
    /// <summary>Erzeugt die Ausnahme.</summary>
    public ColorProfileFormatException()
    {
    }

    /// <summary>Erzeugt die Ausnahme mit einer Meldung.</summary>
    /// <param name="message">Die Meldung.</param>
    public ColorProfileFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Erzeugt die Ausnahme mit Meldung und Ursache.</summary>
    /// <param name="message">Die Meldung.</param>
    /// <param name="innerException">Die Ursache.</param>
    public ColorProfileFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Ergebnis beim Lesen einer Farbprofil-Datei.</summary>
/// <param name="Profiles">Die gültigen Profile.</param>
/// <param name="Skipped">Zahl der übersprungenen, ungültigen Einträge.</param>
public sealed record ColorProfileReadResult(IReadOnlyList<ColorProfile> Profiles, int Skipped);

/// <summary>
/// Austauschformat für Farbprofile (A56), gemeinsam mit dem AI Asset Manager und File Renamer: JSON mit Formatkennung und
/// Version, Farben als Hex-Code. Programme ohne Farbstich übernehmen nur Akzentfarbe und Pastell-Sättigung.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "format": "color-profiles",
///   "version": 1,
///   "profiles": [
///     { "name": "Salbei", "theme": "light", "accent": "#779E88", "background": "#70A15E", "brightness": 0, "pastelSaturation": 30 }
///   ]
/// }
/// </code>
/// „background“ ist die Farbe des Farbstichs (Farbton und Stärke als HSL bei 50 % Helligkeit), Grau bedeutet neutral.
/// „brightness“ verschiebt die Helligkeit der Flächen in Prozentpunkten (−25 bis +25).
/// </remarks>
public static class ColorProfileFile
{
    /// <summary>Formatkennung.</summary>
    public const string Format = "color-profiles";

    /// <summary>Aktuelle Version des Formats.</summary>
    public const int Version = 1;

    /// <summary>Empfohlene Dateiendung.</summary>
    public const string Extension = ".colorprofiles.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Erzeugt den Inhalt einer Farbprofil-Datei.</summary>
    /// <param name="profiles">Die Profile mit ihren Anzeigenamen (mitgelieferte bereits übersetzt).</param>
    /// <returns>Der JSON-Text.</returns>
    public static string Write(IEnumerable<ColorProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var list = new JsonArray();
        foreach (var profile in profiles)
        {
            var colors = profile.Colors.Normalized();
            list.Add(new JsonObject
            {
                ["name"] = profile.Name,
                ["theme"] = profile.IsDark ? "dark" : "light",
                ["accent"] = profile.EffectiveAccent(),
                ["background"] = profile.BackgroundHex(),
                ["brightness"] = colors.Brightness,
                ["pastelSaturation"] = colors.PastelSaturation,
            });
        }

        var root = new JsonObject { ["format"] = Format, ["version"] = Version, ["profiles"] = list };
        return root.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Liest eine Farbprofil-Datei. Einträge ohne Namen, mit unbekanntem Design oder ungültiger Farbe werden
    /// übersprungen; fehlende Zahlen bekommen Standardwerte.
    /// </summary>
    /// <param name="json">Der Inhalt der Datei.</param>
    /// <returns>Die gültigen Profile und die Zahl der übersprungenen.</returns>
    /// <exception cref="ColorProfileFormatException">Kein JSON, falsche Formatkennung oder neuere Version.</exception>
    public static ColorProfileReadResult Read(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new ColorProfileFormatException("Die Datei ist kein gültiges JSON.", exception);
        }

        if (root is not JsonObject document || Text(document, "format") != Format)
        {
            throw new ColorProfileFormatException("Die Datei enthält keine Farbprofile.");
        }

        if (Number(document, "version") is not { } version || version > Version)
        {
            throw new ColorProfileFormatException("Die Version der Datei wird nicht unterstützt.");
        }

        var profiles = new List<ColorProfile>();
        var skipped = 0;
        foreach (var node in document["profiles"] as JsonArray ?? [])
        {
            if (node is JsonObject entry && Parse(entry) is { } profile)
            {
                profiles.Add(profile);
            }
            else
            {
                skipped++;
            }
        }

        return new ColorProfileReadResult(profiles, skipped);
    }

    private static ColorProfile? Parse(JsonObject entry)
    {
        var name = ColorProfile.CleanName(Text(entry, "name"));
        bool? dark = Text(entry, "theme") switch
        {
            "dark" => true,
            "light" => false,
            _ => null,
        };
        var accent = Text(entry, "accent");
        var background = Text(entry, "background") ?? "#808080";
        if (name.Length == 0 || dark is null || !AccentPalette.IsValidHex(accent) || !AccentPalette.IsValidHex(background))
        {
            return null;
        }

        var (hue, saturation) = ThemePalette.TintFromColor(AccentPalette.Normalize(background));
        var colors = new ThemeColorSettings
        {
            AccentColor = accent,
            PastelSaturation = Number(entry, "pastelSaturation") ?? AccentPalette.DefaultSaturation,
            TintHue = hue,
            TintSaturation = saturation,
            Brightness = Number(entry, "brightness") ?? 0,
        };
        return new ColorProfile(name, dark.Value, colors.Normalized());
    }

    private static string? Text(JsonObject node, string key) =>
        node[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int? Number(JsonObject node, string key) =>
        node[key] is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number)
            ? (int)Math.Round(Math.Clamp(number, int.MinValue, int.MaxValue))
            : null;
}
