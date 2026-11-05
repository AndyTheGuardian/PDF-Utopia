using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Pages;

/// <summary>
/// Tests der Lesezeichen-Zuordnung (A46, P6).
/// </summary>
public sealed class BookmarkMapTests
{
    private static Bookmark Mark(string title, int? page, params Bookmark[] children) => new(title, page, true, children);

    private static readonly Bookmark[] Outline =
    [
        Mark("Kapitel 1", 1, Mark("1.1", 2), Mark("1.2", 3)),
        Mark("Kapitel 2", 4, Mark("2.1", 5)),
        Mark("Anhang", null, Mark("A", 6)),
    ];

    [Fact]
    public void Place_UsesCurrentPageOrder()
    {
        var document = new EditableDocument(6);
        document.MovePage(3, 0);

        var placed = BookmarkMap.Place(Outline, document.Pages.ToList());

        Assert.Equal(1, placed[0].PageIndex);
        Assert.Equal(0, placed[1].PageIndex);
        Assert.Null(placed[2].PageIndex);
        Assert.Equal(5, placed[2].Children[0].PageIndex);
    }

    [Fact]
    public void Place_DeletedPage_IsMissingTarget()
    {
        var document = new EditableDocument(6);
        document.DeletePage(2);

        var placed = BookmarkMap.Place(Outline, document.Pages.ToList());

        Assert.True(placed[0].Children[1].IsTargetMissing);
        Assert.False(placed[2].IsTargetMissing);
    }

    [Fact]
    public void Place_IgnoresInsertedPagesFromOtherFiles()
    {
        var document = new EditableDocument(1);
        document.InsertPages(0, 1, [4]);

        var placed = BookmarkMap.Place([Mark("Seite 4", 4)], document.Pages.ToList());

        Assert.Null(placed[0].PageIndex);
    }

    [Theory]
    [InlineData(0, "Kapitel 1")]
    [InlineData(1, "1.1")]
    [InlineData(2, "1.2")]
    [InlineData(4, "2.1")]
    [InlineData(5, "A")]
    public void Current_IsLastBookmarkAtOrBeforePage(int pageIndex, string expected)
    {
        var placed = BookmarkMap.Place(Outline, new EditableDocument(6).Pages.ToList());
        Assert.Equal(expected, BookmarkMap.Current(placed, pageIndex)?.Source.Title);
    }

    [Fact]
    public void Current_BeforeFirstBookmark_IsNull()
    {
        var placed = BookmarkMap.Place([Mark("Später", 3)], new EditableDocument(4).Pages.ToList());
        Assert.Null(BookmarkMap.Current(placed, 0));
    }

    [Fact]
    public void Prune_DropsDeletedTargets_AndPromotesChildren()
    {
        // Seite 5 („2.1“) und Seite 1 („Kapitel 1“) löschen; hinten zuerst, damit die Positionen stimmen
        var document = new EditableDocument(6);
        document.DeletePage(4);
        document.DeletePage(0);

        var pruned = BookmarkMap.Prune(BookmarkMap.Place(Outline, document.Pages.ToList()));

        Assert.Equal(["1.1", "1.2", "Kapitel 2", "Anhang"], pruned.Select(bookmark => bookmark.Source.Title));
        Assert.Empty(pruned[2].Children);
        Assert.Equal("A", Assert.Single(pruned[3].Children).Source.Title);
    }

    [Fact]
    public void Prune_GroupWithoutRemainingChildren_IsDropped()
    {
        var document = new EditableDocument(6);
        document.DeletePage(5);

        var pruned = BookmarkMap.Prune(BookmarkMap.Place(Outline, document.Pages.ToList()));

        Assert.DoesNotContain(pruned, bookmark => bookmark.Source.Title == "Anhang");
    }
}
