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
    private long _savedState;

    /// <summary>
    /// Erzeugt das Bearbeitungsmodell einer geöffneten Datei.
    /// </summary>
    /// <param name="sourcePageCount">Seitenanzahl der Datei (≥ 1).</param>
    /// <param name="undoLimit">Höchstzahl der Rückgängig-Schritte.</param>
    public EditableDocument(int sourcePageCount, int undoLimit = UndoHistory.DefaultLimit)
    {
        Pages = new PageList(sourcePageCount);
        History = new UndoHistory(undoLimit);
        _savedState = History.State;
    }

    /// <summary>Seiten in aktueller Reihenfolge.</summary>
    public PageList Pages { get; }

    /// <summary>Annotationen aller Seiten.</summary>
    public AnnotationStore Annotations { get; } = new();

    /// <summary>Verlauf für Rückgängig/Wiederholen.</summary>
    public UndoHistory History { get; }

    /// <summary>
    /// Ob sich das Dokument vom geöffneten bzw. zuletzt gespeicherten Stand unterscheidet. Führen Rückgängig oder
    /// Wiederholen genau zu diesem Stand zurück, gilt es wieder als unverändert.
    /// </summary>
    public bool IsModified => History.State != _savedState;

    /// <summary>Ob die Seite gelöscht werden darf (nicht die letzte).</summary>
    public bool CanDeletePage => Pages.Count > 1;

    /// <summary>
    /// Merkt sich, dass der aktuelle Stand gespeichert ist.
    /// </summary>
    public void MarkSaved() => _savedState = History.State;

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
    /// Dreht mehrere Seiten als einen Rückgängig-Schritt (Mehrfachauswahl, A48).
    /// </summary>
    /// <param name="indices">Positionen der Seiten.</param>
    /// <param name="clockwise"><c>true</c> im, <c>false</c> gegen den Uhrzeigersinn (Strg+Klick).</param>
    public void RotatePages(IReadOnlyCollection<int> indices, bool clockwise = true)
    {
        var targets = indices.Distinct().Order().ToList();
        if (targets.Count == 0)
        {
            return;
        }

        History.Execute(new DelegateCommand(
            () => targets.ForEach(index => Pages.Rotate(index, clockwise)),
            () => targets.ForEach(index => Pages.Rotate(index, !clockwise))));
    }

    /// <summary>
    /// Löscht mehrere Seiten als einen Rückgängig-Schritt; mindestens eine Seite bleibt (A48).
    /// </summary>
    /// <param name="indices">Positionen der Seiten.</param>
    /// <returns><c>false</c>, wenn dabei alle Seiten gelöscht würden (dann geschieht nichts).</returns>
    public bool DeletePages(IReadOnlyCollection<int> indices)
    {
        var targets = indices.Distinct().Order().ToList();
        if (targets.Count == 0 || targets.Count >= Pages.Count)
        {
            return false;
        }

        var removed = targets.Select(index => (Index: index, Page: Pages[index])).ToList();
        History.Execute(new DelegateCommand(
            () =>
            {
                // Von hinten löschen, damit die übrigen Positionen gültig bleiben
                foreach (var (index, _) in Enumerable.Reverse(removed))
                {
                    Pages.RemoveAt(index);
                }
            },
            () =>
            {
                foreach (var (index, page) in removed)
                {
                    Pages.Insert(index, page);
                }
            }));
        return true;
    }

    /// <summary>
    /// Verschiebt mehrere Seiten als Block vor oder hinter eine Zielseite; ihre Reihenfolge untereinander bleibt
    /// (A48). Liegt das Ziel selbst in der Auswahl, geschieht nichts.
    /// </summary>
    /// <param name="indices">Positionen der verschobenen Seiten.</param>
    /// <param name="targetIndex">Position der Zielseite.</param>
    /// <param name="after"><c>true</c>: hinter die Zielseite, sonst davor.</param>
    public void MovePages(IReadOnlyCollection<int> indices, int targetIndex, bool after)
    {
        var moving = indices.Distinct().Order().ToList();
        if (moving.Count == 0 || moving.Contains(targetIndex) || targetIndex < 0 || targetIndex >= Pages.Count)
        {
            return;
        }

        var before = Pages.ToList();
        var block = moving.Select(index => before[index]).ToList();
        var rest = before.Where((_, index) => !moving.Contains(index)).ToList();
        var insertAt = rest.IndexOf(before[targetIndex]) + (after ? 1 : 0);
        var order = rest.Take(insertAt).Concat(block).Concat(rest.Skip(insertAt)).ToList();
        if (order.SequenceEqual(before))
        {
            return;
        }

        History.Execute(new DelegateCommand(() => Pages.Reorder(order), () => Pages.Reorder(before)));
    }

    /// <summary>
    /// Fügt Seiten aus einer anderen Datei als einen Rückgängig-Schritt ein (A41). Ihre Annotationen werden mit
    /// neuen Kennungen übernommen und sind danach wie alle anderen bearbeitbar.
    /// </summary>
    /// <param name="index">Position der ersten neuen Seite (0 bis Seitenanzahl).</param>
    /// <param name="sourceIndex">Quelldatei der Seiten (ab 1, siehe <see cref="PageEntry.SourceIndex"/>).</param>
    /// <param name="sourcePageNumbers">Seitennummern in der Quelldatei (1-basiert), in Zielreihenfolge.</param>
    /// <param name="annotationsOf">Gespeicherte Annotationen einer Quellseite oder <c>null</c> für keine.</param>
    /// <returns>Die eingefügten Seiten.</returns>
    /// <exception cref="ArgumentException">Ohne Seiten oder mit der geöffneten Datei als Quelle.</exception>
    public IReadOnlyList<PageEntry> InsertPages(
        int index, int sourceIndex, IReadOnlyList<int> sourcePageNumbers, Func<int, IEnumerable<Annotation>>? annotationsOf = null)
    {
        if (sourcePageNumbers.Count == 0)
        {
            throw new ArgumentException("Es muss mindestens eine Seite eingefügt werden.", nameof(sourcePageNumbers));
        }

        if (sourceIndex == PageEntry.OpenedFile)
        {
            throw new ArgumentException("Eingefügte Seiten brauchen eine eigene Quelldatei.", nameof(sourceIndex));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Pages.Count);

        var entries = Pages.CreateEntries(sourceIndex, sourcePageNumbers);
        foreach (var entry in entries)
        {
            // Die Seiten sind erst nach dem Einfügen sichtbar; Rückgängig lässt die Annotationen wie beim Löschen liegen
            foreach (var annotation in annotationsOf?.Invoke(entry.SourcePageNumber) ?? [])
            {
                Annotations.Add(entry.Id, annotation with { Id = Guid.NewGuid() });
            }
        }

        History.Execute(new DelegateCommand(() => Pages.InsertRange(index, entries), () => Pages.RemoveRange(index, entries.Count)));
        return entries;
    }

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
