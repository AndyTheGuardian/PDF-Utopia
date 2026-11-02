using PDFiumCore;

namespace PdfUtopia.Pdf.Rendering;

/// <summary>
/// Gemeinsamer Zugang zu PDFium. PDFium ist nicht threadsicher; alle Aufrufe laufen deshalb unter <see cref="Sync"/>.
/// </summary>
internal static class PdfiumLibrary
{
    private static bool _initialized;

    /// <summary>Sperre für alle PDFium-Aufrufe.</summary>
    public static Lock Sync { get; } = new();

    /// <summary>
    /// Initialisiert PDFium beim ersten Aufruf. Muss unter <see cref="Sync"/> aufgerufen werden.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        fpdfview.FPDF_InitLibrary();
        _initialized = true;
    }

    /// <summary>
    /// Übersetzt den letzten PDFium-Fehlercode.
    /// </summary>
    /// <returns>Der Grund.</returns>
    public static PdfOpenError LastError() => fpdfview.FPDF_GetLastError() switch
    {
        2 => PdfOpenError.FileNotReadable,
        3 => PdfOpenError.InvalidFormat,
        4 => PdfOpenError.PasswordRequired,
        5 => PdfOpenError.UnsupportedSecurity,
        _ => PdfOpenError.Unknown,
    };
}
