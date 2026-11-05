# PDF Utopia

Plattformübergreifender PDF-Editor für Windows, Linux und macOS, geschrieben in C# mit
[Avalonia](https://avaloniaui.net/). PDF Utopia ist für Anmerkungen gemacht: kommentieren, markieren und zeichnen,
mit Maus oder Stift. Dazu kommen Seiten umsortieren, drehen, löschen und extrahieren. Alle Änderungen werden als
echte PDF-Annotationen gespeichert. Jeder PDF-Betrachter zeigt sie an, und PDF Utopia kann sie später
weiterbearbeiten.

## Funktionen

- **Kommentare mit Fußnoten:** Ein Klick auf einen Absatz oder ein aufgezogener Bereich erzeugt einen nummerierten
  Kommentar. Der Text steht als Fußnote auf einer nach unten verlängerten Seite.
- **Marker, Freihand, Formen (Rechteck, Ellipse, Linie, Pfeile mit offener oder geschlossener Spitze an einem oder beiden Enden) und Text:** mit Maus oder Stift, Freihand wahlweise druckempfindlich und mit einstellbarer
  Glättung.
- **Radierer:** als Werkzeug oder über das Radiererende des Stifts.
- **Seiten:** per Drag & Drop umsortieren, drehen, löschen, leere Seiten einfügen – auch mehrere Seiten auf einmal (Strg/Umschalt+Klick).
- **Unterschrift:** bis zu fünf Unterschriften mit Stift oder Maus schreiben und als Stempel auf Seiten setzen (sichtbar, keine digitale Signatur).
- **Anmerkungsübersicht:** alle Anmerkungen nach Seiten, mit Filter; ein Klick springt hin.
- **Formulare:** werden angezeigt und bleiben beim Speichern ausfüllbar.
- **Lesezeichen:** Inhaltsverzeichnis der Datei als Baum zum Springen; beim Speichern bleibt es erhalten.
- **Extrahieren:** Seitenauswahl wie `2`, `4-8` oder `2,4,8,13-15`, mit Vorschau.
- **Einfügen:** Seiten aus einer anderen PDF-Datei übernehmen, per Dialog oder Drag & Drop auf die Seitenliste; Anmerkungen kommen mit.
- **Rückgängig/Wiederholen:** mit einstellbarer Anzahl von Schritten (Standard 50).
- **Zoom und Ansichten:** Volle Höhe, Volle Breite, Ganze Seite; die Ansicht passt sich der Fenstergröße an.
- **Suche:** im Text aller Seiten, die Treffer werden hinterlegt.
- **Tabs:** für mehrere Dokumente gleichzeitig.
- **Passwortgeschützte Dateien:** öffnen; beim Speichern das Passwort behalten, ändern oder entfernen.
- **Speichern:** immer über „Speichern unter“, die Originaldatei bleibt erhalten.
- **Drucken:** über den PDF-Betrachter des Systems.
- **Oberfläche:** Theme System/Hell/Dunkel, wählbare Akzentfarbe, Deutsch und Englisch.

Bestehender Text im PDF lässt sich nicht bearbeiten. Weitere bekannte Einschränkungen stehen in der
[Anwenderdokumentation](docs/ANWENDERDOKUMENTATION.md).

## Voraussetzungen

- [.NET SDK 10](https://dotnet.microsoft.com/download) (getestet mit 10.0.114)
- Windows 10/11, Linux (x64) oder macOS gemäß der Support-Matrix von .NET 10. macOS 10.13 wird nicht unterstützt.

Die PDFium-Bibliotheken für alle drei Plattformen kommen über das NuGet-Paket PDFiumCore mit. Weitere Installationen
sind nicht nötig.

## Bauen, starten, testen

```bash
dotnet build PdfUtopia.slnx
```

```bash
dotnet run --project src/PdfUtopia.App
```

Dateien lassen sich gleich beim Start öffnen:

```bash
dotnet run --project src/PdfUtopia.App -- "datei1.pdf" "datei2.pdf"
```

```bash
dotnet test --solution PdfUtopia.slnx
```

Die Tests laufen mit xUnit v3 über die Microsoft Testing Platform (`global.json`). Sie arbeiten mit den
Testdateien in `testdata/` (die geschützte Datei hat das Passwort `utopia`). Ein mit `[Fact(Explicit = true)]`
markierter Test erzeugt diese Testdateien neu.

### Eigenständiges Programmpaket

```bash
dotnet publish src/PdfUtopia.App -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

Für andere Plattformen `-r win-x64`, `-r osx-arm64` oder `-r osx-x64` angeben. Gestartet wird `PdfUtopia`
(unter Windows `PdfUtopia.exe`) im Ausgabeordner. Das Linux-Paket ist getestet (rund 112 MB), die Pakete für
Windows und macOS noch nicht.

Als **einzelne Datei** (rund 103 MB, native Bibliotheken werden beim ersten Start entpackt):

```bash
dotnet publish src/PdfUtopia.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/single-linux-x64
```

## Projektstruktur

| Pfad | Inhalt |
|---|---|
| `src/PdfUtopia.Core` | Fachlogik ohne Oberfläche und ohne PDF-Bibliothek: Geometrie, Seitenliste, Annotationen, Rückgängig-Verlauf, Strichaufzeichnung, Radierer, Farben, Einstellungen |
| `src/PdfUtopia.Pdf` | PDF-Anbindung: Rendern, Text und Suche (PDFium), Speichern, Extrahieren und Annotationen schreiben (PDFsharp), gespeicherte Annotationen wieder einlesen |
| `src/PdfUtopia.App` | Avalonia-Oberfläche (MVVM mit CommunityToolkit.Mvvm): Fenster, Dialoge, Seiteneditor, Lokalisierung, Themes |
| `tests/PdfUtopia.Core.Tests` | Unit-Tests der Fachlogik |
| `tests/PdfUtopia.Pdf.Tests` | Tests der PDF-Anbindung mit den Testdateien |
| `testdata/` | Erzeugte Test-PDFs, darunter eine passwortgeschützte, eine mit Lesezeichen und eine mit Formular |
| `mockups/` | Abgenommene HTML-Mockups der Oberfläche |
| `spikes/PressureProbe` | Prototyp zum Prüfen von Stift-Andruck und Radiererende |
| `docs/` | Anwenderdokumentation und Benutzerhandbuch |
| `SPECS.md` | Ursprüngliche Spezifikation |
| `OFFENE-PUNKTE.md` | Getroffene Annahmen, bekannte Einschränkungen und erledigte Punkte |

## Dokumentation

- [Benutzerhandbuch](docs/BENUTZERHANDBUCH.md): Schritt-für-Schritt-Anleitungen für alle Aufgaben
- [Anwenderdokumentation](docs/ANWENDERDOKUMENTATION.md): Installation, Bedienkonzept, Tastenkürzel,
  Einstellungen, Datenschutz, Einschränkungen
- [Offene Punkte](OFFENE-PUNKTE.md): Entscheidungen und Annahmen aus der Entwicklung

## Verwendete Bibliotheken

| Bibliothek | Version | Zweck | Lizenz |
|---|---|---|---|
| Avalonia (mit Fluent-Theme) | 12.1.4 | Oberfläche | MIT |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM-Grundlagen | MIT |
| PDFiumCore | 157.0.8100 | Rendern, Text, Suche | MIT (PDFium: BSD-3-Clause/Apache-2.0) |
| PDFsharp | 6.2.4 | Schreiben, Extrahieren, Annotationen | MIT |
| System.Security.Cryptography.Pkcs | 10.0.14 | Behebt eine Sicherheitslücke der PDFsharp-Abhängigkeit | MIT |
| xUnit v3 | 4.0.2 | Tests | Apache-2.0 |

PDFsharp bleibt bewusst auf 6.2.4: Ab Version 6.3 steht es unter AGPL-3.0 und prüft die Lizenz zur Laufzeit.

## Lizenz

Noch nicht festgelegt (Vorschlag: MIT). Bis eine `LICENSE`-Datei vorliegt, gelten keine Nutzungsrechte über das
Urheberrecht hinaus.
