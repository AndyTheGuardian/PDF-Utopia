using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Signatures;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.App.Services;

/// <summary>
/// Antwort auf die Frage nach ungespeicherten Änderungen.
/// </summary>
public enum UnsavedChangesChoice
{
    /// <summary>Vorgang abbrechen.</summary>
    Cancel,

    /// <summary>Erst speichern.</summary>
    Save,

    /// <summary>Änderungen verwerfen.</summary>
    Discard,
}

/// <summary>
/// Eine Datei, aus der Seiten eingefügt werden können (A41).
/// </summary>
/// <param name="Path">Vollständiger Pfad.</param>
/// <param name="DisplayName">Anzeigename.</param>
/// <param name="KnownPassword">Bekanntes Passwort (geöffnetes Dokument) oder <c>null</c>.</param>
/// <param name="HasUnsavedChanges">Ob das geöffnete Dokument ungespeicherte Änderungen hat; eingefügt wird der gespeicherte Stand.</param>
/// <param name="IsOpenDocument">Ob die Datei in einem Tab geöffnet ist.</param>
public sealed record InsertSourceOption(string Path, string DisplayName, string? KnownPassword, bool HasUnsavedChanges, bool IsOpenDocument);

/// <summary>
/// Vorgaben des Einfügen-Dialogs.
/// </summary>
/// <param name="Sources">Wählbare Quellen; die erste ist vorgewählt.</param>
/// <param name="CurrentIndex">Angezeigte Seite des Zieldokuments (0-basiert).</param>
/// <param name="PageCount">Seitenanzahl des Zieldokuments.</param>
/// <param name="Position">Vorgewählte Position.</param>
/// <param name="AfterPage">Seite bei <see cref="InsertPosition.AfterPage"/>.</param>
public sealed record InsertRequest(IReadOnlyList<InsertSourceOption> Sources, int CurrentIndex, int PageCount, InsertPosition Position, int AfterPage);

/// <summary>
/// Ergebnis des Einfügen-Dialogs.
/// </summary>
/// <param name="Source">Die geöffnete Quelldatei.</param>
/// <param name="Pages">Seitennummern der Quelle (1-basiert), in Zielreihenfolge.</param>
/// <param name="Index">Position der ersten neuen Seite im Zieldokument.</param>
public sealed record InsertChoice(PdfSource Source, IReadOnlyList<int> Pages, int Index);

/// <summary>
/// Ergebnis des Einstellungsdialogs.
/// </summary>
/// <param name="Theme">Erscheinungsbild.</param>
/// <param name="Language">Sprache.</param>
/// <param name="UndoLimit">Höchstzahl der Rückgängig-Schritte.</param>
/// <param name="UserName">Name für Stempel (F35).</param>
public sealed record SettingsChoice(ThemePreference Theme, UiLanguage Language, int UndoLimit, string UserName);

/// <summary>
/// Dialoge und Dateiauswahl, entkoppelt vom ViewModel (testbar, austauschbar).
/// </summary>
public interface IDialogService
{
    /// <summary>Wählt PDF-Dateien zum Öffnen.</summary>
    /// <returns>Die lokalen Pfade (leer bei Abbruch).</returns>
    Task<IReadOnlyList<string>> PickPdfFilesAsync();

    /// <summary>Wählt das Ziel für „Speichern unter“.</summary>
    /// <param name="suggestedName">Vorgeschlagener Dateiname.</param>
    /// <returns>Der lokale Pfad oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PickSaveFileAsync(string suggestedName);

    /// <summary>Fragt nach dem Passwort einer geschützten Datei.</summary>
    /// <param name="fileName">Dateiname für die Meldung.</param>
    /// <param name="previousAttemptWrong">Ob der vorige Versuch falsch war.</param>
    /// <returns>Das Passwort oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PromptPasswordAsync(string fileName, bool previousAttemptWrong);

    /// <summary>Fragt, wie der Passwortschutz beim Speichern behandelt wird (F22).</summary>
    /// <param name="sourceIsProtected">Ob die Quelldatei geschützt ist.</param>
    /// <returns>Modus und ggf. neues Passwort, oder <c>null</c> bei Abbruch.</returns>
    Task<(PasswordMode Mode, string? NewPassword)?> AskPasswordModeAsync(bool sourceIsProtected);

    /// <summary>Zeigt den Extract-Dialog mit Live-Vorschau.</summary>
    /// <param name="pageCount">Seitenanzahl der aktuellen Seitenliste.</param>
    /// <param name="preset">Vorbelegte Eingabe (z. B. die Mehrfachauswahl) oder <c>null</c> für alle Seiten.</param>
    /// <returns>Die Seitenpositionen oder <c>null</c> bei Abbruch.</returns>
    Task<IReadOnlyList<int>?> AskPagesToExtractAsync(int pageCount, string? preset = null);

    /// <summary>
    /// Zeigt die Dateiauswahl für eine einzelne PDF-Datei.
    /// </summary>
    /// <returns>Der Pfad oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PickPdfFileAsync();

    /// <summary>
    /// Fragt nach Quelle, Seiten und Position zum Einfügen von Seiten aus einer anderen Datei (A41). Der Dialog
    /// öffnet die gewählte Quelle selbst und fragt bei Bedarf nach ihrem Passwort.
    /// </summary>
    /// <param name="request">Quellen und Vorgaben.</param>
    /// <returns>Die Auswahl (die Datei geht an den Aufrufer über) oder <c>null</c> bei Abbruch.</returns>
    Task<InsertChoice?> AskPagesToInsertAsync(InsertRequest request);

    /// <summary>
    /// Zeigt den Dialog „Unterschriften“ (A49): bis zu fünf Unterschriften schreiben, benennen und löschen.
    /// </summary>
    /// <param name="signatures">Gespeicherte Unterschriften.</param>
    /// <param name="startSlot">Zuerst gewählter Platz.</param>
    /// <param name="persist">Speichert die Unterschriften nach jeder Änderung.</param>
    /// <returns>Die Unterschriften nach dem Schließen.</returns>
    Task<IReadOnlyList<Signature>> EditSignaturesAsync(IReadOnlyList<Signature> signatures, int startSlot, Action<IReadOnlyList<Signature>> persist);

    /// <summary>
    /// Wählt ein Bild (PNG oder JPEG) zum Einfügen (A51).
    /// </summary>
    /// <returns>Der Pfad oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PickImageFileAsync();

    /// <summary>
    /// Fragt nach Text und Farbe eines eigenen Stempels (A51).
    /// </summary>
    /// <param name="detail">Zweite Zeile (Datum, Name) für die Vorschau.</param>
    /// <returns>Der Stempel oder <c>null</c> bei Abbruch.</returns>
    Task<CustomStamp?> AskCustomStampAsync(string detail);

    /// <summary>
    /// Liest ein Bild aus der Zwischenablage (A51).
    /// </summary>
    /// <returns>Das Bild oder <c>null</c>, wenn die Zwischenablage keines enthält.</returns>
    Task<LoadedImage?> ReadClipboardImageAsync();

    /// <summary>
    /// Fragt vor dem Speichern, Extrahieren oder Drucken, ob die Schwärzungen angewendet werden (A52).
    /// </summary>
    /// <param name="pageNumbers">Betroffene Seiten (1-basiert, in der gespeicherten Datei).</param>
    /// <param name="areaCount">Anzahl der Bereiche.</param>
    /// <param name="confirmText">Text der bestätigenden Schaltfläche.</param>
    /// <returns><c>true</c>, wenn geschwärzt werden soll.</returns>
    Task<bool> ConfirmRedactionAsync(IReadOnlyList<int> pageNumbers, int areaCount, string confirmText);

    /// <summary>Zeigt die Einstellungen.</summary>
    /// <param name="current">Aktuelle Werte.</param>
    /// <returns>Die neuen Werte oder <c>null</c> bei Abbruch.</returns>
    Task<SettingsChoice?> EditSettingsAsync(SettingsChoice current);

    /// <summary>Fragt nach dem Umgang mit ungespeicherten Änderungen.</summary>
    /// <param name="fileName">Dateiname.</param>
    /// <returns>Die Wahl.</returns>
    Task<UnsavedChangesChoice> AskUnsavedChangesAsync(string fileName);

    /// <summary>Zeigt eine Meldung.</summary>
    /// <param name="title">Titel.</param>
    /// <param name="message">Text.</param>
    /// <returns>Ein Task, der nach dem Schließen endet.</returns>
    Task ShowMessageAsync(string title, string message);
}
