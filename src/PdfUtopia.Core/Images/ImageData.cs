namespace PdfUtopia.Core.Images;

/// <summary>
/// Wie die Bildpunkte eines eingefügten Bildes vorliegen.
/// </summary>
public enum ImageEncoding
{
    /// <summary>JPEG-Datei, unverändert eingebettet.</summary>
    Jpeg,

    /// <summary>Unkomprimierte Bildpunkte: je Punkt Rot, Grün, Blau (8 Bit), optional eine Deckkraftebene.</summary>
    Rgb,
}

/// <summary>
/// Bilddaten eines eingefügten Bildes (A51). Unveränderlich; mehrere Anmerkungen können dieselben Daten teilen.
/// </summary>
/// <param name="PixelWidth">Breite in Bildpunkten.</param>
/// <param name="PixelHeight">Höhe in Bildpunkten.</param>
/// <param name="Encoding">Art der Daten.</param>
/// <param name="Data">JPEG-Datei bzw. Bildpunkte (3 Byte je Punkt, zeilenweise von oben).</param>
/// <param name="Alpha">Deckkraft je Punkt (1 Byte) oder <c>null</c>, wenn das Bild deckend ist.</param>
public sealed record ImageData(int PixelWidth, int PixelHeight, ImageEncoding Encoding, byte[] Data, byte[]? Alpha = null)
{
    /// <summary>Größte Kantenlänge in Bildpunkten; größere Bilder werden beim Einfügen verkleinert (F36).</summary>
    public const int MaxPixels = 2000;

    /// <summary>Seitenverhältnis Höhe zu Breite.</summary>
    public double AspectRatio => (double)PixelHeight / PixelWidth;

    /// <summary>
    /// Größe nach dem Verkleinern auf höchstens <see cref="MaxPixels"/> an der längsten Kante.
    /// </summary>
    /// <param name="width">Breite.</param>
    /// <param name="height">Höhe.</param>
    /// <returns>Die Größe; unverändert, wenn das Bild klein genug ist.</returns>
    public static (int Width, int Height) FitWithinLimit(int width, int height)
    {
        var longest = Math.Max(width, height);
        if (longest <= MaxPixels)
        {
            return (width, height);
        }

        var scale = (double)MaxPixels / longest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>
    /// Erzeugt Bilddaten aus Bildpunkten mit Rot, Grün, Blau und Deckkraft (4 Byte je Punkt, nicht vormultipliziert).
    /// Ist jeder Punkt deckend, entfällt die Deckkraftebene.
    /// </summary>
    /// <param name="width">Breite.</param>
    /// <param name="height">Höhe.</param>
    /// <param name="rgba">Die Bildpunkte.</param>
    /// <returns>Die Bilddaten.</returns>
    public static ImageData FromRgba(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var count = width * height;
        var rgb = new byte[count * 3];
        var alpha = new byte[count];
        var opaque = true;
        for (var i = 0; i < count; i++)
        {
            rgb[i * 3] = rgba[i * 4];
            rgb[i * 3 + 1] = rgba[i * 4 + 1];
            rgb[i * 3 + 2] = rgba[i * 4 + 2];
            alpha[i] = rgba[i * 4 + 3];
            opaque &= alpha[i] == 255;
        }

        return new ImageData(width, height, ImageEncoding.Rgb, rgb, opaque ? null : alpha);
    }
}

/// <summary>
/// Liest Größe und Farbkanäle einer JPEG-Datei aus ihrem Kopf.
/// </summary>
public static class JpegInfo
{
    /// <summary>
    /// Liest die Angaben des ersten Bildrahmens (SOF).
    /// </summary>
    /// <param name="data">Die Datei.</param>
    /// <returns>Breite, Höhe und Zahl der Farbkanäle oder <c>null</c>, wenn es keine lesbare JPEG-Datei ist.</returns>
    public static (int Width, int Height, int Components)? Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return null;
        }

        var position = 2;
        while (position + 4 <= data.Length)
        {
            if (data[position] != 0xFF)
            {
                return null;
            }

            var marker = data[position + 1];
            var length = (data[position + 2] << 8) | data[position + 3];
            // SOF0–SOF15 außer DHT (C4), JPG (C8) und DAC (CC)
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (position + 10 > data.Length)
                {
                    return null;
                }

                var height = (data[position + 5] << 8) | data[position + 6];
                var width = (data[position + 7] << 8) | data[position + 8];
                return (width, height, data[position + 9]);
            }

            position += 2 + length;
        }

        return null;
    }
}
