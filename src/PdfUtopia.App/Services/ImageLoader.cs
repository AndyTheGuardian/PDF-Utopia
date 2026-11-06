using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PdfUtopia.Core.Images;

namespace PdfUtopia.App.Services;

/// <summary>
/// Ergebnis des Einlesens eines Bildes.
/// </summary>
/// <param name="Image">Die Bilddaten.</param>
/// <param name="OriginalWidth">Breite vor dem Verkleinern.</param>
/// <param name="OriginalHeight">Höhe vor dem Verkleinern.</param>
public sealed record LoadedImage(ImageData Image, int OriginalWidth, int OriginalHeight)
{
    /// <summary>Ob das Bild verkleinert wurde (F36).</summary>
    public bool WasShrunk => Image.PixelWidth != OriginalWidth || Image.PixelHeight != OriginalHeight;
}

/// <summary>
/// Liest Bilder für Stempel/Bild ein (A51) und hält die Anzeige-Bitmaps vor. JPEG-Dateien in erlaubter Größe bleiben
/// unverändert; alles andere wird in Bildpunkte gewandelt und bei Bedarf auf 2000 Pixel verkleinert (F36).
/// </summary>
internal static class ImageLoader
{
    /// <summary>Unterstützte Dateiendungen.</summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg"];

    private static readonly ConditionalWeakTable<ImageData, Bitmap> Bitmaps = new();

    /// <summary>
    /// Ob eine Datei nach ihrer Endung ein unterstütztes Bild ist.
    /// </summary>
    /// <param name="path">Pfad oder Dateiname.</param>
    /// <returns><c>true</c> für PNG und JPEG.</returns>
    public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Liest ein Bild aus dem Inhalt einer PNG- oder JPEG-Datei.
    /// </summary>
    /// <param name="bytes">Dateiinhalt.</param>
    /// <returns>Das Bild oder <c>null</c>, wenn es nicht lesbar ist.</returns>
    public static LoadedImage? FromBytes(byte[] bytes)
    {
        // JPEG in Graustufen oder RGB und in erlaubter Größe: unverändert übernehmen
        if (JpegInfo.Read(bytes) is { Components: 1 or 3 } jpeg && Math.Max(jpeg.Width, jpeg.Height) <= ImageData.MaxPixels && jpeg.Width > 0 && jpeg.Height > 0)
        {
            return new LoadedImage(new ImageData(jpeg.Width, jpeg.Height, ImageEncoding.Jpeg, bytes), jpeg.Width, jpeg.Height);
        }

        try
        {
            using var bitmap = new Bitmap(new MemoryStream(bytes));
            return FromBitmap(bitmap);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Wandelt eine Bitmap (z. B. aus der Zwischenablage) in Bilddaten, bei Bedarf verkleinert.
    /// </summary>
    /// <param name="bitmap">Die Bitmap.</param>
    /// <returns>Das Bild.</returns>
    public static LoadedImage FromBitmap(Bitmap bitmap)
    {
        var original = bitmap.PixelSize;
        var (width, height) = ImageData.FitWithinLimit(original.Width, original.Height);
        var scaled = width == original.Width && height == original.Height
            ? null
            : bitmap.CreateScaledBitmap(new PixelSize(width, height), BitmapInterpolationMode.HighQuality);
        try
        {
            using var target = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using var buffer = target.Lock();
            (scaled ?? bitmap).CopyPixels(buffer);
            var pixels = new byte[width * height * 4];
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(buffer.Address + row * buffer.RowBytes, pixels, row * width * 4, width * 4);
            }

            return new LoadedImage(ImageData.FromRgba(width, height, pixels), original.Width, original.Height);
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    /// <summary>
    /// Bitmap zur Anzeige von Bilddaten (einmal erzeugt, solange die Daten leben).
    /// </summary>
    /// <param name="image">Die Bilddaten.</param>
    /// <returns>Die Bitmap.</returns>
    public static Bitmap BitmapOf(ImageData image) => Bitmaps.GetValue(image, Create);

    /// <summary>
    /// Erzeugt die Anzeige-Bitmap.
    /// </summary>
    private static Bitmap Create(ImageData image)
    {
        if (image.Encoding == ImageEncoding.Jpeg)
        {
            return new Bitmap(new MemoryStream(image.Data));
        }

        var bitmap = new WriteableBitmap(new PixelSize(image.PixelWidth, image.PixelHeight), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var buffer = bitmap.Lock();
        var row = new byte[image.PixelWidth * 4];
        for (var y = 0; y < image.PixelHeight; y++)
        {
            for (var x = 0; x < image.PixelWidth; x++)
            {
                var pixel = y * image.PixelWidth + x;
                row[x * 4] = image.Data[pixel * 3];
                row[x * 4 + 1] = image.Data[pixel * 3 + 1];
                row[x * 4 + 2] = image.Data[pixel * 3 + 2];
                row[x * 4 + 3] = image.Alpha?[pixel] ?? 255;
            }

            Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
        }

        return bitmap;
    }
}
