using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Pages;

/// <summary>
/// Tests der Seitenauswahl beim Extrahieren (Spezifikation: Einzeln, Bereich, Auswahl; Regeln aus F5/F13).
/// </summary>
public sealed class PageSelectionParserTests
{
    private const int PageCount = 14;

    [Theory]
    [InlineData("2", new[] { 2 })]
    [InlineData("4-8", new[] { 4, 5, 6, 7, 8 })]
    [InlineData("2,4,8,13-15", new[] { 2, 4, 8, 13, 14 })]
    [InlineData(" 2 , 4 - 6 ", new[] { 2, 4, 5, 6 })]
    [InlineData("8,2", new[] { 8, 2 })]
    [InlineData("1-14", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 })]
    public void Parse_ValidInput_ReturnsPagesInInputOrder(string input, int[] expected)
    {
        var selection = PageSelectionParser.Parse(input, PageCount);

        Assert.Equal(expected, selection.Pages);
    }

    [Fact]
    public void Parse_ReversedRange_IsReadAscendingWithHint()
    {
        var selection = PageSelectionParser.Parse("8-4", PageCount);

        Assert.Equal([4, 5, 6, 7, 8], selection.Pages);
        var hint = Assert.Single(selection.Hints);
        Assert.Equal(new PageSelectionHint(PageSelectionHintKind.ReversedRange, "8-4", RangeStart: 4, RangeEnd: 8), hint);
    }

    [Fact]
    public void Parse_SinglePageAboveCount_UsesLastPageWithHint()
    {
        var selection = PageSelectionParser.Parse("20", PageCount);

        Assert.Equal([14], selection.Pages);
        Assert.Equal(new PageSelectionHint(PageSelectionHintKind.PageClamped, "20", Page: 20, RangeEnd: 14), Assert.Single(selection.Hints));
    }

    [Fact]
    public void Parse_RangeEndAboveCount_IsClampedToLastPage()
    {
        var selection = PageSelectionParser.Parse("13-15", PageCount);

        Assert.Equal([13, 14], selection.Pages);
        Assert.Equal(PageSelectionHintKind.PageClamped, Assert.Single(selection.Hints).Kind);
    }

    [Fact]
    public void Parse_DuplicatePages_AreKeptOnceAtFirstPositionWithOneHintEach()
    {
        var selection = PageSelectionParser.Parse("2,3-5,4,2,2", PageCount);

        Assert.Equal([2, 3, 4, 5], selection.Pages);
        Assert.Equal(
            [new PageSelectionHint(PageSelectionHintKind.DuplicatePage, "", Page: 4), new PageSelectionHint(PageSelectionHintKind.DuplicatePage, "", Page: 2)],
            selection.Hints);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("0-3")]
    [InlineData("-3")]
    [InlineData("2-")]
    [InlineData("1-2-3")]
    [InlineData("99999999999")]
    public void Parse_InvalidToken_IsIgnoredWithHint(string token)
    {
        var selection = PageSelectionParser.Parse($"1,{token}", PageCount);

        Assert.Equal([1], selection.Pages);
        Assert.Equal(new PageSelectionHint(PageSelectionHintKind.InvalidToken, token), Assert.Single(selection.Hints));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ,")]
    public void Parse_EmptyInput_ReturnsEmptySelectionWithoutHints(string? input)
    {
        var selection = PageSelectionParser.Parse(input, PageCount);

        Assert.False(selection.HasPages);
        Assert.Empty(selection.Hints);
    }

    [Fact]
    public void Parse_CombinedCorrections_ProduceAllHintsInOrder()
    {
        var selection = PageSelectionParser.Parse("2,8-4,13-20,2,abc", PageCount);

        Assert.Equal([2, 4, 5, 6, 7, 8, 13, 14], selection.Pages);
        Assert.Equal(
            [PageSelectionHintKind.ReversedRange, PageSelectionHintKind.PageClamped, PageSelectionHintKind.DuplicatePage, PageSelectionHintKind.InvalidToken],
            selection.Hints.Select(hint => hint.Kind));
    }

    [Fact]
    public void Parse_PageCountBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PageSelectionParser.Parse("1", 0));
}
