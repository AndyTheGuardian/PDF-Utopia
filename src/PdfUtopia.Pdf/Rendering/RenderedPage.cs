namespace PdfUtopia.Pdf.Rendering;

/// <summary>
/// Gerenderte Seite als Pixel im Format BGRA (8 Bit je Kanal, vormultipliziertes Alpha), Zeile für Zeile.
/// </summary>
/// <param name="Width">Breite in Pixeln.</param>
/// <param name="Height">Höhe in Pixeln.</param>
/// <param name="Stride">Bytes je Zeile.</param>
/// <param name="Pixels">Die Pixeldaten (<paramref name="Stride"/> × <paramref name="Height"/> Bytes).</param>
public sealed record RenderedPage(int Width, int Height, int Stride, byte[] Pixels)
{
    /// <summary>
    /// Liest ein Pixel.
    /// </summary>
    /// <param name="x">Spalte.</param>
    /// <param name="y">Zeile.</param>
    /// <returns>Rot, Grün, Blau.</returns>
    public (byte R, byte G, byte B) PixelAt(int x, int y)
    {
        var offset = y * Stride + x * 4;
        return (Pixels[offset + 2], Pixels[offset + 1], Pixels[offset]);
    }
}
