using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Pages;

/// <summary>
/// Tests von Position und Seitenauswahl beim Einfügen von Seiten (A41).
/// </summary>
public sealed class InsertPlacementTests
{
    [Theory]
    [InlineData(InsertPosition.AfterCurrent, 2, 0, 3)]
    [InlineData(InsertPosition.AfterCurrent, 4, 0, 5)]
    [InlineData(InsertPosition.Start, 2, 0, 0)]
    [InlineData(InsertPosition.End, 2, 0, 5)]
    [InlineData(InsertPosition.AfterPage, 2, 3, 3)]
    [InlineData(InsertPosition.AfterPage, 2, 0, 0)]
    [InlineData(InsertPosition.AfterPage, 2, 9, 5)]
    [InlineData(InsertPosition.AfterPage, 2, -1, 0)]
    public void IndexFor(InsertPosition position, int currentIndex, int afterPage, int expected) =>
        Assert.Equal(expected, InsertPlacement.IndexFor(position, currentIndex, afterPage, 5));

    [Theory]
    [InlineData(new[] { 1, 2, 3, 5 }, "1-3,5")]
    [InlineData(new[] { 2 }, "2")]
    [InlineData(new[] { 6, 5, 1, 3 }, "1,3,5-6")]
    [InlineData(new[] { 1, 2, 3 }, "1-3")]
    [InlineData(new int[0], "")]
    public void FormatSelection(int[] pages, string expected) =>
        Assert.Equal(expected, InsertPlacement.FormatSelection(pages));

    [Fact]
    public void FormatSelection_RoundTripsThroughParse()
    {
        int[] pages = [1, 2, 4, 7, 8, 9];
        Assert.Equal(pages, PageSelectionParser.Parse(InsertPlacement.FormatSelection(pages), 10).Pages);
    }

    [Fact]
    public void AllPages() => Assert.Equal([1, 2, 3], InsertPlacement.AllPages(3));

    [Theory]
    [InlineData(new[] { 1, 3 }, new[] { 2, 4, 5 })]
    [InlineData(new int[0], new[] { 1, 2, 3, 4, 5 })]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, new int[0])]
    public void Invert(int[] pages, int[] expected) => Assert.Equal(expected, InsertPlacement.Invert(pages, 5));
}
