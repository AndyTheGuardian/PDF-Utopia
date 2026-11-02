using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Tests der Suche: Fundstellen auf der Seite (zum Hinterlegen in Akzentfarbe) und Trefferzahl.
/// </summary>
public sealed class SearchTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly PdfSource _source;

    public SearchTests() => _source = PdfSource.Open(TestPdfFactory.Create(_temp.File("testdatei.pdf")));

    public void Dispose()
    {
        _source.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public void FindText_Heading_IsFoundAtItsPosition()
    {
        var hit = Assert.Single(_source.FindText(1, "Seite 1"));

        // Überschrift: links 70, Grundlinie 770 (Benutzerraum) → Anzeige oben etwa 842 − 770 − Schrifthöhe
        Assert.InRange(hit.X, 65, 75);
        Assert.InRange(hit.Bottom, 70, 80);
        Assert.True(hit.Width > 50, $"Breite der Fundstelle: {hit.Width}");
    }

    [Fact]
    public void FindText_IgnoresCase_AndFindsEveryOccurrence()
    {
        var hits = _source.FindText(1, "LOREM");

        Assert.Equal(4, hits.Count);
        Assert.Equal(hits.OrderBy(hit => hit.Y), hits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("gibt es nicht")]
    public void FindText_NoMatch_ReturnsNothing(string term) => Assert.Empty(_source.FindText(1, term));

    [Fact]
    public void FindText_OnPageRotatedInFile_FollowsTextDirection()
    {
        var hit = Assert.Single(_source.FindText(TestPdfFactory.RotatedPage, $"Seite {TestPdfFactory.RotatedPage}"));

        Assert.True(hit.Height > hit.Width, $"Senkrechte Fundstelle erwartet: {hit}");
    }

    [Fact]
    public void CountOccurrences_MatchesFindText()
    {
        var text = _source.PageText(1);

        Assert.Equal(_source.FindText(1, "ipsum").Count, PdfSource.CountOccurrences(text, "Ipsum"));
        Assert.Equal(0, PdfSource.CountOccurrences(text, " "));
    }
}
