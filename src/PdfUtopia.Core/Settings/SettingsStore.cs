using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfUtopia.Core.Settings;

/// <summary>
/// Liest und schreibt die Einstellungen als JSON-Datei. Eine fehlende oder beschädigte Datei ergibt die
/// Standardwerte; geschrieben wird über eine temporäre Datei, damit ein Abbruch die alte Datei nicht zerstört.
/// </summary>
/// <param name="path">Pfad der Einstellungsdatei.</param>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Pfad der Einstellungsdatei.</summary>
    public string FilePath { get; } = path;

    /// <summary>
    /// Plattformüblicher Ablageort: Windows <c>%APPDATA%\PDF Utopia</c>, Linux <c>~/.config/PDF Utopia</c>,
    /// macOS <c>~/Library/Application Support/PDF Utopia</c>.
    /// </summary>
    /// <returns>Der Pfad der Einstellungsdatei.</returns>
    public static string DefaultPath()
    {
        var folder = OperatingSystem.IsMacOS() ? Environment.SpecialFolder.LocalApplicationData : Environment.SpecialFolder.ApplicationData;
        return Path.Combine(Environment.GetFolderPath(folder), "PDF Utopia", "settings.json");
    }

    /// <summary>
    /// Lädt die Einstellungen.
    /// </summary>
    /// <returns>Die bereinigten Einstellungen oder die Standardwerte.</returns>
    public AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings()).Normalized()
                : new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Speichert die Einstellungen.
    /// </summary>
    /// <param name="settings">Die Einstellungen.</param>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalized(), Options));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
