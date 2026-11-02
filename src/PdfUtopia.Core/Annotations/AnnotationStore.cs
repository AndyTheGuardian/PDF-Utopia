namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Annotationen aller Seiten, zugeordnet über die feste Seitenkennung (<see cref="Pages.PageEntry.Id"/>).
/// Die Reihenfolge je Seite ist die Zeichenreihenfolge; Kommentare werden in dieser Reihenfolge nummeriert.
/// Annotationen gelöschter Seiten bleiben erhalten, damit Rückgängig die Seite vollständig wiederherstellt.
/// </summary>
public sealed class AnnotationStore
{
    private readonly Dictionary<int, List<Annotation>> _byPage = [];

    /// <summary>Wird nach jeder Änderung mit der betroffenen Seitenkennung ausgelöst.</summary>
    public event EventHandler<int>? Changed;

    /// <summary>
    /// Annotationen einer Seite in Zeichenreihenfolge.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <returns>Die Annotationen (leer, wenn keine vorhanden).</returns>
    public IReadOnlyList<Annotation> For(int pageId) =>
        _byPage.TryGetValue(pageId, out var list) ? list.AsReadOnly() : [];

    /// <summary>
    /// Kommentare einer Seite in Nummernreihenfolge; die Fußnotenzahl ist die Position + 1.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <returns>Die Kommentare.</returns>
    public IReadOnlyList<CommentAnnotation> CommentsFor(int pageId) => For(pageId).OfType<CommentAnnotation>().ToList();

    /// <summary>
    /// Fußnotenzahl eines Kommentars.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="commentId">Kennung des Kommentars.</param>
    /// <returns>Die Zahl ab 1 oder <c>null</c>, wenn der Kommentar nicht auf der Seite liegt.</returns>
    public int? FootnoteNumber(int pageId, Guid commentId)
    {
        var index = CommentsFor(pageId).ToList().FindIndex(comment => comment.Id == commentId);
        return index < 0 ? null : index + 1;
    }

    /// <summary>
    /// Fügt eine Annotation hinzu.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="annotation">Die Annotation.</param>
    /// <param name="index">Position in der Zeichenreihenfolge; <c>null</c> = ans Ende.</param>
    /// <exception cref="InvalidOperationException">Wenn die Kennung auf der Seite schon vergeben ist.</exception>
    public void Add(int pageId, Annotation annotation, int? index = null)
    {
        var list = ListFor(pageId);
        if (list.Exists(existing => existing.Id == annotation.Id))
        {
            throw new InvalidOperationException($"Annotation {annotation.Id} ist bereits vorhanden.");
        }

        list.Insert(index is { } position ? Math.Clamp(position, 0, list.Count) : list.Count, annotation);
        Changed?.Invoke(this, pageId);
    }

    /// <summary>
    /// Entfernt eine Annotation.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="annotationId">Kennung der Annotation.</param>
    /// <returns>Die entfernte Annotation und ihre bisherige Position.</returns>
    /// <exception cref="KeyNotFoundException">Wenn die Annotation nicht auf der Seite liegt.</exception>
    public (Annotation Annotation, int Index) Remove(int pageId, Guid annotationId)
    {
        var list = ListFor(pageId);
        var index = IndexOrThrow(list, annotationId);
        var annotation = list[index];
        list.RemoveAt(index);
        Changed?.Invoke(this, pageId);
        return (annotation, index);
    }

    /// <summary>
    /// Ersetzt eine Annotation durch eine geänderte Fassung mit derselben Kennung.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="replacement">Die neue Fassung.</param>
    /// <returns>Die bisherige Fassung.</returns>
    /// <exception cref="KeyNotFoundException">Wenn die Annotation nicht auf der Seite liegt.</exception>
    public Annotation Replace(int pageId, Annotation replacement)
    {
        var list = ListFor(pageId);
        var index = IndexOrThrow(list, replacement.Id);
        var previous = list[index];
        list[index] = replacement;
        Changed?.Invoke(this, pageId);
        return previous;
    }

    /// <summary>
    /// Liefert die Liste einer Seite und legt sie bei Bedarf an.
    /// </summary>
    private List<Annotation> ListFor(int pageId)
    {
        if (!_byPage.TryGetValue(pageId, out var list))
        {
            list = [];
            _byPage[pageId] = list;
        }

        return list;
    }

    /// <summary>
    /// Sucht die Position einer Annotation.
    /// </summary>
    private static int IndexOrThrow(List<Annotation> list, Guid annotationId)
    {
        var index = list.FindIndex(annotation => annotation.Id == annotationId);
        return index >= 0 ? index : throw new KeyNotFoundException($"Annotation {annotationId} nicht gefunden.");
    }
}
