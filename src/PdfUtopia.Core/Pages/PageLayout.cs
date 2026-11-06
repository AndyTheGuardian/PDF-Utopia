namespace PdfUtopia.Core.Pages;

/// <summary>
/// Seitenansicht (A55).
/// </summary>
public enum PageViewMode
{
    /// <summary>Eine Seite; beim Blättern wird sie durch die nächste ersetzt.</summary>
    Single,

    /// <summary>Alle Seiten untereinander.</summary>
    Continuous,

    /// <summary>Ein Seitenpaar; beim Blättern wird es durch das nächste ersetzt.</summary>
    Double,

    /// <summary>Alle Seitenpaare untereinander.</summary>
    DoubleContinuous,
}

/// <summary>
/// Eine Zeile der Seitenansicht: eine Seite oder ein Seitenpaar.
/// </summary>
/// <param name="Left">Linke Seite (0-basiert) oder <c>null</c>, wenn die erste Seite allein rechts steht.</param>
/// <param name="Right">Rechte Seite oder <c>null</c> (Einzelseite oder letzte Seite ohne Partner).</param>
/// <param name="IsPair">Ob die Zeile zu einer Doppelseiten-Ansicht gehört (fehlende Seiten bleiben dann als Lücke frei).</param>
public readonly record struct PageRow(int? Left, int? Right, bool IsPair = false)
{
    /// <summary>Erste Seite der Zeile.</summary>
    public int First => Left ?? Right ?? 0;

    /// <summary>Ob eine Seite in der Zeile steht.</summary>
    /// <param name="index">Position der Seite.</param>
    /// <returns><c>true</c>, wenn sie links oder rechts steht.</returns>
    public bool Contains(int index) => Left == index || Right == index;
}

/// <summary>
/// Aufteilung der Seiten in Zeilen für die Seitenansichten (A55): Einzelseiten untereinander oder Seitenpaare, auf
/// Wunsch mit der ersten Seite allein rechts (wie ein Buchdeckel).
/// </summary>
public static class PageLayout
{
    /// <summary>Abstand zwischen Seiten in Punkten bei Zoom 100 %.</summary>
    public const double Gap = 16;

    /// <summary>Ob die Ansicht Seitenpaare zeigt.</summary>
    /// <param name="mode">Die Ansicht.</param>
    /// <returns><c>true</c> bei Doppelseite.</returns>
    public static bool IsDouble(PageViewMode mode) => mode is PageViewMode.Double or PageViewMode.DoubleContinuous;

    /// <summary>Ob die Ansicht alle Zeilen untereinander zeigt.</summary>
    /// <param name="mode">Die Ansicht.</param>
    /// <returns><c>true</c> bei fortlaufenden Ansichten.</returns>
    public static bool IsContinuous(PageViewMode mode) => mode is PageViewMode.Continuous or PageViewMode.DoubleContinuous;

    /// <summary>
    /// Alle Zeilen des Dokuments.
    /// </summary>
    /// <param name="mode">Die Ansicht.</param>
    /// <param name="pageCount">Anzahl der Seiten.</param>
    /// <param name="firstPageAlone">Bei Doppelseite: erste Seite allein rechts.</param>
    /// <returns>Die Zeilen in Lesereihenfolge.</returns>
    public static IReadOnlyList<PageRow> Rows(PageViewMode mode, int pageCount, bool firstPageAlone)
    {
        if (!IsDouble(mode))
        {
            return Enumerable.Range(0, pageCount).Select(index => new PageRow(index, null)).ToList();
        }

        var rows = new List<PageRow>();
        var start = 0;
        if (firstPageAlone && pageCount > 0)
        {
            rows.Add(new PageRow(null, 0, IsPair: true));
            start = 1;
        }

        for (var index = start; index < pageCount; index += 2)
        {
            rows.Add(new PageRow(index, index + 1 < pageCount ? index + 1 : null, IsPair: true));
        }

        return rows;
    }

    /// <summary>
    /// Zeile, in der eine Seite steht.
    /// </summary>
    /// <param name="rows">Die Zeilen.</param>
    /// <param name="index">Position der Seite.</param>
    /// <returns>Position der Zeile; 0, wenn die Seite nicht vorkommt.</returns>
    public static int RowOf(IReadOnlyList<PageRow> rows, int index)
    {
        for (var row = 0; row < rows.Count; row++)
        {
            if (rows[row].Contains(index))
            {
                return row;
            }
        }

        return 0;
    }

    /// <summary>
    /// Zeilen, die die Ansicht gerade zeigt: alle bei fortlaufenden Ansichten, sonst nur die der aktuellen Seite.
    /// </summary>
    /// <param name="mode">Die Ansicht.</param>
    /// <param name="pageCount">Anzahl der Seiten.</param>
    /// <param name="firstPageAlone">Bei Doppelseite: erste Seite allein rechts.</param>
    /// <param name="currentIndex">Aktuelle Seite.</param>
    /// <returns>Die sichtbaren Zeilen.</returns>
    public static IReadOnlyList<PageRow> VisibleRows(PageViewMode mode, int pageCount, bool firstPageAlone, int currentIndex)
    {
        var rows = Rows(mode, pageCount, firstPageAlone);
        return IsContinuous(mode) || rows.Count == 0 ? rows : [rows[RowOf(rows, currentIndex)]];
    }

    /// <summary>
    /// Größe einer Zeile in Punkten: Seiten nebeneinander mit Abstand. In einem Seitenpaar nimmt eine fehlende Seite
    /// den Platz der vorhandenen ein, damit die erste Seite rechts und eine letzte Seite ohne Partner links steht.
    /// </summary>
    /// <param name="row">Die Zeile.</param>
    /// <param name="displaySize">Angezeigte Größe einer Seite.</param>
    /// <returns>Breite und Höhe.</returns>
    public static (double Width, double Height) RowSize(PageRow row, Func<int, (double Width, double Height)> displaySize)
    {
        var left = row.Left is { } l ? displaySize(l) : default;
        var right = row.Right is { } r ? displaySize(r) : default;
        if (!row.IsPair)
        {
            return row.Left is null ? right : left;
        }

        // Lücke so breit wie die vorhandene Seite
        var (leftWidth, rightWidth) = (row.Left is null ? right.Width : left.Width, row.Right is null ? left.Width : right.Width);
        return (leftWidth + Gap + rightWidth, Math.Max(left.Height, right.Height));
    }
}
