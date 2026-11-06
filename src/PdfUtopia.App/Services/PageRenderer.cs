using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.Services;

/// <summary>
/// Rendert Seiten eines Dokuments als Avalonia-Bitmaps und merkt sich die Ergebnisse je Quelldatei, Seite, Drehung
/// und Maßstab. Gerendert wird im Hintergrund, damit die Oberfläche bei großen Dateien flüssig bleibt. Angezeigte
/// Bitmaps werden festgehalten (<see cref="Pin"/>): Fallen sie aus dem Zwischenspeicher, werden sie erst freigegeben,
/// wenn keine Seite sie mehr zeigt (mehrere Seiten gleichzeitig, A55).
/// </summary>
/// <param name="sourceOf">Liefert die Quelldatei zu <see cref="PageEntry.SourceIndex"/> (eingefügte Seiten, A41).</param>
public sealed class PageRenderer(Func<int, PdfSource> sourceOf) : IDisposable
{
    private const int MaxCachedPages = 24;

    private readonly Dictionary<(int Source, int Page, PageRotation Rotation, double Scale), Bitmap> _cache = [];
    private readonly LinkedList<(int, int, PageRotation, double)> _order = new();
    private readonly Dictionary<Bitmap, int> _pinned = [];

    /// <summary>
    /// Rendert eine Seite.
    /// </summary>
    /// <param name="page">Die Seite.</param>
    /// <param name="scale">Pixel je Punkt.</param>
    /// <param name="drawForms"><c>false</c>: Formularfelder weglassen (die Seitenansicht zeigt sie als Eingabefelder).</param>
    /// <returns>Die Bitmap.</returns>
    public async Task<Bitmap> RenderAsync(PageEntry page, double scale, bool drawForms = true)
    {
        var key = (page.SourceIndex, page.SourcePageNumber, page.Rotation, Math.Round(scale, 3));
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var source = sourceOf(page.SourceIndex);
        var rendered = await Task.Run(() => source.Render(page.SourcePageNumber, key.Item4, page.Rotation, drawForms));
        var bitmap = ToBitmap(rendered);
        Remember(key, bitmap);
        return bitmap;
    }

    /// <summary>
    /// Hält eine angezeigte Bitmap fest, damit sie nicht freigegeben wird, solange eine Seite sie zeigt.
    /// </summary>
    /// <param name="bitmap">Die Bitmap.</param>
    public void Pin(Bitmap bitmap) => _pinned[bitmap] = _pinned.GetValueOrDefault(bitmap) + 1;

    /// <summary>
    /// Gibt eine festgehaltene Bitmap wieder frei; ist sie nicht mehr im Zwischenspeicher, wird sie entsorgt.
    /// </summary>
    /// <param name="bitmap">Die Bitmap.</param>
    public void Unpin(Bitmap bitmap)
    {
        if (!_pinned.TryGetValue(bitmap, out var count))
        {
            return;
        }

        if (count > 1)
        {
            _pinned[bitmap] = count - 1;
            return;
        }

        _pinned.Remove(bitmap);
        if (!_cache.ContainsValue(bitmap))
        {
            bitmap.Dispose();
        }
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
        foreach (var bitmap in _cache.Values.Concat(_pinned.Keys).Distinct())
        {
            bitmap.Dispose();
        }

        _cache.Clear();
        _pinned.Clear();
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
            if (_cache.Remove(oldest.Value, out var removed) && !_pinned.ContainsKey(removed))
            {
                removed.Dispose();
            }
        }
    }
}
