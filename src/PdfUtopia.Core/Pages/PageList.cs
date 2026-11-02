namespace PdfUtopia.Core.Pages;

/// <summary>
/// Eine Seite des bearbeiteten Dokuments.
/// </summary>
/// <param name="Id">Feste Kennung; bleibt beim Umsortieren und Drehen gleich und ordnet die Annotationen zu.</param>
/// <param name="SourcePageNumber">Seitennummer in der geöffneten Datei (1-basiert).</param>
/// <param name="Rotation">Zusätzliche Drehung gegenüber der Datei.</param>
public sealed record PageEntry(int Id, int SourcePageNumber, PageRotation Rotation = PageRotation.None);

/// <summary>
/// Reihenfolge, Drehung und Bestand der Seiten eines Dokuments. Ein Dokument behält immer mindestens eine Seite.
/// </summary>
public sealed class PageList : IReadOnlyList<PageEntry>
{
    private readonly List<PageEntry> _pages;

    /// <summary>
    /// Erzeugt die Seitenliste einer geöffneten Datei mit den Seiten 1 bis <paramref name="sourcePageCount"/>.
    /// </summary>
    /// <param name="sourcePageCount">Seitenanzahl der Datei (≥ 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Seitenanzahl unter 1.</exception>
    public PageList(int sourcePageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourcePageCount, 1);
        _pages = Enumerable.Range(1, sourcePageCount).Select(number => new PageEntry(number, number)).ToList();
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
