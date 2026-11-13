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

    /// <summary>
    /// Zeigt den Dialog „Texterkennung“ (A53) und führt die Erkennung mit Fortschritt aus.
    /// </summary>
    /// <param name="request">Angaben und Erkennung.</param>
    /// <returns>Das Ergebnis oder <c>null</c>, wenn nicht gestartet wurde.</returns>
    Task<OcrOutcome?> RecognizeTextAsync(OcrRequest request);

    /// <summary>
    /// Erklärt, wie Tesseract installiert wird (A53, F41).
    /// </summary>
    /// <returns><c>true</c> für „Erneut suchen“.</returns>
    Task<bool> ShowTesseractMissingAsync();

    /// <summary>Fragt nach dem Umgang mit ungespeicherten Änderungen.</summary>
    /// <param name="fileName">Dateiname.</param>
    /// <returns>Die Wahl.</returns>
    Task<UnsavedChangesChoice> AskUnsavedChangesAsync(string fileName);

    /// <summary>Zeigt eine Meldung.</summary>
    /// <param name="title">Titel.</param>
    /// <param name="message">Text.</param>
    /// <returns>Ein Task, der nach dem Schließen endet.</returns>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Zeigt die Einstellungen; Änderungen wirken sofort als Vorschau (A56).</summary>
    /// <param name="viewModel">Die Einstellungen.</param>
    /// <returns><c>true</c> bei „Übernehmen“, <c>false</c> bei „Abbrechen“.</returns>
    Task<bool> ShowSettingsAsync(ViewModels.SettingsViewModel viewModel);

    /// <summary>Fragt nach einer Bestätigung.</summary>
    /// <param name="title">Titel.</param>
    /// <param name="message">Frage.</param>
    /// <param name="confirmText">Beschriftung der bestätigenden Schaltfläche.</param>
    /// <returns><c>true</c> bei Bestätigung.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>Lässt zwischen mehreren Antworten wählen (neben „Abbrechen“).</summary>
    /// <param name="title">Titel.</param>
    /// <param name="message">Frage.</param>
    /// <param name="choices">Antworten; die erste ist hervorgehoben.</param>
    /// <returns>Position der gewählten Antwort oder <c>null</c> bei Abbruch.</returns>
    Task<int?> ChooseAsync(string title, string message, IReadOnlyList<string> choices);

    /// <summary>Fragt nach einem Text, z. B. dem Namen eines Farbprofils.</summary>
    /// <param name="title">Titel.</param>
    /// <param name="label">Beschriftung des Felds.</param>
    /// <param name="initial">Vorbelegung.</param>
    /// <param name="confirmText">Beschriftung des Bestätigungsknopfs.</param>
    /// <returns>Der Text oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PromptTextAsync(string title, string label, string initial, string confirmText);

    /// <summary>Wählt eine Datei zum Öffnen.</summary>
    /// <param name="title">Titel der Auswahl.</param>
    /// <param name="fileTypeName">Name des Dateityps im Filter.</param>
    /// <param name="pattern">Muster des Dateityps, z. B. <c>*.colorprofiles.json</c>.</param>
    /// <returns>Der Pfad oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PickOpenFileAsync(string title, string fileTypeName, string pattern);

    /// <summary>Wählt Ort und Namen einer neuen Datei; das System fragt vor dem Überschreiben.</summary>
    /// <param name="title">Titel der Auswahl.</param>
    /// <param name="suggestedName">Vorgeschlagener Dateiname.</param>
    /// <param name="fileTypeName">Name des Dateityps.</param>
    /// <param name="pattern">Muster des Dateityps.</param>
    /// <returns>Der Pfad oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PickSaveFileAsync(string title, string suggestedName, string fileTypeName, string pattern);
}
