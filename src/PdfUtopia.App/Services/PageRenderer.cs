using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.Services;

/// <summary>
/// Rendert Seiten eines Dokuments als Avalonia-Bitmaps und merkt sich die Ergebnisse je Quelldatei, Seite, Drehung
/// und Maßstab. Gerendert wird im Hintergrund, damit die Oberfläche bei großen Dateien flüssig bleibt.
/// </summary>
/// <param name="sourceOf">Liefert die Quelldatei zu <see cref="PageEntry.SourceIndex"/> (eingefügte Seiten, A41).</param>
public sealed class PageRenderer(Func<int, PdfSource> sourceOf) : IDisposable
{
    private const int MaxCachedPages = 24;

    private readonly Dictionary<(int Source, int Page, PageRotation Rotation, double Scale), Bitmap> _cache = [];
    private readonly LinkedList<(int, int, PageRotation, double)> _order = new();

    /// <summary>
    /// Rendert eine Seite.
    /// </summary>
    /// <param name="page">Die Seite.</param>
    /// <param name="scale">Pixel je Punkt.</param>
    /// <returns>Die Bitmap.</returns>
    public async Task<Bitmap> RenderAsync(PageEntry page, double scale)
    {
        var key = (page.SourceIndex, page.SourcePageNumber, page.Rotation, Math.Round(scale, 3));
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var source = sourceOf(page.SourceIndex);
        var rendered = await Task.Run(() => source.Render(page.SourcePageNumber, key.Item4, page.Rotation));
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
    private void Remember((int, int, PageRotation, double) key, Bitmap bitmap)
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
