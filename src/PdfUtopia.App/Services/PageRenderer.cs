using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.Services;

/// <summary>
/// Rendert Seiten einer Datei als Avalonia-Bitmaps und merkt sich die Ergebnisse je Seite, Drehung und
/// Maßstab. Gerendert wird im Hintergrund, damit die Oberfläche bei großen Dateien flüssig bleibt.
/// </summary>
/// <param name="source">Die geöffnete Datei.</param>
public sealed class PageRenderer(PdfSource source) : IDisposable
{
    private const int MaxCachedPages = 24;

    private readonly Dictionary<(int Page, PageRotation Rotation, double Scale), Bitmap> _cache = [];
    private readonly LinkedList<(int, PageRotation, double)> _order = new();

    /// <summary>
    /// Rendert eine Seite.
    /// </summary>
    /// <param name="sourcePageNumber">Seitennummer in der Datei.</param>
    /// <param name="rotation">Zusätzliche Drehung.</param>
    /// <param name="scale">Pixel je Punkt.</param>
    /// <returns>Die Bitmap.</returns>
    public async Task<Bitmap> RenderAsync(int sourcePageNumber, PageRotation rotation, double scale)
    {
        var key = (sourcePageNumber, rotation, Math.Round(scale, 3));
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var rendered = await Task.Run(() => source.Render(sourcePageNumber, key.Item3, rotation));
        var bitmap = ToBitmap(rendered);
        Remember(key, bitmap);
        return bitmap;
    }

    /// <summary>
    /// Wandelt gerenderte Pixel in eine Bitmap.
    /// </summary>
    /// <param name="page">Die Pixel (BGRA, vormultipliziert).</param>
    /// <returns>Die Bitmap.</returns>
    public static Bitmap ToBitmap(RenderedPage page)
    {
        var bitmap = new WriteableBitmap(new PixelSize(page.Width, page.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        for (var row = 0; row < page.Height; row++)
        {
            System.Runtime.InteropServices.Marshal.Copy(page.Pixels, row * page.Stride, buffer.Address + row * buffer.RowBytes, page.Width * 4);
        }

        return bitmap;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var bitmap in _cache.Values)
        {
            bitmap.Dispose();
        }

        _cache.Clear();
        _order.Clear();
    }

    /// <summary>
    /// Nimmt eine Bitmap in den Zwischenspeicher auf; die älteste fällt bei Überlauf weg.
    /// </summary>
    private void Remember((int, PageRotation, double) key, Bitmap bitmap)
    {
        _cache[key] = bitmap;
        _order.AddLast(key);
        while (_order.Count > MaxCachedPages && _order.First is { } oldest)
        {
            _order.RemoveFirst();
            if (_cache.Remove(oldest.Value, out var removed))
            {
                removed.Dispose();
            }
        }
    }
}
