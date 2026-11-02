namespace PdfUtopia.Pdf;

/// <summary>
/// Grund, warum eine PDF-Datei nicht geöffnet werden konnte.
/// </summary>
public enum PdfOpenError
{
    /// <summary>Unbekannter Fehler.</summary>
    Unknown,

    /// <summary>Datei nicht gefunden oder nicht lesbar.</summary>
    FileNotReadable,

    /// <summary>Keine gültige PDF-Datei oder beschädigt.</summary>
    InvalidFormat,

    /// <summary>Passwort erforderlich oder falsch.</summary>
    PasswordRequired,

    /// <summary>Nicht unterstütztes Sicherheitsverfahren.</summary>
    UnsupportedSecurity,
}

/// <summary>
/// Fehler beim Öffnen einer PDF-Datei; die Oberfläche zeigt je nach <see cref="Error"/> eine passende Meldung
/// oder fragt nach dem Passwort.
/// </summary>
/// <param name="error">Der Grund.</param>
/// <param name="path">Die Datei.</param>
public sealed class PdfOpenException(PdfOpenError error, string path)
    : Exception($"Die PDF-Datei „{path}“ konnte nicht geöffnet werden ({error}).")
{
    /// <summary>Der Grund.</summary>
    public PdfOpenError Error { get; } = error;

    /// <summary>Die Datei.</summary>
    public string FilePath { get; } = path;
}
