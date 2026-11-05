using Avalonia;
using Avalonia.Controls;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Reiter der linken Leiste (A46, F30): alle sichtbaren Reiter gleich breit in einer Zeile, solange sie hineinpassen;
/// sonst je zwei nebeneinander, ein einzelner letzter Reiter nimmt die ganze Breite der neuen Zeile ein.
/// </summary>
public sealed class TabStripPanel : Panel
{
    /// <summary>Abstand zwischen Reitern.</summary>
    public const double Spacing = 2;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var tabs = VisibleTabs();
        if (tabs.Count == 0)
        {
            return default;
        }

        foreach (var tab in tabs)
        {
            tab.Measure(Size.Infinity);
        }

        var width = double.IsInfinity(availableSize.Width) ? tabs.Sum(tab => tab.DesiredSize.Width) : availableSize.Width;
        var rows = Rows(tabs, width);
        var height = rows.Sum(row => row.Max(tab => tab.DesiredSize.Height)) + Spacing * (rows.Count - 1);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var row in Rows(VisibleTabs(), finalSize.Width))
        {
            var height = row.Max(tab => tab.DesiredSize.Height);
            var width = (finalSize.Width - Spacing * (row.Count - 1)) / row.Count;
            for (var i = 0; i < row.Count; i++)
            {
                row[i].Arrange(new Rect(i * (width + Spacing), y, width, height));
            }

            y += height + Spacing;
        }

        return finalSize;
    }

    /// <summary>
    /// Teilt die Reiter auf Zeilen auf: eine Zeile, wenn jeder Reiter in seinem gleich breiten Anteil Platz hat.
    /// </summary>
    private static List<List<Control>> Rows(List<Control> tabs, double width)
    {
        var share = (width - Spacing * (tabs.Count - 1)) / tabs.Count;
        return tabs.All(tab => tab.DesiredSize.Width <= share) ? [tabs] : tabs.Chunk(2).Select(row => row.ToList()).ToList();
    }

    /// <summary>Sichtbare Reiter.</summary>
    private List<Control> VisibleTabs() => Children.Where(child => child.IsVisible).ToList();
}
