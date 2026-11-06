namespace PdfUtopia.Core.Pages;

/// <summary>
/// Mehrfachauswahl in der Seitenliste (A48) wie in einem Dateimanager: Klick wählt eine Seite, Strg+Klick nimmt sie
/// hinzu oder heraus, Umschalt+Klick wählt den Bereich ab der zuletzt angeklickten Seite. Gemerkt werden die
/// Seitenkennungen, damit die Auswahl Umsortieren übersteht.
/// </summary>
public sealed class PageListSelection
{
    private readonly HashSet<int> _selected = [];
    private int? _anchor;

    /// <summary>Wird nach jeder Änderung ausgelöst.</summary>
    public event EventHandler? Changed;

    /// <summary>Anzahl gewählter Seiten.</summary>
    public int Count => _selected.Count;

    /// <summary>Ob mehrere Seiten gewählt sind (erst dann wirken Befehle auf die Auswahl).</summary>
    public bool IsMultiple => _selected.Count > 1;

    /// <summary>
    /// Ob eine Seite gewählt ist.
    /// </summary>
    /// <param name="pageId">Kennung der Seite.</param>
    /// <returns><c>true</c>, wenn sie gewählt ist.</returns>
    public bool Contains(int pageId) => _selected.Contains(pageId);

    /// <summary>
    /// Wertet einen Klick auf eine Seite aus.
    /// </summary>
    /// <param name="pages">Seiten in aktueller Reihenfolge.</param>
    /// <param name="index">Position der angeklickten Seite.</param>
    /// <param name="toggle">Strg gedrückt: Seite hinzunehmen bzw. herausnehmen.</param>
    /// <param name="range">Umschalt gedrückt: Bereich ab der zuletzt angeklickten Seite.</param>
    public void Click(IReadOnlyList<PageEntry> pages, int index, bool toggle, bool range)
    {
        var id = pages[index].Id;
        if (range && _anchor is { } anchor && IndexOf(pages, anchor) is var from and >= 0)
        {
            _selected.Clear();
            for (var i = Math.Min(from, index); i <= Math.Max(from, index); i++)
            {
                _selected.Add(pages[i].Id);
            }
        }
        else if (toggle)
        {
            if (!_selected.Remove(id))
            {
                _selected.Add(id);
            }

            _anchor = id;
        }
        else
        {
            _selected.Clear();
            _selected.Add(id);
            _anchor = id;
        }

        OnChanged();
    }

    /// <summary>
    /// Wählt alle Seiten.
    /// </summary>
    /// <param name="pages">Seiten in aktueller Reihenfolge.</param>
    public void SelectAll(IReadOnlyList<PageEntry> pages)
    {
        _selected.Clear();
        _selected.UnionWith(pages.Select(page => page.Id));
        OnChanged();
    }

    /// <summary>
    /// Hebt die Auswahl auf (die angezeigte Seite bleibt Ausgangspunkt für Umschalt+Klick).
    /// </summary>
    /// <param name="current">Kennung der angezeigten Seite oder <c>null</c>.</param>
    public void Reset(int? current)
    {
        _selected.Clear();
        if (current is { } id)
        {
            _selected.Add(id);
        }

        _anchor = current;
        OnChanged();
    }

    /// <summary>
    /// Entfernt Seiten, die nicht mehr in der Liste sind (gelöscht).
    /// </summary>
    /// <param name="pages">Seiten in aktueller Reihenfolge.</param>
    public void Prune(IReadOnlyList<PageEntry> pages)
    {
        var present = pages.Select(page => page.Id).ToHashSet();
        if (_selected.RemoveWhere(id => !present.Contains(id)) > 0)
        {
            OnChanged();
        }
    }

    /// <summary>
    /// Positionen der gewählten Seiten, aufsteigend.
    /// </summary>
    /// <param name="pages">Seiten in aktueller Reihenfolge.</param>
    /// <returns>Die Positionen.</returns>
    public IReadOnlyList<int> Indices(IReadOnlyList<PageEntry> pages) =>
        Enumerable.Range(0, pages.Count).Where(index => _selected.Contains(pages[index].Id)).ToList();

    /// <summary>Position einer Seitenkennung oder −1.</summary>
    private static int IndexOf(IReadOnlyList<PageEntry> pages, int id)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            if (pages[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Meldet eine Änderung.</summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
