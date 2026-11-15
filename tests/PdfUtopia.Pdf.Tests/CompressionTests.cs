using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core.Compression;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;
using SkiaSharp;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Tests des Komprimierens (A57): Bilder verkleinern und als JPEG speichern, nie größer als vorher.
/// </summary>
public sealed class CompressionTests : IDisposable
{
    private const int ImagePixels = 2400;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Compress_LargeImages_DownsampledToPresetResolution()
    {
        var input = CreateImagePdf(ImageKind.Rgb, ImageKind.Jpeg, ImageKind.Gray);

        var output = PdfCompressor.Compress(input, CompressionPreset.Quality, cancellation: TestContext.Current.CancellationToken);

        Assert.True(output.Length < input.Length / 2, $"{output.Length} von {input.Length}");
        var limit = CompressionPreset.Quality.MaxPixels(842);
        Assert.All(Images(output), image =>
        {
            Assert.True(image.Width <= limit && image.Height <= limit, $"{image.Width}×{image.Height}");
            Assert.Equal("/DCTDecode", image.Filter);
        });
    }

    [Fact]
    public void Compress_Economy_SmallerThanQuality()
    {
        var input = CreateImagePdf(ImageKind.Rgb, ImageKind.Jpeg);

        var quality = PdfCompressor.Compress(input, CompressionPreset.Quality, cancellation: TestContext.Current.CancellationToken);
        var economy = PdfCompressor.Compress(input, CompressionPreset.Economy, cancellation: TestContext.Current.CancellationToken);

        Assert.True(economy.Length < quality.Length, $"{economy.Length} ≥ {quality.Length}");
    }

    [Fact]
    public void Compress_ImageWithTransparency_LeftUnchanged()
    {
        var input = CreateImagePdf(ImageKind.Transparent);

        var output = PdfCompressor.Compress(input, CompressionPreset.Economy, cancellation: TestContext.Current.CancellationToken);

        Assert.Contains(Images(output), image => image.Width == ImagePixels && image.Filter != "/DCTDecode");
    }

    [Fact]
    public void Compress_TextOnly_NeverLarger()
    {
        var input = File.ReadAllBytes(TestPdfFactory.Create(_temp.File("text.pdf")));

        var output = PdfCompressor.Compress(input, CompressionPreset.Economy, cancellation: TestContext.Current.CancellationToken);

        Assert.True(output.Length <= input.Length);
        AssertReadable(output, TestPdfFactory.PageCount);
    }

    [Fact]
    public void Compress_Result_OpensAndRenders()
    {
        var output = PdfCompressor.Compress(CreateImagePdf(ImageKind.Rgb, ImageKind.Gray), CompressionPreset.Economy, cancellation: TestContext.Current.CancellationToken);

        AssertReadable(output, 2);
    }

    [Fact]
    public void Compress_ReportsProgressUpToOne()
    {
        var reports = new List<double>();
        PdfCompressor.Compress(CreateImagePdf(ImageKind.Rgb, ImageKind.Jpeg), CompressionPreset.Quality, new SynchronousProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.NotEmpty(reports);
        Assert.Equal(1, reports[^1]);
        Assert.Equal(reports.Order(), reports);
    }

    [Fact]
    public void Compress_Cancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            PdfCompressor.Compress(CreateImagePdf(ImageKind.Rgb), CompressionPreset.Quality, cancellation: cancellation.Token));
    }

    [Fact]
    public void Save_WithPassword_Protected()
    {
        var target = _temp.File("komprimiert.pdf");

        PdfCompressor.Save(PdfCompressor.Compress(CreateImagePdf(ImageKind.Rgb), CompressionPreset.Quality, cancellation: TestContext.Current.CancellationToken), target, "geheim");

        Assert.Equal(PdfOpenError.PasswordRequired, Assert.Throws<PdfOpenException>(() => PdfSource.Open(target)).Error);
        using var source = PdfSource.Open(target, "geheim");
        Assert.Equal(1, source.PageCount);
    }

    [Fact]
    public void Save_WithoutPassword_WritesBytes()
    {
        var target = _temp.File("komprimiert.pdf");
        var data = PdfCompressor.Compress(CreateImagePdf(ImageKind.Rgb), CompressionPreset.Quality, cancellation: TestContext.Current.CancellationToken);

        PdfCompressor.Save(data, target, null);

        Assert.Equal(data, File.ReadAllBytes(target));
    }

    [Fact]
    public void SaveUnprotected_ProtectedSource_NoPassword()
    {
        var path = TestPdfFactory.Create(_temp.File("geschützt.pdf"), TestPdfFactory.Password);
        using var source = PdfSource.Open(path, TestPdfFactory.Password);

        var data = PdfExporter.SaveUnprotected([source], source.CreateDocument());

        AssertReadable(data, TestPdfFactory.PageCount);
    }

    private enum ImageKind
    {
        Rgb,
        Jpeg,
        Gray,
        Transparent,
    }

    /// <summary>
    /// PDF mit je einer A4-Seite pro Bild (2400 × 2400 Pixel, also rund 290 dpi bei voller Seitenbreite).
    /// </summary>
    private static byte[] CreateImagePdf(params ImageKind[] kinds)
    {
        using var document = new PdfDocument();
        foreach (var kind in kinds)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(595);
            page.Height = XUnit.FromPoint(842);
            using var graphics = XGraphics.FromPdfPage(page);
            using var stream = new MemoryStream(EncodedImage(kind));
            using var image = XImage.FromStream(stream);
            graphics.DrawImage(image, 0, 0, 595, 595);
        }

        using var buffer = new MemoryStream();
        document.Save(buffer, closeStream: false);
        return buffer.ToArray();
    }

    private static readonly Dictionary<ImageKind, byte[]> EncodedImages = [];

    /// <summary>
    /// Fotoähnliches Testbild: Verlauf mit Rauschen, damit verlustfreie Kodierung groß bleibt. Einmal erzeugt.
    /// </summary>
    private static byte[] EncodedImage(ImageKind kind)
    {
        lock (EncodedImages)
        {
            if (!EncodedImages.TryGetValue(kind, out var encoded))
            {
                encoded = CreateEncodedImage(kind);
                EncodedImages[kind] = encoded;
            }

            return encoded;
        }
    }

    private static byte[] CreateEncodedImage(ImageKind kind)
    {
        var random = new Random(4711);
        var transparent = kind == ImageKind.Transparent;
        using var bitmap = new SKBitmap(new SKImageInfo(ImagePixels, ImagePixels, SKColorType.Rgba8888,
            transparent ? SKAlphaType.Unpremul : SKAlphaType.Opaque));
        var pixels = new byte[bitmap.ByteCount];
        var index = 0;
        for (var y = 0; y < ImagePixels; y++)
        {
            for (var x = 0; x < ImagePixels; x++, index += 4)
            {
                var noise = random.Next(40);
                var red = (byte)(x * 200 / ImagePixels + noise);
                pixels[index] = red;
                pixels[index + 1] = kind == ImageKind.Gray ? red : (byte)(y * 200 / ImagePixels + noise);
                pixels[index + 2] = kind == ImageKind.Gray ? red : (byte)(noise * 4);
                pixels[index + 3] = transparent ? (byte)(x % 255) : (byte)255;
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var data = kind == ImageKind.Jpeg ? bitmap.Encode(SKEncodedImageFormat.Jpeg, 100) : bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Größe und Kodierung aller Bilder einer Datei.
    /// </summary>
    private static List<(int Width, int Height, string Filter)> Images(byte[] pdf)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        return document.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Where(dictionary => dictionary.Elements.GetName("/Subtype") == "/Image" && !dictionary.Elements.GetBoolean("/ImageMask")
                && dictionary.Elements.GetInteger("/Width") > 100 && !IsSoftMask(document, dictionary))
            .Select(dictionary => (dictionary.Elements.GetInteger("/Width"), dictionary.Elements.GetInteger("/Height"), dictionary.Elements.GetName("/Filter")))
            .ToList();
    }

    /// <summary>
    /// Ob ein Bild als weiche Maske eines anderen dient.
    /// </summary>
    private static bool IsSoftMask(PdfDocument document, PdfDictionary candidate) =>
        document.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Any(dictionary => (dictionary.Elements["/SMask"] as PdfSharp.Pdf.Advanced.PdfReference)?.Value == candidate);

    private void AssertReadable(byte[] pdf, int pageCount)
    {
        var path = _temp.File($"{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, pdf);
        using var source = PdfSource.Open(path);
        Assert.Equal(pageCount, source.PageCount);
    }

    /// <summary>Fortschritt ohne Synchronisationskontext (Meldungen kommen sofort an).</summary>
    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
