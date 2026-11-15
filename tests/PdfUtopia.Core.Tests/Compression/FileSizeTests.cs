using System.Globalization;
using PdfUtopia.Core.Compression;

namespace PdfUtopia.Core.Tests.Compression;

/// <summary>
/// Tests der Anzeige von Dateigrößen.
/// </summary>
public sealed class FileSizeTests
{
    [Theory]
    [InlineData(2_516_582, "de-DE", "2,4 MB")]
    [InlineData(2_516_582, "en-US", "2.4 MB")]
    [InlineData(604_160, "de-DE", "590 KB")]
    [InlineData(100, "de-DE", "1 KB")]
    [InlineData(0, "en-US", "1 KB")]
    public void Format_MegabytesWithOneDecimal_KilobytesWhole(long bytes, string culture, string expected)
    {
        Assert.Equal(expected, FileSize.Format(bytes, CultureInfo.GetCultureInfo(culture)));
    }
}
