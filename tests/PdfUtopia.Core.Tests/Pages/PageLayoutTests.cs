using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Pages;

/// <summary>
/// Tests der Aufteilung in Zeilen für die Seitenansichten (A55).
/// </summary>
public sealed class PageLayoutTests
{
    [Theory]
    [InlineData(PageViewMode.Single)]
    [InlineData(PageViewMode.Continuous)]
    public void Rows_SinglePages_OnePagePerRow(PageViewMode mode)
    {
        var rows = PageLayout.Rows(mode, 3, firstPageAlone: true);
        Assert.Equal([new PageRow(0, null), new PageRow(1, null), new PageRow(2, null)], rows);
    }

    [Fact]
    public void Rows_DoubleWithFirstPageAlone_CoverRightThenPairs()
    {
        var rows = PageLayout.Rows(PageViewMode.Double, 6, firstPageAlone: true);
        Assert.Equal([new PageRow(null, 0, true), new PageRow(1, 2, true), new PageRow(3, 4, true), new PageRow(5, null, true)], rows);
    }

    [Fact]
    public void Rows_DoubleWithoutFirstPageAlone_PairsFromStart()
    {
        var rows = PageLayout.Rows(PageViewMode.DoubleContinuous, 5, firstPageAlone: false);
        Assert.Equal([new PageRow(0, 1, true), new PageRow(2, 3, true), new PageRow(4, null, true)], rows);
    }

    [Fact]
    public void Rows_SinglePageDocument_Double_OnlyCover()
    {
        Assert.Equal([new PageRow(null, 0, true)], PageLayout.Rows(PageViewMode.Double, 1, firstPageAlone: true));
        Assert.Equal([new PageRow(0, null, true)], PageLayout.Rows(PageViewMode.Double, 1, firstPageAlone: false));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(5, 3)]
    public void RowOf_FindsRowOfPage(int page, int expectedRow)
    {
        var rows = PageLayout.Rows(PageViewMode.Double, 6, firstPageAlone: true);
        Assert.Equal(expectedRow, PageLayout.RowOf(rows, page));
    }

    [Fact]
    public void VisibleRows_NotContinuous_OnlyRowOfCurrentPage()
    {
        Assert.Equal([new PageRow(3, 4, true)], PageLayout.VisibleRows(PageViewMode.Double, 6, true, 4));
        Assert.Equal([new PageRow(4, null)], PageLayout.VisibleRows(PageViewMode.Single, 6, true, 4));
    }

    [Fact]
    public void VisibleRows_Continuous_AllRows()
    {
        Assert.Equal(4, PageLayout.VisibleRows(PageViewMode.DoubleContinuous, 6, true, 4).Count);
        Assert.Equal(6, PageLayout.VisibleRows(PageViewMode.Continuous, 6, true, 4).Count);
    }

    [Fact]
    public void RowSize_Pair_SideBySideWithGap()
    {
        var size = PageLayout.RowSize(new PageRow(1, 2, true), index => index == 1 ? (600, 800) : (800, 600));
        Assert.Equal((600 + PageLayout.Gap + 800, 800), size);
    }

    [Fact]
    public void RowSize_CoverOrLastPage_KeepsGapForMissingPage()
    {
        Assert.Equal((2 * 600 + PageLayout.Gap, 800), PageLayout.RowSize(new PageRow(null, 0, true), _ => (600, 800)));
        Assert.Equal((2 * 600 + PageLayout.Gap, 800), PageLayout.RowSize(new PageRow(5, null, true), _ => (600, 800)));
    }

    [Fact]
    public void RowSize_SinglePage_PageSize()
    {
        Assert.Equal((600, 800), PageLayout.RowSize(new PageRow(2, null), _ => (600, 800)));
    }

    [Fact]
    public void Settings_UnknownViewMode_FallsBackToSingle()
    {
        var settings = new PdfUtopia.Core.Settings.AppSettings { ViewMode = (PageViewMode)42 }.Normalized();
        Assert.Equal(PageViewMode.Single, settings.ViewMode);
        Assert.True(settings.FirstPageAlone);
    }
}
