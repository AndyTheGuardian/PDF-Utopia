namespace PdfUtopia.Core.Pages;

/// <summary>
/// Eine Seite des bearbeiteten Dokuments.
/// </summary>
/// <param name="Id">Feste Kennung; bleibt beim Umsortieren und Drehen gleich und ordnet die Annotationen zu.</param>
/// <param name="SourcePageNumber">Seitennummer in der geöffneten Datei (1-basiert).</param>
/// <param name="Rotation">Zusätzliche Drehung gegenüber der Datei.</param>
/// <param name="SourceIndex">Quelldatei: 0 = die geöffnete Datei, ab 1 eine Datei, aus der Seiten eingefügt wurden (A41).</param>
public sealed record PageEntry(int Id, int SourcePageNumber, PageRotation Rotation = PageRotation.None, int SourceIndex = 0)
{
    /// <summary>Kennung der geöffneten Datei in <see cref="SourceIndex"/>.</summary>
    public const int OpenedFile = 0;

    /// <summary>Ob die Seite aus einer anderen Datei eingefügt wurde.</summary>
    public bool IsInserted => SourceIndex != OpenedFile;
}

/// <summary>
/// Reihenfolge, Drehung und Bestand der Seiten eines Dokuments. Ein Dokument behält immer mindestens eine Seite.
/// </summary>
public sealed class PageList : IReadOnlyList<PageEntry>
{
    private readonly List<PageEntry> _pages;
    private int _nextId;

    /// <summary>
    /// Erzeugt die Seitenliste einer geöffneten Datei mit den Seiten 1 bis <paramref name="sourcePageCount"/>.
    /// </summary>
    /// <param name="sourcePageCount">Seitenanzahl der Datei (≥ 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Seitenanzahl unter 1.</exception>
    public PageList(int sourcePageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourcePageCount, 1);
        _pages = Enumerable.Range(1, sourcePageCount).Select(number => new PageEntry(number, number)).ToList();
        _nextId = sourcePageCount + 1;
    }

    /// <summary>Wird nach jeder Änderung ausgelöst.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc />
    public int Count => _pages.Count;

    /// <inheritdoc />
    public PageEntry this[int index] => _pages[index];

    /// <summary>
    /// Sucht die Position einer Seite.
    /// </summary>
    /// <param name="pageId">Kennung der Seite.</param>
    /// <returns>Die Position oder −1.</returns>
    public int IndexOf(int pageId) => _pages.FindIndex(page => page.Id == pageId);

    /// <summary>
    /// Verschiebt eine Seite. <paramref name="toIndex"/> ist die Position nach dem Verschieben.
    /// </summary>
    /// <param name="fromIndex">Bisherige Position.</param>
    /// <param name="toIndex">Neue Position.</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Position außerhalb der Liste.</exception>
    public void Move(int fromIndex, int toIndex)
    {
        CheckIndex(fromIndex);
        CheckIndex(toIndex);
        if (fromIndex == toIndex)
        {
            return;
        }

        var page = _pages[fromIndex];
        _pages.RemoveAt(fromIndex);
        _pages.Insert(toIndex, page);
        OnChanged();
    }

    /// <summary>
    /// Dreht eine Seite um eine Vierteldrehung.
    /// </summary>
    /// <param name="index">Position der Seite.</param>
    /// <param name="clockwise"><c>true</c> im, <c>false</c> gegen den Uhrzeigersinn.</param>
    /// <returns>Die neue Drehung.</returns>
    public PageRotation Rotate(int index, bool clockwise = true)
    {
        CheckIndex(index);
        var page = _pages[index];
        _pages[index] = page with { Rotation = page.Rotation.Turn(clockwise) };
        OnChanged();
        return _pages[index].Rotation;
    }

    /// <summary>
    /// Entfernt eine Seite. Die letzte verbleibende Seite kann nicht entfernt werden.
    /// </summary>
    /// <param name="index">Position der Seite.</param>
    /// <returns>Die entfernte Seite (für Rückgängig).</returns>
    /// <exception cref="InvalidOperationException">Wenn nur noch eine Seite vorhanden ist.</exception>
    public PageEntry RemoveAt(int index)
    {
        CheckIndex(index);
        if (_pages.Count == 1)
        {
            throw new InvalidOperationException("Die letzte Seite eines Dokuments kann nicht gelöscht werden.");
        }

        var page = _pages[index];
        _pages.RemoveAt(index);
        OnChanged();
        return page;
    }

    /// <summary>
    /// Fügt eine zuvor entfernte Seite wieder ein.
    /// </summary>
    /// <param name="index">Position (0 bis <see cref="Count"/>).</param>
    /// <param name="page">Die Seite.</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Position außerhalb der Liste.</exception>
    /// <exception cref="InvalidOperationException">Wenn eine Seite mit derselben Kennung schon enthalten ist.</exception>
    public void Insert(int index, PageEntry page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _pages.Count);
        if (IndexOf(page.Id) >= 0)
        {
            throw new InvalidOperationException($"Seite {page.Id} ist bereits enthalten.");
        }

        _pages.Insert(index, page);
        OnChanged();
    }

    /// <summary>
    /// Erzeugt neue Seiten mit bisher nicht vergebenen Kennungen, ohne sie einzufügen (Seiten einfügen, A41).
    /// </summary>
    /// <param name="sourceIndex">Quelldatei der Seiten.</param>
    /// <param name="sourcePageNumbers">Seitennummern in der Quelldatei (1-basiert), in Zielreihenfolge.</param>
    /// <returns>Die neuen Seiten.</returns>
    public IReadOnlyList<PageEntry> CreateEntries(int sourceIndex, IEnumerable<int> sourcePageNumbers) =>
        sourcePageNumbers.Select(number => new PageEntry(_nextId++, number, SourceIndex: sourceIndex)).ToList();

    /// <summary>
    /// Fügt mehrere Seiten an einer Position ein.
    /// </summary>
    /// <param name="index">Position (0 bis <see cref="Count"/>).</param>
    /// <param name="pages">Die Seiten.</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Position außerhalb der Liste.</exception>
    /// <exception cref="InvalidOperationException">Wenn eine Seite mit derselben Kennung schon enthalten ist.</exception>
    public void InsertRange(int index, IReadOnlyList<PageEntry> pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _pages.Count);
        if (pages.FirstOrDefault(page => IndexOf(page.Id) >= 0) is { } duplicate)
        {
            throw new InvalidOperationException($"Seite {duplicate.Id} ist bereits enthalten.");
        }

        _pages.InsertRange(index, pages);
        OnChanged();
    }

    /// <summary>
    /// Entfernt mehrere aufeinanderfolgende Seiten; mindestens eine Seite bleibt erhalten.
    /// </summary>
    /// <param name="index">Position der ersten Seite.</param>
    /// <param name="count">Anzahl.</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei einem Bereich außerhalb der Liste.</exception>
    /// <exception cref="InvalidOperationException">Wenn keine Seite übrig bliebe.</exception>
    public void RemoveRange(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, _pages.Count);
        if (count >= _pages.Count)
        {
            throw new InvalidOperationException("Ein Dokument behält immer mindestens eine Seite.");
        }

        _pages.RemoveRange(index, count);
        OnChanged();
    }

    /// <summary>
    /// Ersetzt die Reihenfolge durch eine Umstellung derselben Seiten (Verschieben mehrerer Seiten, A48).
    /// </summary>
    /// <param name="order">Alle Seiten in neuer Reihenfolge.</param>
    /// <exception cref="ArgumentException">Wenn nicht genau dieselben Seiten enthalten sind.</exception>
    public void Reorder(IReadOnlyList<PageEntry> order)
    {
        if (order.Count != _pages.Count || !order.Select(page => page.Id).ToHashSet().SetEquals(_pages.Select(page => page.Id)))
        {
            throw new ArgumentException("Die neue Reihenfolge muss genau dieselben Seiten enthalten.", nameof(order));
        }

        _pages.Clear();
        _pages.AddRange(order);
        OnChanged();
    }

    /// <inheritdoc />
    public IEnumerator<PageEntry> GetEnumerator() => _pages.GetEnumerator();

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Prüft eine Position.
    /// </summary>
    /// <param name="index">Die Position.</param>
    private void CheckIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _pages.Count);
    }

    /// <summary>
    /// Meldet eine Änderung.
    /// </summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
