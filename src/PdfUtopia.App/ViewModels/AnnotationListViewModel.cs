using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.App.Localization;
using PdfUtopia.Core;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Anmerkungsübersicht eines Dokuments (A45, Mockup v4): alle Anmerkungen nach Seiten gruppiert, mit Filter nach
/// Art. Baut sich nach jeder Änderung neu auf; mehrere Änderungen kurz hintereinander ergeben einen Neuaufbau.
/// </summary>
public sealed partial class AnnotationListViewModel : ObservableObject
{
    private readonly EditableDocument _document;
    private readonly Func<PageEntry, PdfRect, string> _highlightedText;
    private IReadOnlyList<AnnotationEntry> _entries = [];
    private bool _refreshPending;

    /// <summary>
    /// Erzeugt die Übersicht.
    /// </summary>
    /// <param name="document">Das Dokument.</param>
    /// <param name="highlightedText">Liefert den Text unter einer Markierung.</param>
    public AnnotationListViewModel(EditableDocument document, Func<PageEntry, PdfRect, string> highlightedText)
    {
        _document = document;
        _highlightedText = highlightedText;
        Filters = [new AnnotationFilterOption(null), .. Enum.GetValues<AnnotationCategory>().Select(category => new AnnotationFilterOption(category))];
        SelectedFilter = Filters[0];
        Refresh();
    }

    /// <summary>Wählbare Filter: alle Arten und jede Art einzeln.</summary>
    public IReadOnlyList<AnnotationFilterOption> Filters { get; }

    /// <summary>Gewählter Filter.</summary>
    [ObservableProperty]
    public partial AnnotationFilterOption SelectedFilter { get; set; }

    /// <summary>Überschriften und Einträge der Liste.</summary>
    public ObservableCollection<AnnotationListItem> Items { get; } = [];

    /// <summary>Anzahl aller Anmerkungen (ohne Filter).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAny))]
    public partial int Count { get; set; }

    /// <summary>Ob das Dokument Anmerkungen hat; nur dann gibt es die Reiter „Seiten | Anmerkungen“.</summary>
    public bool HasAny => Count > 0;

    /// <summary>Ob die Liste mit dem Filter leer ist.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>Hinweis bei leerer Liste.</summary>
    public string EmptyText => Loc.T(Count == 0 ? "Annotations.Empty" : "Annotations.EmptyFilter");

    /// <summary>Zuletzt angesprungene Anmerkung (bleibt in der Liste markiert).</summary>
    public Guid? SelectedId { get; private set; }

    /// <summary>
    /// Plant einen Neuaufbau nach der aktuellen Änderung ein.
    /// </summary>
    public void RequestRefresh()
    {
        if (_refreshPending)
        {
            return;
        }

        _refreshPending = true;
        Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Background);
    }

    /// <summary>
    /// Markiert einen Eintrag als angesprungen.
    /// </summary>
    /// <param name="id">Kennung der Anmerkung.</param>
    public void Select(Guid id)
    {
        SelectedId = id;
        foreach (var item in Items)
        {
            item.IsSelected = item.Entry?.Annotation.Id == id;
        }
    }

    /// <summary>
    /// Baut die Liste nach dem aktuellen Stand und Filter neu auf.
    /// </summary>
    private void Refresh()
    {
        _refreshPending = false;
        _entries = AnnotationOverview.Build(_document, _highlightedText);
        Count = _entries.Count;
        Items.Clear();
        var lastPage = -1;
        foreach (var entry in _entries.Where(entry => AnnotationOverview.Matches(entry, SelectedFilter.Category)))
        {
            if (entry.PageIndex != lastPage)
            {
                Items.Add(AnnotationListItem.Header(entry.PageIndex));
                lastPage = entry.PageIndex;
            }

            Items.Add(new AnnotationListItem(entry) { IsSelected = entry.Annotation.Id == SelectedId });
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>
    /// Neuer Filter: Liste neu aufbauen.
    /// </summary>
    partial void OnSelectedFilterChanged(AnnotationFilterOption value) => Refresh();
}

/// <summary>
/// Eintrag im Filter der Anmerkungsübersicht.
/// </summary>
/// <param name="Category">Art oder <c>null</c> für alle.</param>
public sealed record AnnotationFilterOption(AnnotationCategory? Category)
{
    /// <summary>Angezeigter Name.</summary>
    public string Name => Loc.T(Category is { } category ? $"Annotations.Filter.{category}" : "Annotations.Filter.All");

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// Zeile der Anmerkungsübersicht: Seitenüberschrift oder Anmerkung.
/// </summary>
public sealed partial class AnnotationListItem : ObservableObject
{
    private readonly int _pageIndex;

    /// <summary>
    /// Zeile für eine Anmerkung.
    /// </summary>
    /// <param name="entry">Der Eintrag.</param>
    public AnnotationListItem(AnnotationEntry entry)
    {
        Entry = entry;
        _pageIndex = entry.PageIndex;
    }

    /// <summary>
    /// Zeile für eine Seitenüberschrift.
    /// </summary>
    private AnnotationListItem(int pageIndex) => _pageIndex = pageIndex;

    /// <summary>Die Anmerkung oder <c>null</c> bei einer Überschrift.</summary>
    public AnnotationEntry? Entry { get; }

    /// <summary>Ob die Zeile eine Seitenüberschrift ist.</summary>
    public bool IsHeader => Entry is null;

    /// <summary>Ob die Zeile eine Anmerkung ist.</summary>
    public bool IsEntry => Entry is not null;

    /// <summary>Text der Überschrift, z. B. „Seite 3“.</summary>
    public string HeaderText => Loc.F("Annotations.Page", _pageIndex + 1).ToUpperInvariant();

    /// <summary>Farbe der Anmerkung.</summary>
    public string ColorHex => Entry?.Annotation.ColorHex ?? "#00000000";

    /// <summary>Fußnotenzahl mit folgendem Leerzeichen bei Kommentaren, sonst leer.</summary>
    public string NumberPrefix => Entry?.FootnoteNumber is { } number ? $"{number} " : string.Empty;

    /// <summary>
    /// Beschreibung: Kommentar- und Textinhalt direkt, Markierungen und Stempel mit ihrem Text, sonst die Art.
    /// </summary>
    public string Description => Entry switch
    {
        null => string.Empty,
        { Label: AnnotationLabel.Comment or AnnotationLabel.Text } entry => entry.Text,
        { Label: AnnotationLabel.Highlight or AnnotationLabel.Stamp, Text.Length: > 0 } entry => $"{Loc.T($"Annotations.Label.{entry.Label}")}: „{entry.Text}“",
        { } entry => Loc.T($"Annotations.Label.{entry.Label}"),
    };

    /// <summary>Ob die Beschreibung nur die Art nennt (wird gedämpft dargestellt).</summary>
    public bool IsKindOnly => Entry is { Text.Length: 0 } && Entry.Label is not AnnotationLabel.Comment and not AnnotationLabel.Text;

    /// <summary>Symbol: Kommentar.</summary>
    public bool IsComment => Entry?.Category == AnnotationCategory.Comment;

    /// <summary>Symbol: Markierung.</summary>
    public bool IsHighlight => Entry?.Category == AnnotationCategory.Highlight;

    /// <summary>Symbol: Freihand (ohne Unterschriften).</summary>
    public bool IsFreehand => Entry?.Category == AnnotationCategory.Freehand && !IsSignature;

    /// <summary>Symbol: Form (ohne Stempel, Bilder und Schwärzungen).</summary>
    public bool IsShape => Entry?.Category == AnnotationCategory.Shape && !IsStamp && !IsRedaction;

    /// <summary>Symbol: Unterschrift (wie in der Werkzeugleiste und an der Miniatur).</summary>
    public bool IsSignature => Entry?.Label == AnnotationLabel.Signature;

    /// <summary>Symbol: Stempel oder Bild.</summary>
    public bool IsStamp => Entry?.Label is AnnotationLabel.Stamp or AnnotationLabel.Image;

    /// <summary>Symbol: Schwärzung.</summary>
    public bool IsRedaction => Entry?.Label == AnnotationLabel.Redaction;

    /// <summary>Symbol: Text.</summary>
    public bool IsText => Entry?.Category == AnnotationCategory.Text;

    /// <summary>Ob die Anmerkung zuletzt angesprungen wurde.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// Erzeugt eine Seitenüberschrift.
    /// </summary>
    /// <param name="pageIndex">Position der Seite (0-basiert).</param>
    /// <returns>Die Zeile.</returns>
    public static AnnotationListItem Header(int pageIndex) => new(pageIndex);
}
