using System.Globalization;
using System.Text.RegularExpressions;

namespace PdfUtopia.Core.Pages;

/// <summary>
/// Ergebnis der Seitenauswahl: die zu extrahierenden Seiten in Eingabereihenfolge und die Hinweise dazu.
/// </summary>
/// <param name="Pages">Seitennummern (1-basiert), ohne Duplikate, in der Reihenfolge der Eingabe.</param>
/// <param name="Hints">Hinweise zu ignorierten, getauschten, begrenzten oder doppelten Angaben.</param>
public sealed record PageSelection(IReadOnlyList<int> Pages, IReadOnlyList<PageSelectionHint> Hints)
{
    /// <summary>Ob mindestens eine Seite ausgewählt ist.</summary>
    public bool HasPages => Pages.Count > 0;
}

/// <summary>
/// Wertet die Seitenauswahl beim Extrahieren aus, z. B. „2“, „4-8“ oder „2,4,8,13-15“.
/// Regeln: Seiten sind 1-basiert; die Reihenfolge der Eingabe bleibt erhalten; „8-4“ wird als 4-8 gelesen;
/// Seiten über der Seitenanzahl werden auf die letzte Seite begrenzt; doppelte Seiten werden nur einmal
/// (an der ersten Position) übernommen; ungültige Teile werden ignoriert. Zu jeder Korrektur entsteht ein Hinweis.
/// </summary>
public static partial class PageSelectionParser
{
    /// <summary>
    /// Wertet eine Eingabe aus.
    /// </summary>
    /// <param name="input">Die Eingabe; <c>null</c> oder leer ergibt eine leere Auswahl.</param>
    /// <param name="pageCount">Seitenanzahl des Dokuments (≥ 1).</param>
    /// <returns>Die ausgewählten Seiten und die Hinweise.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Bei einer Seitenanzahl unter 1.</exception>
    public static PageSelection Parse(string? input, int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);
        var collector = new SelectionCollector(pageCount);
        foreach (var token in Tokenize(input))
        {
            collector.Add(token);
        }

        return collector.ToSelection();
    }

    /// <summary>
    /// Zerlegt die Eingabe an Kommas in nicht leere, getrimmte Teile.
    /// </summary>
    /// <param name="input">Die Eingabe.</param>
    /// <returns>Die Teile.</returns>
    private static IEnumerable<string> Tokenize(string? input) =>
        (input ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Einzelne Seite, z. B. „12“.</summary>
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex SinglePattern();

    /// <summary>Bereich, z. B. „4-8“ oder „4 - 8“.</summary>
    [GeneratedRegex(@"^(\d+)\s*-\s*(\d+)$")]
    private static partial Regex RangePattern();

    /// <summary>
    /// Sammelt Seiten und Hinweise während der Auswertung.
    /// </summary>
    /// <param name="pageCount">Seitenanzahl des Dokuments.</param>
    private sealed class SelectionCollector(int pageCount)
    {
        private readonly List<int> _pages = [];
        private readonly HashSet<int> _seen = [];
        private readonly List<PageSelectionHint> _hints = [];

        /// <summary>
        /// Wertet einen Teil der Eingabe aus.
        /// </summary>
        /// <param name="token">Der Teil, z. B. „4-8“.</param>
        public void Add(string token)
        {
            if (!TryReadBounds(token, out var start, out var end) || start < 1 || end < 1)
            {
                AddHint(new PageSelectionHint(PageSelectionHintKind.InvalidToken, token));
                return;
            }

            if (start > end)
            {
                (start, end) = (end, start);
                AddHint(new PageSelectionHint(PageSelectionHintKind.ReversedRange, token, RangeStart: start, RangeEnd: end));
            }

            start = Clamp(start, token);
            end = Clamp(end, token);
            for (var page = start; page <= end; page++)
            {
                AddPage(page);
            }
        }

        /// <summary>
        /// Liefert das Ergebnis.
        /// </summary>
        /// <returns>Seiten und Hinweise.</returns>
        public PageSelection ToSelection() => new(_pages.ToList(), _hints.ToList());

        /// <summary>
        /// Liest Anfang und Ende einer Einzelseite oder eines Bereichs.
        /// </summary>
        /// <param name="token">Der Teil der Eingabe.</param>
        /// <param name="start">Anfang.</param>
        /// <param name="end">Ende (bei Einzelseite gleich dem Anfang).</param>
        /// <returns><c>true</c>, wenn der Teil gültig aufgebaut ist und die Zahlen lesbar sind.</returns>
        private static bool TryReadBounds(string token, out int start, out int end)
        {
            start = end = 0;
            if (SinglePattern().IsMatch(token))
            {
                return TryParse(token, out start) && TryParse(token, out end);
            }

            var range = RangePattern().Match(token);
            return range.Success && TryParse(range.Groups[1].Value, out start) && TryParse(range.Groups[2].Value, out end);
        }

        /// <summary>
        /// Liest eine Zahl; zu große Zahlen (Überlauf) gelten als ungültig.
        /// </summary>
        private static bool TryParse(string text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// Begrenzt eine Seite auf die letzte Seite und meldet die Begrenzung.
        /// </summary>
        /// <param name="page">Angegebene Seite.</param>
        /// <param name="token">Teil der Eingabe für den Hinweis.</param>
        /// <returns>Die gültige Seite.</returns>
        private int Clamp(int page, string token)
        {
            if (page <= pageCount)
            {
                return page;
            }

            AddHint(new PageSelectionHint(PageSelectionHintKind.PageClamped, token, Page: page, RangeEnd: pageCount));
            return pageCount;
        }

        /// <summary>
        /// Übernimmt eine Seite, sofern sie noch nicht enthalten ist; sonst einmaliger Hinweis.
        /// </summary>
        /// <param name="page">Die Seite.</param>
        private void AddPage(int page)
        {
            if (_seen.Add(page))
            {
                _pages.Add(page);
                return;
            }

            AddHint(new PageSelectionHint(PageSelectionHintKind.DuplicatePage, string.Empty, Page: page));
        }

        /// <summary>
        /// Fügt einen Hinweis hinzu, sofern er nicht schon vorhanden ist.
        /// </summary>
        /// <param name="hint">Der Hinweis.</param>
        private void AddHint(PageSelectionHint hint)
        {
            if (!_hints.Contains(hint))
            {
                _hints.Add(hint);
            }
        }
    }
}
