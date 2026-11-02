using PdfUtopia.Core.Pages;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Tests des Öffnens, Renderns und Textauslesens mit PDFium an der erzeugten Testdatei.
/// </summary>
public sealed class PdfSourceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public PdfSourceTests() => _file = TestPdfFactory.Create(_temp.File("testdatei.pdf"));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Open_TestFile_HasAllPages()
    {
        using var source = PdfSource.Open(_file);

        Assert.Equal(TestPdfFactory.PageCount, source.PageCount);
    }

    [Fact]
    public void PageSize_RespectsRotationInFile()
    {
        using var source = PdfSource.Open(_file);

        Assert.Equal((TestPdfFactory.Width, TestPdfFactory.Height), source.PageSize(1));
        Assert.Equal((TestPdfFactory.Height, TestPdfFactory.Width), source.PageSize(TestPdfFactory.RotatedPage));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(14)]
    public void PageText_ContainsHeading(int page)
    {
        using var source = PdfSource.Open(_file);

        Assert.Contains($"Seite {page}", source.PageText(page));
    }

    [Fact]
    public void Render_ProducesPageSizedBitmapWithContent()
    {
        using var source = PdfSource.Open(_file);

        var page = source.Render(1, scale: 1);

        Assert.Equal((595, 842), (page.Width, page.Height));
        Assert.Equal((255, 255, 255), page.PixelAt(5, 5));
        var figure = page.PixelAt(300, 842 - 480);
        Assert.True(figure.B > figure.R && figure.R < 240, $"Abbildung erwartet, war {figure}");
    }

    [Fact]
    public void Render_WithExtraRotation_SwapsSize()
    {
        using var source = PdfSource.Open(_file);

        var page = source.Render(1, scale: 0.5, PageRotation.Clockwise90);

        Assert.Equal((421, 298), (page.Width, page.Height));
    }

    [Fact]
    public void Open_ProtectedFile_RequiresPassword()
    {
        var protectedFile = TestPdfFactory.Create(_temp.File("geschützt.pdf"), TestPdfFactory.Password);

        var exception = Assert.Throws<PdfOpenException>(() => PdfSource.Open(protectedFile));
        Assert.Equal(PdfOpenError.PasswordRequired, exception.Error);
        Assert.Equal(PdfOpenError.PasswordRequired, Assert.Throws<PdfOpenException>(() => PdfSource.Open(protectedFile, "falsch")).Error);

        using var source = PdfSource.Open(protectedFile, TestPdfFactory.Password);
        Assert.Equal(TestPdfFactory.PageCount, source.PageCount);
    }

    [Fact]
    public void Open_NotAPdf_ReportsInvalidFormat()
    {
        var path = _temp.File("kein.pdf");
        File.WriteAllText(path, "Das ist keine PDF-Datei.");

        Assert.Equal(PdfOpenError.InvalidFormat, Assert.Throws<PdfOpenException>(() => PdfSource.Open(path)).Error);
    }

    [Fact]
    public void Open_MissingFile_ReportsNotReadable() =>
        Assert.Equal(PdfOpenError.FileNotReadable, Assert.Throws<PdfOpenException>(() => PdfSource.Open(_temp.File("fehlt.pdf"))).Error);

    [Fact]
    public void Open_DoesNotLockFile()
    {
        using var source = PdfSource.Open(_file);

        File.WriteAllBytes(_file, File.ReadAllBytes(_file));
    }
}
