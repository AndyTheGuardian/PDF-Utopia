# PDF Utopia – Entwicklerdokumentation

Stand: 2026-11-07

Diese Dokumentation richtet sich an Entwicklerinnen und Entwickler, die PDF Utopia bauen, verstehen oder erweitern
wollen. Bedienung und Regeln aus Anwendersicht stehen in der [Anwenderdokumentation](ANWENDERDOKUMENTATION.md) und im
[Benutzerhandbuch](BENUTZERHANDBUCH.md); Entscheidungen, Annahmen und deren Begründung in
[OFFENE-PUNKTE.md](../OFFENE-PUNKTE.md) (IDs wie A51, F38, P10 verweisen dorthin).

**Inhalt**

1. [Überblick](#1-überblick)
2. [Bauen, Starten, Testen, Veröffentlichen](#2-bauen-starten-testen-veröffentlichen)
3. [Projektaufbau](#3-projektaufbau)
4. [Kernkonzepte](#4-kernkonzepte)
5. [Lesen einer Datei](#5-lesen-einer-datei)
6. [Schreiben einer Datei](#6-schreiben-einer-datei)
7. [Die Oberfläche](#7-die-oberfläche)
8. [Tests](#8-tests)
9. [Eine neue Anmerkungsart hinzufügen](#9-eine-neue-anmerkungsart-hinzufügen)
10. [Konventionen und Arbeitsweise](#10-konventionen-und-arbeitsweise)
11. [Fallstricke](#11-fallstricke)

---

## 1. Überblick

PDF Utopia ist ein plattformübergreifender PDF-Editor (Windows, Linux, macOS) in C# mit .NET 10 und Avalonia 12.
Schwerpunkt sind Anmerkungen (Kommentare, Marker, Freihand, Formen, Text, Unterschrift, Stempel, Bilder),
Seitenbearbeitung, Formulare, Schwärzen und Texterkennung.

Grundprinzipien:

- **Das Original wird nie verändert.** Gespeichert wird immer in eine neue Datei („Speichern unter“), atomar über eine
  temporäre Datei.
- **Bearbeitung ist ein Modell, keine PDF-Manipulation.** Seitenliste, Drehungen, Anmerkungen, Formularwerte und
  erkannter Text leben im Core (`EditableDocument`). Erst beim Speichern entsteht daraus eine neue PDF-Datei.
- **Echte PDF-Annotationen mit Erscheinungsbild.** Jede Anmerkung wird als Standard-Annotation (`/Ink`, `/Square`,
  `/FreeText`, `/Stamp` …) mit eigenem Appearance-Stream geschrieben, damit jeder Betrachter sie gleich zeigt. Zusätzlich
  trägt sie ihre Daten als JSON, damit PDF Utopia sie beim Wiederöffnen bearbeitbar zurückliest.
- **Jede Änderung ist ein Rückgängig-Schritt** über `UndoHistory`.

### Abhängigkeiten

| Bibliothek | Version | Zweck | Hinweis |
|---|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | 12.1.4 | Oberfläche | |
| CommunityToolkit.Mvvm | 8.4.2 | `ObservableProperty`, `RelayCommand` (Quellgeneratoren, `partial`-Properties) | |
| PDFiumCore | 157.0.8100 | Rendern, Text, Formular-Anzeige, Lesezeichen | bringt PDFium für alle drei Systeme mit |
| PDFsharp | **6.2.4** | Schreiben, Import, Passwortschutz | **nicht aktualisieren**: ab 6.3 AGPL mit Lizenzprüfung zur Laufzeit (A21) |
| System.Security.Cryptography.Pkcs | 10.0.14 | direkt angehoben | Sicherheitslücke in der PDFsharp-Abhängigkeit 8.0.1 (A21) |
| xunit.v3 | 4.0.2 | Tests (Microsoft Testing Platform) | |
| Tesseract | extern, ≥ 4 | Texterkennung | separat installiertes Programm, kein Paket (F41) |

---

## 2. Bauen, Starten, Testen, Veröffentlichen

Voraussetzung: .NET SDK 10. Alle Befehle im Projektordner.

```bash
dotnet build PdfUtopia.slnx
dotnet run --project src/PdfUtopia.App -- testdata/testdatei.pdf
dotnet test --solution PdfUtopia.slnx
```

`global.json` stellt `dotnet test` auf die Microsoft Testing Platform um (daher `--solution`). Warnungen gelten als
Fehler (`TreatWarningsAsErrors` in `Directory.Build.props`), ebenso die xUnit-Analysatoren (z. B. xUnit1051:
`TestContext.Current.CancellationToken` an asynchrone Aufrufe weitergeben).

Programmpaket als einzelne Datei (D4), Beispiel Linux:

```bash
dotnet publish src/PdfUtopia.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/single-linux-x64
```

Die nativen Bibliotheken (PDFium, Skia) werden beim ersten Start nach `~/.cache/dotnet_bundle_extract/PdfUtopia`
entpackt. Eine laufende Einzeldatei nicht überschreiben – sie liest sich zur Laufzeit selbst.

**Zum Testen mit eigenen Einstellungen** (ohne die echten Einstellungen und Unterschriften anzufassen) unter Linux
`XDG_CONFIG_HOME` auf einen eigenen Ordner setzen:

```bash
XDG_CONFIG_HOME=/tmp/pdfutopia-test dotnet run --project src/PdfUtopia.App
```

---

## 3. Projektaufbau

```
PdfUtopia.slnx
├─ src/
│  ├─ PdfUtopia.Core   – Modell und Regeln, ohne Oberfläche und ohne PDF-Bibliothek
│  ├─ PdfUtopia.Pdf    – PDFium (Lesen/Rendern) und PDFsharp (Schreiben), Tesseract-Anbindung
│  └─ PdfUtopia.App    – Avalonia-Oberfläche (MVVM)
├─ tests/
│  ├─ PdfUtopia.Core.Tests
│  └─ PdfUtopia.Pdf.Tests  – erzeugt Test-PDFs, prüft Struktur, Text und gerenderte Pixel
├─ testdata/   – erzeugte Testdateien (siehe Abschnitt 8)
├─ mockups/    – abgenommene HTML-Mockups v1–v9
├─ spikes/     – Prototypen (z. B. PressureProbe für Stift-Eingaben)
└─ docs/       – Anwender-, Benutzer- und Entwicklerdokumentation, Abnahme-Checkliste
```

Abhängigkeitsrichtung: **App → Pdf → Core**, App auch direkt → Core. Der Core kennt weder Avalonia noch PDFium oder
PDFsharp; dadurch ist die Fachlogik ohne Oberfläche testbar.

### PdfUtopia.Core

| Ordner | Inhalt |
|---|---|
| `EditableDocument.cs` | Bearbeitungsmodell eines Dokuments: Seitenliste, Anmerkungen, Formularwerte, erkannter Text, Rückgängig, „geändert“-Zustand |
| `Annotations/` | Anmerkungsarten (unveränderliche Records), `AnnotationStore` (je Seite), `AnnotationBounds`, `AnnotationOverview` (Anmerkungsübersicht) |
| `Drawing/` | Stifteingabe (`StrokeRecorder`, `InputSample`), Glättung (`StrokeGeometry`), Radierer (`EraserHitTest`) |
| `Geometry/` | `PdfPoint`, `PdfRect`, `PageTransform` (Seitendrehung), `ZoomFit` |
| `Pages/` | `PageList`/`PageEntry`, Mehrfachauswahl, Seitenauswahl-Parser („2,4-8“), Einfügeposition, Lesezeichen-Zuordnung |
| `Forms/` | Formularfelder und -werte (A50) |
| `Images/` | `ImageData` (eingefügte Bilder), `JpegInfo` |
| `Text/` | Helvetica-Metriken, Zeilenumbruch, erkannter Text (`RecognizedPage`, `TesseractTsv`) |
| `History/` | `UndoHistory`, `DelegateCommand` |
| `Settings/` | `AppSettings` (JSON, mit `Normalized()`), `SettingsStore` |
| `Signatures/` | gespeicherte Unterschriften und ihre Datei |
| `Theming/` | Akzentpalette, Zeichenfarben |

### PdfUtopia.Pdf

| Ordner | Inhalt |
|---|---|
| `Rendering/PdfSource.cs` | eine geöffnete Datei: Laden (mit Passwort), Rendern, Text, Suche, Absätze, Formularumgebung, Lesezeichen, eingelesene eigene Anmerkungen |
| `Rendering/PdfiumLibrary.cs` | Initialisierung und **globale Sperre** für alle PDFium-Aufrufe |
| `Import/` | `SavedAnnotationImporter` (eigene Anmerkungen zurücklesen, A25), `FormReader` |
| `Export/` | `PdfExporter` (Speichern/Extrahieren), `AnnotationWriter`, `ContentBuilder`, `FootnoteLayout`, `FormRebuilder`, `Redactor`, `RecognizedTextWriter`, `PageGeometry`, `StandardFont`, `AnnotationSerializer` |
| `Ocr/TesseractEngine.cs` | Tesseract finden, Seite erkennen (A53) |

### PdfUtopia.App

| Ordner | Inhalt |
|---|---|
| `ViewModels/` | `MainWindowViewModel` (Befehle, Tabs, Einstellungen), `DocumentViewModel` (ein Tab: Quellen, aktuelle Seite, Miniaturen, Text-Zugriff), `ToolOptionsViewModel` (Werkzeug und Optionsleiste), Anmerkungs- und Lesezeichenliste, Farbwahl |
| `Controls/PageEditor*.cs` | die Seitenansicht: Zeichnen, Eingabe aller Werkzeuge; aufgeteilt in `partial`-Dateien (Formulare, Stempel/Bilder, Schwärzen) |
| `Controls/` | `BoxRenderer`/`StampPreview`, `StrokeRenderer`, `SignaturePad`/`SignaturePreview`, `TabStripPanel` |
| `Services/` | `DialogService` (alle Dialoge, hinter `IDialogService`), Dialoge als eigene Klassen, `PageRenderer` (Seiten mit Zwischenspeicher), `ImageLoader`, `ThemeService` |
| `Views/` | `MainWindow.axaml`, Farbwahl, `ThumbnailDragController` (Umsortieren per Ziehen) |
| `Localization/` | `Loc` und `Strings` (Deutsch/Englisch) |

---

## 4. Kernkonzepte

### 4.1 Koordinaten

Alle Koordinaten im Core sind **PDF-Punkte (1/72 Zoll), Ursprung links oben, Y nach unten**, bezogen auf die Seite
**mit der Drehung aus der Datei, aber ohne die Drehung aus der Bearbeitung**. Das ist die Ansicht, die PDFium für eine
Seite ohne zusätzliche Drehung liefert (`PdfSource.PageSize`).

- `PageTransform.Rotate/Unrotate` rechnen zwischen dieser „ungedrehten“ Seite und der angezeigten Seite mit
  Bearbeitungsdrehung (`PageEntry.Rotation`, 90°-Schritte im Uhrzeigersinn) um – für Punkte, Rechtecke und ganze
  Anmerkungen.
- Der `PageEditor` arbeitet in Anzeige-Koordinaten (`ToDisplay`/`ToPage`); der Zoom liegt außen als
  `LayoutTransform`.
- Beim Schreiben rechnet `PageGeometry` in PDF-Benutzerkoordinaten um (Ursprung links unten, Dateidrehung, CropBox).

### 4.2 Das Bearbeitungsmodell

`EditableDocument` hält:

- `Pages` – die aktuelle Seitenliste aus `PageEntry(Id, SourcePageNumber, Rotation, SourceIndex)`. Die `Id` ist stabil
  über Umsortieren und Löschen; Anmerkungen hängen an ihr. `SourceIndex` 0 ist die geöffnete Datei, höhere Werte sind
  eingefügte Dateien (A41).
- `Annotations` – `AnnotationStore` je Seiten-Id, in Zeichenreihenfolge.
- `Form` – Formularwerte (A50), `RecognizedText` – erkannter Text je Seiten-Id (A53).
- `History` – `UndoHistory`. **Jede Änderung läuft über `History.Execute(new DelegateCommand(do, undo))`.**
  `IsModified` vergleicht den aktuellen Verlaufszustand mit dem gespeicherten (`MarkSaved`).

### 4.3 Anmerkungen

Anmerkungen sind **unveränderliche Records** mit `Id` und `ColorHex`. Ändern heißt ersetzen (`UpdateAnnotation` mit
`with`-Kopie, gleiche `Id`). Arten:

| Typ | Gespeichert als | Besonderheit |
|---|---|---|
| `InkAnnotation` | `/Ink` | Punkte mit Andruck, Glättung |
| `HighlightAnnotation` | `/Highlight` | Multiplizieren, halbtransparent |
| `RectangleAnnotation`, `EllipseAnnotation`, `LineAnnotation` | `/Square`, `/Circle`, `/Line` | Pfeilspitzen (A44) |
| `CommentAnnotation` | `/Square` + `/FreeText` als Fußnote | Seite wird für Fußnoten verlängert |
| `TextAnnotation` | `/FreeText` | Umbruch mit Helvetica-Metriken, steht aufrecht zur Drehung beim Anlegen |
| `SignatureAnnotation` | `/Ink` | sichtbare Unterschrift, keine digitale Signatur (A49) |
| `StampAnnotation`, `ImageAnnotation` (Basis `BoxAnnotation`) | `/Stamp` | Rechteck um die Mitte gedreht (`Angle`), Eckgriffe, Drehgriff (A51, F37) |
| `RedactionAnnotation` | **nicht als Annotation** | wird beim Speichern ins Seitenbild geschrieben (A52) |

Damit Anzeige und Datei gleich aussehen, liegen gemeinsame Maße im Core (z. B. `AnnotationShape.CornerRadius`,
`StampLayout`, `Helvetica`-Breiten, `StrokeGeometry`). Die App zeichnet Text mit „Liberation Sans“, die metrisch
Helvetica entspricht.

### 4.4 Rückgängig

`UndoHistory` begrenzt die Schritte (1–500, Standard 50). Mehrere Änderungen, die zusammengehören, sind **ein**
Befehl: z. B. `RemoveAnnotations` (Radierstrich), `AddAnnotations` (Suchen und schwärzen), `SetRecognizedText`
(Texterkennung über mehrere Seiten). Dateien selbst werden nie rückgängig gemacht; nur das Modell.

---

## 5. Lesen einer Datei

`PdfSource.Open(path, password, importSavedAnnotations: true)`:

1. Datei lesen, mit PDFium laden (Passwortfehler → `PdfOpenException`).
2. **Eigene Anmerkungen zurücklesen** (`SavedAnnotationImporter`): Annotationen mit `/PdfUtopia true` werden aus der
   Datei entfernt und aus `/PdfUtopiaData` (JSON, `AnnotationSerializer`) wieder zu Core-Records; Bilddaten kommen aus
   dem Bild-XObject (`/PdfUtopiaImage`). Eine Seitenverlängerung für Fußnoten (`/PdfUtopiaExtension`) wird
   zurückgenommen. Die bereinigte Datei wird neu geladen – PDFium zeigt dann nur den Originalinhalt, der Editor zeichnet
   die Anmerkungen selbst. Fremde Annotationen bleiben unangetastet und werden von PDFium gezeichnet.
3. Formularfelder lesen (`FormReader`), PDFium-Formularumgebung einrichten (Felder werden sonst nicht gezeichnet, P8).
4. `CreateDocument()` baut das `EditableDocument` mit den eingelesenen Anmerkungen und Formularwerten.

**PDFium ist nicht threadsicher.** Jeder Zugriff läuft unter `PdfiumLibrary.Sync` (in `PdfSource.WithPage`/
`WithTextPage`). Rendern im Hintergrund ist dadurch möglich, aber serialisiert.

Textfunktionen von `PdfSource`: `PageText`, `FindText` (Rechtecke je Zeile mit Rand), `TextBlocks` (Absätze für
den Kommentar-Vorschlag), `TextInArea` (Text unter einer Markierung). Die App fragt Text über `DocumentViewModel`
ab, das die Textebene der Datei mit erkanntem Text verbindet (`PageText`, `FindText`, `HasNoText`).

---

## 6. Schreiben einer Datei

`PdfExporter.Save` / `Extract` → `Write`:

1. Für jede Seite der Zielliste die Quellseite mit PDFsharp importieren (je Quelldatei einmal geöffnet).
2. `AnnotationWriter.Write`: jede Anmerkung als Annotation mit Appearance-Stream (`ContentBuilder` erzeugt die
   Zeichenbefehle), `/NM` = Id, `/PdfUtopia true`, `/PdfUtopiaData` = JSON in der gespeicherten Drehung. Kommentare
   ergeben Fußnoten; nötigenfalls wird die Seite nach unten verlängert (`FootnoteLayout`, `Extend`).
3. `/Rotate` = Dateidrehung + Bearbeitungsdrehung.
4. Lesezeichen auf die neue Seitenfolge umstellen (P6), Formular-Registrierung `/AcroForm` neu aufbauen und geänderte
   Werte mit Erscheinungsbild schreiben (`FormRebuilder`, P7, A50).
5. **Schwärzen** (`Redactor`, A52): Gibt es Schwärzungen, wird die Datei zuerst vollständig in den Speicher geschrieben,
   mit PDFium in 300 dpi gerendert (mit Anmerkungen und Feldern), die Bereiche werden schwarz in die Pixel geschrieben
   und die betroffenen Seiten durch das Bild ersetzt (neue MediaBox in Anzeigegröße, `/Rotate 0`, ohne `/Annots`,
   Ressourcen, Vorschaubild, Seiten-Metadaten). Formularfelder dieser Seiten werden aus `/AcroForm` entfernt,
   `/StructTreeRoot` und `/MarkInfo` gelöscht. Nicht mehr erreichbare Objekte schreibt PDFsharp nicht mit – das prüft
   `RedactionTests.Extract_OnlyRedactedPage_LeavesNoFontsOrTextInTheFile`.
6. **Erkannter Text** (`RecognizedTextWriter`, A53): als unsichtbare Textebene (Textmodus 3), zeilenweise mit
   gemeinsamer Grundlinie, jedes Wort mit `Tz` auf seine Breite gestreckt. Erst nach dem Schwärzen geschrieben; Wörter,
   die eine Schwärzung berühren, entfallen.
7. Passwortschutz nach `ExportOptions.PasswordMode` (F22), dann atomar speichern (temporäre Datei, `File.Move`).

Texte in Erscheinungsbildern nutzen die nicht eingebetteten Standardschriften Helvetica/Helvetica-Bold mit
WinAnsi-Kodierung (`StandardFont`). Zeichen außerhalb davon werden zu „?“ (E5).

---

## 7. Die Oberfläche

### 7.1 MVVM

- `MainWindowViewModel` ist der Einstiegspunkt: offene Dokumente (Tabs), Befehle (Menü, Werkzeugleiste,
  Tastenkürzel), Einstellungen (`SettingsStore`), Unterschriften, Stempel, Schwärzen, Texterkennung.
- `DocumentViewModel` je Tab: Quellen, `EditableDocument`, aktuelle Seite, die Zeilen der Seitenansicht (`Rows`),
  `PageRenderer`, Miniaturen, Anmerkungs- und Lesezeichenliste, Suche, `SelectedBox` (angeklickter
  Stempel/Bild/Schwärzung), Text-Zugriff. `Show(index)` zeigt eine Seite gezielt an (löst `PageShown` aus, die Ansicht
  scrollt dorthin), `Activate(index)` macht sie nur zur aktuellen Seite (beim Scrollen und Arbeiten auf einer anderen
  sichtbaren Seite).
- `PageViewModel` je angezeigter Seite (A55): Bild, Annotationen, Fußnoten, Suchtreffer, Absätze, Hinzufügen und
  Entfernen von Annotationen. Gefunden wird die Seite über ihre Kennung, nicht über die Position.
  `PageRowViewModel` ist eine Zeile der Ansicht (eine Seite oder ein Seitenpaar, mit Lücke neben einer einzelnen
  ersten bzw. letzten Seite). Die Aufteilung berechnet `PdfUtopia.Core.Pages.PageLayout` (`PageViewMode`, `PageRow`).
- `ToolOptionsViewModel`: gewähltes Werkzeug (`Tool`) und alles, was die Optionsleiste zeigt; dauerhafte Werte meldet
  es über `Changed` zum Speichern.
- Dialoge laufen über `IDialogService`; die ViewModels kennen keine Fenster.

Befehle mit `CanExecute = nameof(HasDocument)` müssen in der `NotifyCanExecuteChangedFor`-Liste von
`ActiveDocument` stehen, sonst bleiben sie nach dem Öffnen eines Dokuments gesperrt (siehe A53, „Dabei behoben“).

### 7.2 PageEditor

Ein eigenes `Control`, das die gerenderte Seite und alle Anmerkungen zeichnet (`Render`) und Zeigereingaben
verarbeitet. Jeder Editor gehört zu einer Seite (`Page`, ein `PageViewModel`); in den fortlaufenden Ansichten und bei
Doppelseite gibt es mehrere. Ein Druck auf eine nicht aktuelle Seite ruft zuerst `DocumentViewModel.Activate` auf.
Die Zeilen stehen in einem `ItemsControl` mit `VirtualizingStackPanel` innerhalb der Zoom-`LayoutTransformControl`:
nur sichtbare Zeilen haben einen Editor. Container werden für andere Seiten wiederverwendet; beim Wechsel der Seite
und beim Abhängen übernimmt der Editor einen offenen Text und gibt das Bild frei. Hervorhebungen aus der
Anmerkungsübersicht merkt sich das `PageViewModel`, bis ein Editor für die Seite da ist. Ablauf einer Eingabe: `OnPointerPressed` → `Begin(tool)` → `Continue` → `Finish`, je Werkzeug ein Zweig.
Eingaben gelten nur bei Kontakt und sind nicht an Zeiger-ID oder Gerätetyp gebunden (A12); das Radiererende des
Stifts wirkt unabhängig vom Werkzeug (A27).

Erweiterungen liegen in `partial`-Dateien: `PageEditor.Forms.cs` (Eingabefelder über Formularfeldern),
`PageEditor.Stamps.cs` (Setzen, Griffe, Drehen, Bild ablegen), `PageEditor.Redactions.cs`. Das Textwerkzeug blendet
eine `TextBox` als Kind ein; sie bleibt offen, solange der Fokus nur in Optionsleiste oder Farbwahl wechselt, und wird
vor Speichern/Extrahieren/Drucken/Schließen über `DocumentViewModel.CommitEdits` übernommen (P10, P16).

### 7.3 Rendern

`PageRenderer` rendert Seiten passend zum Zoom scharf und hält die letzten 24 Ergebnisse (Datei, Seite, Drehung,
Maßstab) zwischengespeichert. Angezeigte Bitmaps hält ein `PageViewModel` mit `Pin`/`Unpin` fest: Fallen sie aus dem
Zwischenspeicher, werden sie erst entsorgt, wenn keine Seite sie mehr zeigt; `DocumentViewModel.RenderThumbnailsAsync` erzeugt die Miniaturen im Hintergrund. Formularfelder
zeichnet PDFium nur, wenn die Seite nicht ausfüllbar dargestellt wird (sonst liegen echte Eingabefelder darüber).

### 7.4 Lokalisierung und Theme

Texte stehen als Paare (Deutsch, Englisch) in `Strings.All`, abgerufen mit `Loc.T(key)` / `Loc.F(key, …)` bzw. in
XAML mit `{loc:T Key}`. Ein Sprachwechsel baut das Hauptfenster neu auf (`App.RebuildMainWindow`). `ThemeService` setzt Hell/Dunkel und die
Akzentfarbe als Ressourcen (`AccentBorderBrush`, `AccentSoftBrush` …).

### 7.5 Gespeicherte Daten

`settings.json` und `signatures.json` liegen im Ordner „PDF Utopia“ unter `ApplicationData` (macOS:
`LocalApplicationData`). `SettingsStore.DefaultPath` nutzt `SpecialFolderOption.DoNotVerify`, damit ein noch fehlender
Ordner nicht zu einem relativen Pfad führt (P9). `AppSettings.Normalized()` ersetzt ungültige Werte, damit eine
beschädigte Datei den Start nicht verhindert.

---

## 8. Tests

- **Core-Tests** prüfen Geometrie, Anmerkungen, Rückgängig, Seitenliste, Seitenansichten (`PageLayoutTests`),
  Parser, Einstellungen, erkannten Text.
- **Pdf-Tests** erzeugen ihre Eingaben mit `TestPdfFactory` (14 Seiten, Überschrift „Seite N“, Seite 3 in der Datei
  gedreht; Varianten mit Lesezeichen, Formular, Passwort) in einem `TempDirectory`. Geprüft wird das Ergebnis über
  - die Struktur (PDFsharp: Subtype, Einträge, Objekte),
  - den Text (PDFium: `PageText`, `FindText`),
  - **gerenderte Pixel** (`Render(...).PixelAt`), meist im Vergleich zur unveränderten Seite.
- `RoundTripTests` speichern alle Anmerkungsarten und lesen sie wieder ein; neue Arten gehören in `AllKinds`.
- `TestdataGenerator` (mit `[Fact(Explicit = true)]`) erzeugt die Dateien in `testdata/` neu; diese Tests laufen nur
  ausdrücklich.
- `RecognitionTests.Tesseract_RecognisesTextOfAnImageOnlyPage` läuft nur, wenn Tesseract mit Deutsch installiert ist,
  sonst wird er übersprungen.

Die Oberfläche hat keine automatischen Tests; sie wird anhand von `docs/ABNAHME-CHECKLISTE.md` geprüft.

---

## 9. Eine neue Anmerkungsart hinzufügen

Checkliste (Beispiel: `StampAnnotation`, A51):

1. **Core:** Record in `Annotations/` (unveränderlich, mit `Id`, `ColorHex`), dazu Maße, die Anzeige und Datei teilen.
2. **Core-Hilfen erweitern:** `AnnotationBounds.Of`, `EraserHitTest.IsErasable`/`Hits`, `PageTransform.Rotate`,
   `AnnotationOverview.Describe` mit neuem `AnnotationLabel`.
3. **Speichern:** `AnnotationSerializer` (`JsonDerivedType` mit eindeutigem `kind`), `AnnotationWriter.Write` mit
   Erscheinungsbild über `ContentBuilder`. Große Binärdaten nicht ins JSON (siehe Bilder: eigenes XObject).
4. **Wiedereinlesen:** reicht das JSON nicht (wie bei Bildern), `SavedAnnotationImporter` ergänzen.
5. **App:** Werkzeug in `Tool`, Optionen in `ToolOptionsViewModel` und `MainWindow.axaml`, Zeichnen in
   `PageEditor.DrawAnnotation`, Eingabe als `partial`-Datei, Texte in `Strings` (Deutsch und Englisch), Beschriftung
   `Annotations.Label.<Label>` für die Übersicht.
6. **Tests:** Core-Geometrie, Speichern (Struktur + Pixel), `RoundTripTests.AllKinds`.
7. **Dokumentation:** Anwenderdokumentation, Benutzerhandbuch, README, OFFENE-PUNKTE.

---

## 10. Konventionen und Arbeitsweise

- **Sprache:** Bezeichner englisch, Kommentare und XML-Dokumentation deutsch. Jede öffentliche und jede nicht
  triviale private Methode hat einen `<summary>`. Kommentare erklären das *Warum* (oft mit Verweis auf eine ID aus
  OFFENE-PUNKTE).
- **Stil:** Clean Code, kleine Methoden, unveränderliche Records für Daten, `sealed` wo möglich, Nullable aktiviert,
  keine Warnungen. ViewModels mit `[ObservableProperty]` auf `partial`-Properties und `[RelayCommand]`.
- **Neue Oberfläche:** zuerst ein HTML-Mockup in `mockups/` (wird im Browser abgenommen), dann umsetzen.
- **OFFENE-PUNKTE.md** ist das Projekttagebuch: Probleme (P), offene Fragen (F), Annahmen/Funktionen (A),
  Einschränkungen (E), Mockup-Abnahmen (M), Dokumentation (D). Jeder Punkt wird im selben Arbeitsschritt eingetragen
  bzw. mit Datum nach „Erledigt“ verschoben; das Stand-Datum wird angepasst.
- **Git:** Funktionen auf einem eigenen Branch (zuletzt `feature/seitenansichten`), ein Commit je abgeschlossener
  Funktion; nach der Abnahme in `main` mergen.
- **Oberfläche testen, ohne den Anwender zu stören:** eine eigene Instanz in einem verschachtelten X-Server starten
  (`Xephyr :99`, `DISPLAY=:99`, eigenes `XDG_CONFIG_HOME`). Eingaben und Bildschirmfotos gehen dann nur an diesen
  Bildschirm.

---

## 11. Fallstricke

| Thema | Was passiert | Lösung |
|---|---|---|
| PDFsharp-Version | Ab 6.3 AGPL und Lizenzprüfung zur Laufzeit | bei 6.2.4 bleiben (A21) |
| PDFium und Threads | Abstürze bei parallelen Aufrufen | alles unter `PdfiumLibrary.Sync` |
| Formularfelder unsichtbar | PDFium zeichnet Felder nur mit Formularumgebung | `FPDFDOC_InitFormFillEnvironment` + `FPDF_FFLDraw` (P8) |
| Schwärzen | Ein schwarzes Rechteck allein entfernt nichts | Seite als Bild ersetzen, alten Inhalt nicht mitschreiben (A52) |
| Erkannter Text auf Querformat-Scans | Textebene quer zur Leserichtung | Ausrichtung mit Tesseract `--psm 0` („osd“) erkennen und aufrecht erkennen (A53) |
| `Environment.GetFolderPath` | liefert bei fehlendem Ordner eine leere Zeichenkette | `SpecialFolderOption.DoNotVerify` (P9) |
| Avalonia-Aufklappfelder | Fokus ins Hauptfenster schließt ein offenes Flyout | Fokus erst im `Closed`-Ereignis zurückgeben (P16) |
| `TextBox` und Esc | Das Textfeld verarbeitet Esc selbst | Esc im Tunnel abfangen (P16) |
| Schaltflächen in Klappfeldern | Schließen vor dem Befehl trennt die Bindung | Schließen per `Dispatcher.UIThread.Post` nach dem Befehl (A51) |
| Einzeldatei überschreiben | laufendes Programm liest sich selbst | erst beenden, dann veröffentlichen |
| Tastenkürzel unter Linux Mint | Strg+Umschalt+N ist vom System belegt | Funktion bleibt über Menü erreichbar (E8) |
| Mehrere Seiteneditoren | Ein Editor, der „die aktuelle Seite“ liest, zeigt in fortlaufenden Ansichten überall dieselbe Seite | jeder Editor nutzt sein `PageViewModel`; aktuelle Seite nur über `Show`/`Activate` ändern (A55) |
| Angezeigte Bitmaps | Der Zwischenspeicher entsorgt Bitmaps, die andere Seiten noch zeigen | `PageRenderer.Pin`/`Unpin` (A55) |
| Rahmen außerhalb der Seite | Die Zoom-`LayoutTransformControl` schneidet alles außerhalb ihres Inhalts ab | Hervorhebungen innerhalb der Seite zeichnen (A55) |
| Einstellungen laden | Setzen der ersten Eigenschaft speichert die zweite, noch ungeladene mit | erst nach dem Laden speichern (`_layoutLoaded`, A55) |
| Schieberegler (Fluent) | höher als Knöpfe und Farbpunkte; die Optionsleiste wuchs je Werkzeug | feste Höhe der Leiste, Schieberegler mit Rand −8 px (P19) |

Hinweis: Die Projektdatei von PdfUtopia.App enthält `InternalsVisibleTo` für ein nicht vorhandenes Projekt
`PdfUtopia.App.Tests`; der Eintrag ist ohne Wirkung und für künftige Oberflächentests vorgesehen.
