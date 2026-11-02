namespace PdfUtopia.Core.Pages;

/// <summary>
/// Art eines Hinweises zur Seitenauswahl beim Extrahieren.
/// </summary>
public enum PageSelectionHintKind
{
    /// <summary>Teil der Eingabe ist keine Seitenzahl und kein Bereich; er wird ignoriert.</summary>
    InvalidToken,

    /// <summary>Bereich war absteigend angegeben (z. B. „8-4“) und wird aufsteigend gelesen.</summary>
    ReversedRange,

    /// <summary>Seitenzahl liegt über der Seitenanzahl und wird auf die letzte Seite begrenzt.</summary>
    PageClamped,

    /// <summary>Seite wurde mehrfach angegeben und wird nur einmal übernommen.</summary>
    DuplicatePage,
}

/// <summary>
/// Hinweis zur Auswertung der Seitenauswahl. Die Oberfläche formuliert daraus den Text in der gewählten Sprache.
/// </summary>
/// <param name="Kind">Art des Hinweises.</param>
/// <param name="Token">Betroffener Teil der Eingabe, z. B. „8-4“ (bei <see cref="PageSelectionHintKind.DuplicatePage"/> leer).</param>
/// <param name="Page">Betroffene Seite: angegebene Seite bei Begrenzung, doppelte Seite bei Duplikat.</param>
/// <param name="RangeStart">Bereichsanfang nach dem Tauschen (nur <see cref="PageSelectionHintKind.ReversedRange"/>).</param>
/// <param name="RangeEnd">Bereichsende nach dem Tauschen bzw. verwendete letzte Seite bei Begrenzung.</param>
public sealed record PageSelectionHint(
    PageSelectionHintKind Kind,
    string Token,
    int? Page = null,
    int? RangeStart = null,
    int? RangeEnd = null);
