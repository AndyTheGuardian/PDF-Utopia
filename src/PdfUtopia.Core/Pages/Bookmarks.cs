namespace PdfUtopia.Core.Pages;

/// <summary>
/// Lesezeichen der geöffneten Datei (A46), wie es in der Datei steht.
/// </summary>
/// <param name="Title">Titel.</param>
/// <param name="SourcePageNumber">Zielseite in der Datei (1-basiert) oder <c>null</c> ohne Ziel (reine Gliederung).</param>
/// <param name="IsOpen">Ob die Unterpunkte laut Datei aufgeklappt angezeigt werden sollen.</param>
/// <param name="Children">Unterpunkte.</param>
public sealed record Bookmark(string Title, int? SourcePageNumber, bool IsOpen, IReadOnlyList<Bookmark> Children);

/// <summary>
/// Lesezeichen bezogen auf eine Seitenliste: Zielposition statt Seitennummer der Datei.
/// </summary>
/// <param name="Source">Das Lesezeichen aus der Datei.</param>
/// <param name="PageIndex">Position der Zielseite in der Seitenliste (0-basiert) oder <c>null</c>, wenn das Lesezeichen kein
/// Ziel hat oder seine Seite nicht (mehr) enthalten ist.</param>
/// <param name="Children">Unterpunkte.</param>
public sealed record PlacedBookmark(Bookmark Source, int? PageIndex, IReadOnlyList<PlacedBookmark> Children)
{
    /// <summary>Ob das Ziel gelöscht ist (das Lesezeichen hatte eine Zielseite, die nicht mehr enthalten ist).</summary>
    public bool IsTargetMissing => Source.SourcePageNumber is not null && PageIndex is null;
}

/// <summary>
/// Ordnet Lesezeichen der aktuellen Seitenreihenfolge zu (A46): für die Anzeige, das angezeigte Kapitel und das
/// Speichern (P6).
/// </summary>
public static class BookmarkMap
{
    /// <summary>
    /// Bezieht Lesezeichen auf eine Seitenliste. Ziel ist jeweils die erste Seite der geöffneten Datei mit der
    /// Seitennummer des Lesezeichens; eingefügte Seiten anderer Dateien zählen nicht.
    /// </summary>
    /// <param name="bookmarks">Lesezeichen der Datei.</param>
    /// <param name="pages">Seiten in Zielreihenfolge (Dokument oder Auswahl beim Extrahieren).</param>
    /// <returns>Die Lesezeichen mit Zielposition.</returns>
    public static IReadOnlyList<PlacedBookmark> Place(IReadOnlyList<Bookmark> bookmarks, IReadOnlyList<PageEntry> pages)
    {
        var positions = new Dictionary<int, int>();
        for (var i = 0; i < pages.Count; i++)
        {
            if (!pages[i].IsInserted)
            {
                positions.TryAdd(pages[i].SourcePageNumber, i);
            }
        }

        return PlaceAll(bookmarks, positions);
    }

    /// <summary>
    /// Lesezeichen des Kapitels, in dem eine Seite liegt: das letzte Lesezeichen (in Lesereihenfolge) mit der höchsten
    /// Zielposition, die nicht hinter der Seite liegt.
    /// </summary>
    /// <param name="bookmarks">Bezogene Lesezeichen.</param>
    /// <param name="pageIndex">Position der angezeigten Seite.</param>
    /// <returns>Das Lesezeichen oder <c>null</c>, wenn keines davor liegt.</returns>
    public static PlacedBookmark? Current(IReadOnlyList<PlacedBookmark> bookmarks, int pageIndex)
    {
        PlacedBookmark? best = null;
        foreach (var bookmark in Flatten(bookmarks))
        {
            if (bookmark.PageIndex is { } index && index <= pageIndex && (best is null || index >= best.PageIndex))
            {
                best = bookmark;
            }
        }

        return best;
    }

    /// <summary>
    /// Bereitet Lesezeichen zum Speichern vor: Lesezeichen gelöschter Seiten entfallen, ihre Unterpunkte rücken an
    /// ihre Stelle; Gliederungspunkte ohne Ziel bleiben nur, wenn sie noch Unterpunkte haben.
    /// </summary>
    /// <param name="bookmarks">Bezogene Lesezeichen.</param>
    /// <returns>Die verbleibenden Lesezeichen.</returns>
    public static IReadOnlyList<PlacedBookmark> Prune(IReadOnlyList<PlacedBookmark> bookmarks)
    {
        var result = new List<PlacedBookmark>();
        foreach (var bookmark in bookmarks)
        {
            var children = Prune(bookmark.Children);
            if (bookmark.PageIndex is not null || (!bookmark.IsTargetMissing && children.Count > 0))
            {
                result.Add(bookmark with { Children = children });
            }
            else
            {
                result.AddRange(children);
            }
        }

        return result;
    }

    /// <summary>
    /// Alle Lesezeichen in Lesereihenfolge (Eltern vor ihren Unterpunkten).
    /// </summary>
    /// <param name="bookmarks">Die Lesezeichen.</param>
    /// <returns>Die flache Folge.</returns>
    public static IEnumerable<PlacedBookmark> Flatten(IEnumerable<PlacedBookmark> bookmarks) =>
        bookmarks.SelectMany(bookmark => Flatten(bookmark.Children).Prepend(bookmark));

    /// <summary>
    /// Bezieht eine Ebene von Lesezeichen samt Unterpunkten.
    /// </summary>
    private static List<PlacedBookmark> PlaceAll(IReadOnlyList<Bookmark> bookmarks, Dictionary<int, int> positions) =>
        bookmarks.Select(bookmark => new PlacedBookmark(
            bookmark,
            bookmark.SourcePageNumber is { } number && positions.TryGetValue(number, out var index) ? index : null,
            PlaceAll(bookmark.Children, positions))).ToList();
}
