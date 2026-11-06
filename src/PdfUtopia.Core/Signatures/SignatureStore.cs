using System.Text.Json;

namespace PdfUtopia.Core.Signatures;

/// <summary>
/// Speichert bis zu fünf Unterschriften als Vektorlinien lokal neben den Einstellungen (A49, F31).
/// </summary>
/// <param name="path">Pfad der Datei.</param>
public sealed class SignatureStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Pfad der Datei.</summary>
    public string FilePath { get; } = path;

    /// <summary>
    /// Standardpfad: <c>signatures.json</c> im Ordner der Einstellungen.
    /// </summary>
    /// <param name="settingsPath">Pfad der Einstellungsdatei.</param>
    /// <returns>Der Pfad.</returns>
    public static string PathNextTo(string settingsPath) => Path.Combine(Path.GetDirectoryName(settingsPath)!, "signatures.json");

    /// <summary>
    /// Lädt die Unterschriften; fehlende oder beschädigte Dateien ergeben eine leere Liste.
    /// </summary>
    /// <returns>Höchstens fünf Unterschriften mit Strichen.</returns>
    public IReadOnlyList<Signature> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var signatures = JsonSerializer.Deserialize<List<Signature>>(File.ReadAllText(FilePath), Options) ?? [];
            return signatures.Where(signature => signature is { Strokes: not null } && signature.HasStrokes)
                .Select(Normalize).Take(Signature.MaxCount).ToList();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>
    /// Speichert die Unterschriften (höchstens fünf); erst in eine temporäre Datei, dann ersetzen.
    /// </summary>
    /// <param name="signatures">Die Unterschriften.</param>
    public void Save(IReadOnlyList<Signature> signatures)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(signatures.Take(Signature.MaxCount).ToList(), Options));
        File.Move(temporary, FilePath, overwrite: true);
    }

    /// <summary>Ersetzt eine unbekannte Farbe durch die Standardfarbe.</summary>
    private static Signature Normalize(Signature signature) =>
        Signature.Colors.Contains(signature.ColorHex) ? signature : signature with { ColorHex = Signature.Colors[0] };
}
