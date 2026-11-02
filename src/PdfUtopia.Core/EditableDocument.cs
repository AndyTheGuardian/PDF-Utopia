using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.History;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.Core;

/// <summary>
/// Ein geöffnetes, bearbeitbares Dokument: Seitenliste, Annotationen und Verlauf.
/// Alle Änderungen laufen über diese Klasse, damit jede rückgängig gemacht werden kann.
/// </summary>
public sealed class EditableDocument
{
    /// <summary>
    /// Erzeugt das Bearbeitungsmodell einer geöffneten Datei.
    /// </summary>
    /// <param name="sourcePageCount">Seitenanzahl der Datei (≥ 1).</param>
    /// <param name="undoLimit">Höchstzahl der Rückgängig-Schritte.</param>
    public EditableDocument(int sourcePageCount, int undoLimit = UndoHistory.DefaultLimit)
    {
        Pages = new PageList(sourcePageCount);
        History = new UndoHistory(undoLimit);
        History.Changed += (_, _) => IsModified = true;
    }

    /// <summary>Seiten in aktueller Reihenfolge.</summary>
    public PageList Pages { get; }

    /// <summary>Annotationen aller Seiten.</summary>
    public AnnotationStore Annotations { get; } = new();

    /// <summary>Verlauf für Rückgängig/Wiederholen.</summary>
    public UndoHistory History { get; }

    /// <summary>Ob es seit dem Öffnen bzw. letzten Speichern Änderungen gab.</summary>
    public bool IsModified { get; private set; }

    /// <summary>Ob die Seite gelöscht werden darf (nicht die letzte).</summary>
    public bool CanDeletePage => Pages.Count > 1;

    /// <summary>
    /// Merkt sich, dass der aktuelle Stand gespeichert ist.
    /// </summary>
    public void MarkSaved() => IsModified = false;

    /// <summary>
    /// Übernimmt beim Öffnen die in der Datei gespeicherten Annotationen einer Seite – ohne Verlaufsschritt
    /// und ohne das Dokument als geändert zu markieren.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="annotations">Annotationen in Zeichenreihenfolge.</param>
    /// <exception cref="InvalidOperationException">Wenn bereits bearbeitet wurde.</exception>
    public void LoadAnnotations(int pageId, IEnumerable<Annotation> annotations)
    {
        if (History.CanUndo || History.CanRedo)
        {
            throw new InvalidOperationException("Gespeicherte Annotationen können nur vor der ersten Änderung geladen werden.");
        }

        foreach (var annotation in annotations)
        {
            Annotations.Add(pageId, annotation);
        }
    }

    /// <summary>
    /// Fügt einer Seite eine Annotation hinzu.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="annotation">Die Annotation.</param>
    public void AddAnnotation(int pageId, Annotation annotation) =>
        History.Execute(new DelegateCommand(
            () => Annotations.Add(pageId, annotation),
            () => Annotations.Remove(pageId, annotation.Id)));

    /// <summary>
    /// Entfernt eine Annotation; Rückgängig stellt sie an derselben Position wieder her.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="annotationId">Kennung der Annotation.</param>
    public void RemoveAnnotation(int pageId, Guid annotationId)
    {
        Annotation? removed = null;
        var removedIndex = 0;
        History.Execute(new DelegateCommand(
            () => (removed, removedIndex) = Annotations.Remove(pageId, annotationId),
            () => Annotations.Add(pageId, removed!, removedIndex)));
    }

    /// <summary>
    /// Entfernt mehrere Annotationen als einen Rückgängig-Schritt (Radierstrich). Rückgängig stellt alle an
    /// ihren bisherigen Positionen wieder her.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="annotationIds">Kennungen der Annotationen; ohne Kennungen entsteht kein Schritt.</param>
    public void RemoveAnnotations(int pageId, IReadOnlyCollection<Guid> annotationIds)
    {
        if (annotationIds.Count == 0)
        {
            return;
        }

        var removed = new List<(Annotation Annotation, int Index)>();
        History.Execute(new DelegateCommand(
            () =>
            {
                removed.Clear();
                var ordered = Annotations.For(pageId).Where(annotation => annotationIds.Contains(annotation.Id)).Select(annotation => annotation.Id).ToList();
                foreach (var id in ordered)
                {
                    removed.Add(Annotations.Remove(pageId, id));
                }
            },
            () =>
            {
                // In umgekehrter Reihenfolge einfügen, damit jede Position wieder stimmt
                for (var i = removed.Count - 1; i >= 0; i--)
                {
                    Annotations.Add(pageId, removed[i].Annotation, removed[i].Index);
                }
            }));
    }

    /// <summary>
    /// Ersetzt eine Annotation durch eine geänderte Fassung, z. B. einen neuen Kommentartext.
    /// </summary>
    /// <param name="pageId">Seitenkennung.</param>
    /// <param name="replacement">Die neue Fassung mit derselben Kennung.</param>
    public void UpdateAnnotation(int pageId, Annotation replacement)
    {
        Annotation? previous = null;
        History.Execute(new DelegateCommand(
            () => previous = Annotations.Replace(pageId, replacement),
            () => Annotations.Replace(pageId, previous!)));
    }

    /// <summary>
    /// Verschiebt eine Seite (Drag &amp; Drop).
    /// </summary>
    /// <param name="fromIndex">Bisherige Position.</param>
    /// <param name="toIndex">Neue Position.</param>
    public void MovePage(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            return;
        }

        History.Execute(new DelegateCommand(() => Pages.Move(fromIndex, toIndex), () => Pages.Move(toIndex, fromIndex)));
    }

    /// <summary>
    /// Dreht eine Seite um eine Vierteldrehung.
    /// </summary>
    /// <param name="index">Position der Seite.</param>
    /// <param name="clockwise"><c>true</c> im, <c>false</c> gegen den Uhrzeigersinn.</param>
    public void RotatePage(int index, bool clockwise = true) =>
        History.Execute(new DelegateCommand(() => Pages.Rotate(index, clockwise), () => Pages.Rotate(index, !clockwise)));

    /// <summary>
    /// Löscht eine Seite; ihre Annotationen bleiben für Rückgängig erhalten.
    /// </summary>
    /// <param name="index">Position der Seite.</param>
    /// <exception cref="InvalidOperationException">Wenn es die letzte Seite ist.</exception>
    public void DeletePage(int index)
    {
        if (!CanDeletePage)
        {
            throw new InvalidOperationException("Die letzte Seite eines Dokuments kann nicht gelöscht werden.");
        }

        PageEntry? removed = null;
        History.Execute(new DelegateCommand(() => removed = Pages.RemoveAt(index), () => Pages.Insert(index, removed!)));
    }
}
