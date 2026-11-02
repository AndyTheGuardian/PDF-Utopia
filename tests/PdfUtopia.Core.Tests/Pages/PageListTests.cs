using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Pages;

/// <summary>
/// Tests der Seitenliste: Umsortieren, Drehen, Löschen.
/// </summary>
public sealed class PageListTests
{
    private static int[] Order(PageList pages) => pages.Select(page => page.SourcePageNumber).ToArray();

    [Fact]
    public void New_ContainsSourcePagesInOrder()
    {
        var pages = new PageList(4);

        Assert.Equal([1, 2, 3, 4], Order(pages));
        Assert.All(pages, page => Assert.Equal(PageRotation.None, page.Rotation));
    }

    [Theory]
    [InlineData(0, 3, new[] { 2, 3, 4, 1 })]
    [InlineData(3, 0, new[] { 4, 1, 2, 3 })]
    [InlineData(1, 2, new[] { 1, 3, 2, 4 })]
    [InlineData(2, 2, new[] { 1, 2, 3, 4 })]
    public void Move_PlacesPageAtTargetIndex(int from, int to, int[] expected)
    {
        var pages = new PageList(4);

        pages.Move(from, to);

        Assert.Equal(expected, Order(pages));
    }

    [Theory]
    [InlineData(PageRotation.None, true, PageRotation.Clockwise90)]
    [InlineData(PageRotation.Clockwise270, true, PageRotation.None)]
    [InlineData(PageRotation.None, false, PageRotation.Clockwise270)]
    [InlineData(PageRotation.Rotate180, false, PageRotation.Clockwise90)]
    public void Turn_RotatesByQuarter(PageRotation start, bool clockwise, PageRotation expected) =>
        Assert.Equal(expected, start.Turn(clockwise));

    [Fact]
    public void Rotate_FourTimes_ReturnsToStartAndKeepsId()
    {
        var pages = new PageList(2);
        var id = pages[1].Id;

        for (var i = 0; i < 4; i++)
        {
            pages.Rotate(1);
        }

        Assert.Equal(PageRotation.None, pages[1].Rotation);
        Assert.Equal(id, pages[1].Id);
    }

    [Fact]
    public void RemoveAt_LastRemainingPage_Throws()
    {
        var pages = new PageList(1);

        Assert.Throws<InvalidOperationException>(() => pages.RemoveAt(0));
    }

    [Fact]
    public void RemoveAndInsert_RestoresPage()
    {
        var pages = new PageList(3);

        var removed = pages.RemoveAt(1);
        pages.Insert(1, removed);

        Assert.Equal([1, 2, 3], Order(pages));
    }

    [Fact]
    public void Insert_DuplicateId_Throws()
    {
        var pages = new PageList(2);

        Assert.Throws<InvalidOperationException>(() => pages.Insert(0, pages[1]));
    }

    [Fact]
    public void Move_InvalidIndex_Throws()
    {
        var pages = new PageList(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => pages.Move(0, 2));
    }

    [Fact]
    public void Changes_RaiseChangedEvent()
    {
        var pages = new PageList(3);
        var count = 0;
        pages.Changed += (_, _) => count++;

        pages.Move(0, 1);
        pages.Rotate(0);
        pages.RemoveAt(2);

        Assert.Equal(3, count);
    }
}
