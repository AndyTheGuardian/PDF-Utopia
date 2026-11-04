namespace PdfUtopia.Core.Pages;

/// <summary>Wohin eingefügte Seiten kommen (A41).</summary>
public enum InsertPosition
{
    /// <summary>Nach der angezeigten Seite (Standard, F27).</summary>
    AfterCurrent,

    /// <summary>Vor der ersten Seite.</summary>
    Start,

    /// <summary>Nach der letzten Seite.</summary>
    End,

    /// <summary>Nach einer angegebenen Seite.</summary>
    AfterPage,
}

/// <summary>
/// Position und Seitenauswahl beim Einfügen von Seiten aus einer anderen Datei (A41).
/// </summary>
public static class InsertPlacement
{
    /// <summary>
    /// Bestimmt die Position der ersten eingefügten Seite.
    /// </summary>
    /// <param name="position">Gewählte Position.</param>
    /// <param name="currentIndex">Angezeigte Seite (0-basiert).</param>
    /// <param name="afterPage">Seitennummer bei <see cref="InsertPosition.AfterPage"/> (1-basiert; 0 = am Anfang).</param>
    /// <param name="pageCount">Seitenanzahl des Dokuments.</param>
    /// <returns>Position in der Seitenliste (0 bis <paramref name="pageCount"/>).</returns>
    public static int IndexFor(InsertPosition position, int currentIndex, int afterPage, int pageCount) => position switch
    {
        InsertPosition.Start => 0,
        InsertPosition.End => pageCount,
        InsertPosition.AfterPage => Math.Clamp(afterPage, 0, pageCount),
        _ => Math.Clamp(currentIndex + 1, 0, pageCount),
    };

    /// <summary>
    /// Schreibt Seitennummern kurz als Eingabe wie beim Extrahieren, z. B. 1, 2, 3, 5 → „1-3,5“.
    /// </summary>
    /// <param name="pages">Seitennummern; Reihenfolge und Duplikate spielen keine Rolle.</param>
    /// <returns>Die Eingabe; leer, wenn keine Seite gewählt ist.</returns>
    public static string FormatSelection(IEnumerable<int> pages)
    {
        var sorted = pages.Distinct().Order().ToList();
        var parts = new List<string>();
        for (var i = 0; i < sorted.Count; i++)
        {
            var end = i;
            while (end + 1 < sorted.Count && sorted[end + 1] == sorted[end] + 1)
            {
                end++;
            }

            parts.Add(end > i ? $"{sorted[i]}-{sorted[end]}" : $"{sorted[i]}");
            i = end;
        }

        return string.Join(",", parts);
    }

    /// <summary>
    /// Alle Seiten einer Quelle.
    /// </summary>
    /// <param name="pageCount">Seitenanzahl.</param>
    /// <returns>Die Seiten 1 bis <paramref name="pageCount"/>.</returns>
    public static IReadOnlyList<int> AllPages(int pageCount) => Enumerable.Range(1, pageCount).ToList();

    /// <summary>
    /// Kehrt eine Auswahl um: gewählte Seiten werden abgewählt und umgekehrt.
    /// </summary>
    /// <param name="pages">Gewählte Seiten.</param>
    /// <param name="pageCount">Seitenanzahl der Quelle.</param>
    /// <returns>Die übrigen Seiten, aufsteigend.</returns>
    public static IReadOnlyList<int> Invert(IEnumerable<int> pages, int pageCount) =>
        AllPages(pageCount).Except(pages).ToList();
}
