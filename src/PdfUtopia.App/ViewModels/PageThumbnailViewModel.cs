using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Eintrag der Seitenliste links: Miniatur, Position und – nach dem Umsortieren – die ursprüngliche Seitennummer.
/// </summary>
/// <param name="entry">Die Seite.</param>
/// <param name="position">Position in der Liste (1-basiert).</param>
public sealed partial class PageThumbnailViewModel(PageEntry entry, int position) : ObservableObject
{
    /// <summary>Die Seite.</summary>
    public PageEntry Entry { get; } = entry;

    /// <summary>Position in der Liste (1-basiert).</summary>
    public int Position { get; } = position;

    /// <summary>Beschriftung unter der Miniatur.</summary>
    public string Label => Position.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Ursprüngliche Seitennummer, falls die Seite verschoben wurde, sonst leer.</summary>
    public string OriginalHint => Entry.SourcePageNumber == Position ? string.Empty : $"({Loc.F("Pages.Original", Entry.SourcePageNumber)})";

    /// <summary>Miniatur; wird im Hintergrund gerendert.</summary>
    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    /// <summary>Ob die Seite gerade angezeigt wird.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Fußnote eines Kommentars unter der Seite; der Text ist direkt bearbeitbar.
/// </summary>
/// <param name="commentId">Kennung des Kommentars.</param>
/// <param name="number">Fußnotenzahl.</param>
/// <param name="colorHex">Kommentarfarbe.</param>
/// <param name="text">Fußnotentext.</param>
/// <param name="commit">Übernimmt einen geänderten Text ins Dokument (rückgängig machbar).</param>
public sealed partial class FootnoteViewModel(Guid commentId, int number, string colorHex, string text, Action<Guid, string> commit)
    : ObservableObject
{
    /// <summary>Kennung des Kommentars.</summary>
    public Guid CommentId { get; } = commentId;

    /// <summary>Fußnotenzahl.</summary>
    public int Number { get; } = number;

    /// <summary>Kommentarfarbe.</summary>
    public string ColorHex { get; } = colorHex;

    /// <summary>Rahmenfarbe des Zahlenkreises (grau bei zu hellen Farben).</summary>
    public string BorderHex { get; } = Core.Theming.DrawingColors.CommentBorder(colorHex);

    /// <summary>Schriftfarbe der Zahl.</summary>
    public string NumberForegroundHex { get; } = Core.Theming.DrawingColors.FootnoteNumberForeground(colorHex);

    /// <summary>Fußnotentext.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = text;

    /// <summary>
    /// Übernimmt den bearbeiteten Text, sobald das Feld verlassen wird.
    /// </summary>
    public void Commit() => commit(CommentId, Text);
}
