using System.Globalization;

namespace PdfUtopia.Core.Compression;

/// <summary>
/// Dateigrößen für die Anzeige: ab 1 MB mit einer Nachkommastelle in MB, darunter in ganzen KB (mindestens 1 KB).
/// </summary>
public static class FileSize
{
    private const double Kilobyte = 1024;
    private const double Megabyte = 1024 * 1024;

    /// <summary>
    /// Formatiert eine Größe, z. B. „2,4 MB“ oder „590 KB“.
    /// </summary>
    /// <param name="bytes">Größe in Bytes.</param>
    /// <param name="culture">Kultur für das Dezimaltrennzeichen.</param>
    /// <returns>Der Text.</returns>
    public static string Format(long bytes, CultureInfo culture) => bytes >= Megabyte
        ? (bytes / Megabyte).ToString("0.0", culture) + " MB"
        : Math.Max(1, (long)Math.Round(bytes / Kilobyte)).ToString(culture) + " KB";
}
