using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core.Tests;

/// <summary>
/// Tests des Bearbeitungsmodells: jede Änderung ist rückgängig machbar und wiederholbar.
/// </summary>
public sealed class EditableDocumentTests
{
    private static CommentAnnotation Comment(string text = "Hinweis") =>
        new(Guid.NewGuid(), "#4F46E5", new PdfRect(50, 100, 400, 60), text);

    private static RectangleAnnotation Rectangle() =>
        new(Guid.NewGuid(), "#2F6FDF", 2, new PdfRect(40, 300, 500, 160));

    private static int[] Order(EditableDocument document) => document.Pages.Select(page => page.SourcePageNumber).ToArray();

    [Fact]
    public void AddAnnotation_UndoRedo()
    {
        var document = new EditableDocument(3);
        var comment = Comment();

        document.AddAnnotation(1, comment);
        Assert.Same(comment, Assert.Single(document.Annotations.For(1)));

        document.History.Undo();
        Assert.Empty(document.Annotations.For(1));

        document.History.Redo();
        Assert.Single(document.Annotations.For(1));
    }

    [Fact]
    public void RemoveAnnotation_Undo_RestoresAtSamePosition()
    {
        var document = new EditableDocument(1);
        var first = Rectangle();
        var second = Comment();
        var third = Rectangle();
        document.AddAnnotation(1, first);
        document.AddAnnotation(1, second);
        document.AddAnnotation(1, third);

        document.RemoveAnnotation(1, second.Id);
        document.History.Undo();

        Assert.Equal([first.Id, second.Id, third.Id], document.Annotations.For(1).Select(annotation => annotation.Id));
    }

    [Fact]
    public void UpdateAnnotation_Undo_RestoresPreviousText()
    {
        var document = new EditableDocument(1);
        var comment = Comment("alt");
        document.AddAnnotation(1, comment);

        document.UpdateAnnotation(1, comment with { Text = "neu" });
        Assert.Equal("neu", document.Annotations.CommentsFor(1)[0].Text);

        document.History.Undo();
        Assert.Equal("alt", document.Annotations.CommentsFor(1)[0].Text);
    }

    [Fact]
    public void FootnoteNumbers_FollowCommentOrderAndIgnoreOtherAnnotations()
    {
        var document = new EditableDocument(1);
        var first = Comment();
        var second = Comment();
        document.AddAnnotation(1, first);
        document.AddAnnotation(1, Rectangle());
        document.AddAnnotation(1, second);

        Assert.Equal(1, document.Annotations.FootnoteNumber(1, first.Id));
        Assert.Equal(2, document.Annotations.FootnoteNumber(1, second.Id));

        document.RemoveAnnotation(1, first.Id);
        Assert.Equal(1, document.Annotations.FootnoteNumber(1, second.Id));
        Assert.Null(document.Annotations.FootnoteNumber(1, first.Id));
    }

    [Fact]
    public void MovePage_UndoRedo()
    {
        var document = new EditableDocument(4);

        document.MovePage(0, 3);
        Assert.Equal([2, 3, 4, 1], Order(document));

        document.History.Undo();
        Assert.Equal([1, 2, 3, 4], Order(document));

        document.History.Redo();
        Assert.Equal([2, 3, 4, 1], Order(document));
    }

    [Fact]
    public void MovePage_SameIndex_CreatesNoHistoryStep()
    {
        var document = new EditableDocument(2);

        document.MovePage(1, 1);

        Assert.False(document.History.CanUndo);
    }

    [Fact]
    public void RotatePage_Undo_RestoresRotation()
    {
        var document = new EditableDocument(2);

        document.RotatePage(1);
        Assert.Equal(PageRotation.Clockwise90, document.Pages[1].Rotation);

        document.History.Undo();
        Assert.Equal(PageRotation.None, document.Pages[1].Rotation);
    }

    [Fact]
    public void DeletePage_Undo_RestoresPageWithItsAnnotations()
    {
        var document = new EditableDocument(3);
        var pageId = document.Pages[1].Id;
        document.AddAnnotation(pageId, Comment());

        document.DeletePage(1);
        Assert.Equal([1, 3], Order(document));

        document.History.Undo();
        Assert.Equal([1, 2, 3], Order(document));
        Assert.Single(document.Annotations.For(pageId));
    }

    [Fact]
    public void DeletePage_LastPage_Throws()
    {
        var document = new EditableDocument(1);

        Assert.False(document.CanDeletePage);
        Assert.Throws<InvalidOperationException>(() => document.DeletePage(0));
    }

    [Fact]
    public void AnnotationsFollowPageWhenReordered()
    {
        var document = new EditableDocument(3);
        var pageId = document.Pages[0].Id;
        document.AddAnnotation(pageId, Rectangle());

        document.MovePage(0, 2);

        Assert.Equal(pageId, document.Pages[2].Id);
        Assert.Single(document.Annotations.For(document.Pages[2].Id));
    }

    [Fact]
    public void IsModified_SetByChangesAndResetBySave()
    {
        var document = new EditableDocument(2);
        Assert.False(document.IsModified);

        document.RotatePage(0);
        Assert.True(document.IsModified);

        document.MarkSaved();
        Assert.False(document.IsModified);
    }

    [Fact]
    public void LoadAnnotations_AddsWithoutHistoryAndModification()
    {
        var document = new EditableDocument(2);

        document.LoadAnnotations(2, [Comment("gespeichert"), Rectangle()]);

        Assert.Equal(2, document.Annotations.For(2).Count);
        Assert.False(document.History.CanUndo);
        Assert.False(document.IsModified);
    }

    [Fact]
    public void LoadAnnotations_AfterEditing_Throws()
    {
        var document = new EditableDocument(2);
        document.RotatePage(0);

        Assert.Throws<InvalidOperationException>(() => document.LoadAnnotations(1, [Comment()]));
    }
}
