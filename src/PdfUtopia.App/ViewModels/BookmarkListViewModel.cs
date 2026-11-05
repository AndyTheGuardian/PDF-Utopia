using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.App.Localization;
using PdfUtopia.Core;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Lesezeichen eines Dokuments als Baum (A46), bezogen auf die aktuelle Seitenreihenfolge. Das Lesezeichen des
/// Kapitels der angezeigten Seite ist markiert; aufgeklappte Zweige bleiben beim Umsortieren aufgeklappt.
/// </summary>
public sealed class BookmarkListViewModel : ObservableObject
{
    private readonly EditableDocument _document;
    private readonly IReadOnlyList<Bookmark> _bookmarks;
    private readonly Dictionary<Bookmark, bool> _expanded = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<PlacedBookmark> _placed = [];

    /// <summary>
    /// Erzeugt die Liste.
    /// </summary>
    /// <param name="document">Das Dokument.</param>
    /// <param name="bookmarks">Lesezeichen der geöffneten Datei.</param>
    public BookmarkListViewModel(EditableDocument document, IReadOnlyList<Bookmark> bookmarks)
    {
        _document = document;
        _bookmarks = bookmarks;
        Refresh(0);
    }

    /// <summary>Oberste Ebene des Baums.</summary>
    public ObservableCollection<BookmarkItemViewModel> Items { get; } = [];

    /// <summary>Ob die Datei Lesezeichen hat; nur dann gibt es den Reiter.</summary>
    public bool HasAny => _bookmarks.Count > 0;

    /// <summary>
    /// Baut den Baum nach Umsortieren, Löschen oder Einfügen von Seiten neu auf.
    /// </summary>
    /// <param name="currentIndex">Angezeigte Seite.</param>
    public void Refresh(int currentIndex)
    {
        foreach (var item in Items.SelectMany(item => item.WithDescendants()))
        {
            _expanded[item.Bookmark.Source] = item.IsExpanded;
        }

        _placed = BookmarkMap.Place(_bookmarks, _document.Pages.ToList());
        Items.Clear();
        foreach (var bookmark in _placed)
        {
            Items.Add(Create(bookmark, depth: 0));
        }

        MarkCurrent(currentIndex);
    }

    /// <summary>
    /// Markiert das Lesezeichen des Kapitels der angezeigten Seite.
    /// </summary>
    /// <param name="currentIndex">Angezeigte Seite.</param>
    public void MarkCurrent(int currentIndex)
    {
        var current = BookmarkMap.Current(_placed, currentIndex);
        foreach (var item in Items.SelectMany(item => item.WithDescendants()))
        {
            item.IsCurrent = ReferenceEquals(item.Bookmark, current);
        }
    }

    /// <summary>
    /// Baut einen Zweig; die erste Ebene ist aufgeklappt, sonst gilt die Vorgabe der Datei bzw. der letzte Zustand.
    /// </summary>
    private BookmarkItemViewModel Create(PlacedBookmark bookmark, int depth) => new(
        bookmark,
        _expanded.TryGetValue(bookmark.Source, out var expanded) ? expanded : depth == 0 || bookmark.Source.IsOpen,
        bookmark.Children.Select(child => Create(child, depth + 1)).ToList());
}

/// <summary>
/// Ein Lesezeichen im Baum.
/// </summary>
public sealed partial class BookmarkItemViewModel : ObservableObject
{
    /// <summary>
    /// Erzeugt den Eintrag.
    /// </summary>
    /// <param name="bookmark">Das bezogene Lesezeichen.</param>
    /// <param name="isExpanded">Ob die Unterpunkte aufgeklappt sind.</param>
    /// <param name="children">Unterpunkte.</param>
    public BookmarkItemViewModel(PlacedBookmark bookmark, bool isExpanded, IReadOnlyList<BookmarkItemViewModel> children)
    {
        Bookmark = bookmark;
        IsExpanded = isExpanded;
        Children = children;
    }

    /// <summary>Das bezogene Lesezeichen.</summary>
    public PlacedBookmark Bookmark { get; }

    /// <summary>Unterpunkte.</summary>
    public IReadOnlyList<BookmarkItemViewModel> Children { get; }

    /// <summary>Titel.</summary>
    public string Title => Bookmark.Source.Title.Length > 0 ? Bookmark.Source.Title : Loc.T("Bookmarks.Untitled");

    /// <summary>Ob ein Klick zu einer Seite führt.</summary>
    public bool CanShow => Bookmark.PageIndex is not null;

    /// <summary>Ob die Zielseite gelöscht ist (Eintrag ausgegraut).</summary>
    public bool IsTargetMissing => Bookmark.IsTargetMissing;

    /// <summary>Hinweis: Zielseite oder „Seite gelöscht“.</summary>
    public string? ToolTip => Bookmark.PageIndex is { } index ? Loc.F("Annotations.Page", index + 1)
        : IsTargetMissing ? Loc.T("Bookmarks.Deleted")
        : null;

    /// <summary>Ob die Unterpunkte aufgeklappt sind.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Ob es das Lesezeichen des angezeigten Kapitels ist.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>
    /// Der Eintrag und alle Unterpunkte.
    /// </summary>
    /// <returns>Die Einträge in Lesereihenfolge.</returns>
    public IEnumerable<BookmarkItemViewModel> WithDescendants() => Children.SelectMany(child => child.WithDescendants()).Prepend(this);
}
