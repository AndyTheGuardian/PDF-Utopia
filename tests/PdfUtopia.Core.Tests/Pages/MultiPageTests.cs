using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests.Pages;

/// <summary>
/// Tests der Mehrfachauswahl und der Befehle für mehrere Seiten (A48).
/// </summary>
public sealed class MultiPageTests
{
    private static int[] Order(EditableDocument document) => document.Pages.Select(page => page.SourcePageNumber).ToArray();

    [Fact]
    public void RotatePages_IsOneUndoStep()
    {
        var document = new EditableDocument(4);

        document.RotatePages([3, 1]);

        Assert.Equal([PageRotation.None, PageRotation.Clockwise90, PageRotation.None, PageRotation.Clockwise90], document.Pages.Select(page => page.Rotation));
        Assert.Equal(1, document.History.UndoCount);
        document.History.Undo();
        Assert.All(document.Pages, page => Assert.Equal(PageRotation.None, page.Rotation));
    }

    [Fact]
    public void RotatePages_Counterclockwise()
    {
        var document = new EditableDocument(2);

        document.RotatePages([0, 1], clockwise: false);

        Assert.All(document.Pages, page => Assert.Equal(PageRotation.Clockwise270, page.Rotation));
        document.History.Undo();
        Assert.All(document.Pages, page => Assert.Equal(PageRotation.None, page.Rotation));
    }

    [Fact]
    public void DeletePages_IsOneUndoStep_AndRestoresPositions()
    {
        var document = new EditableDocument(5);

        Assert.True(document.DeletePages([4, 1, 2]));
        Assert.Equal([1, 4], Order(document));

        document.History.Undo();
        Assert.Equal([1, 2, 3, 4, 5], Order(document));
        document.History.Redo();
        Assert.Equal([1, 4], Order(document));
    }

    [Fact]
    public void DeletePages_AllPages_DoesNothing()
    {
        var document = new EditableDocument(2);

        Assert.False(document.DeletePages([0, 1]));
        Assert.Equal(2, document.Pages.Count);
        Assert.False(document.History.CanUndo);
    }

    [Theory]
    [InlineData(new[] { 1, 2 }, 4, true, new[] { 1, 4, 5, 2, 3, 6 })]
    [InlineData(new[] { 3, 5 }, 0, false, new[] { 4, 6, 1, 2, 3, 5 })]
    [InlineData(new[] { 0, 5 }, 2, false, new[] { 2, 1, 6, 3, 4, 5 })]
    [InlineData(new[] { 0, 1 }, 2, false, new[] { 1, 2, 3, 4, 5, 6 })]
    public void MovePages_MovesBlockBeforeOrAfterTarget(int[] indices, int target, bool after, int[] expected)
    {
        var document = new EditableDocument(6);

        document.MovePages(indices, target, after);

        Assert.Equal(expected, Order(document));
    }

    [Fact]
    public void MovePages_IsOneUndoStep_AndNoStepWithoutChange()
    {
        var document = new EditableDocument(4);

        document.MovePages([0, 1], 1, after: true);
        Assert.False(document.History.CanUndo);

        document.MovePages([0, 2], 3, after: true);
        Assert.Equal([2, 4, 1, 3], Order(document));
        document.History.Undo();
        Assert.Equal([1, 2, 3, 4], Order(document));
    }

    [Fact]
    public void Selection_Click_Toggle_Range()
    {
        var pages = new EditableDocument(6).Pages.ToList();
        var selection = new PageListSelection();

        selection.Click(pages, 1, toggle: false, range: false);
        Assert.Equal([1], selection.Indices(pages));
        Assert.False(selection.IsMultiple);

        selection.Click(pages, 4, toggle: true, range: false);
        Assert.Equal([1, 4], selection.Indices(pages));

        selection.Click(pages, 1, toggle: true, range: false);
        Assert.Equal([4], selection.Indices(pages));

        // Wie im Dateimanager: Strg+Klick setzt den Ausgangspunkt, auch beim Abwählen
        selection.Click(pages, 3, toggle: false, range: true);
        Assert.Equal([1, 2, 3], selection.Indices(pages));
        Assert.True(selection.IsMultiple);
    }

    [Fact]
    public void Selection_SurvivesReorder_AndPrunesDeletedPages()
    {
        var document = new EditableDocument(4);
        var selection = new PageListSelection();
        selection.Click(document.Pages.ToList(), 0, false, false);
        selection.Click(document.Pages.ToList(), 2, true, false);

        document.MovePage(0, 3);
        Assert.Equal([1, 3], selection.Indices(document.Pages.ToList()));

        document.DeletePage(3);
        selection.Prune(document.Pages.ToList());
        Assert.Equal(1, selection.Count);
    }

    [Fact]
    public void Selection_SelectAllAndReset()
    {
        var pages = new EditableDocument(3).Pages.ToList();
        var selection = new PageListSelection();

        selection.SelectAll(pages);
        Assert.Equal(3, selection.Count);

        selection.Reset(pages[2].Id);
        Assert.Equal([2], selection.Indices(pages));

        selection.Click(pages, 0, toggle: false, range: true);
        Assert.Equal([0, 1, 2], selection.Indices(pages));
    }
}
