# PDF Utopia – Anwenderdokumentation

Stand: 2026-11-03

Diese Dokumentation ist das Nachschlagewerk zu PDF Utopia: Voraussetzungen, Installation, Funktionsumfang,
die Regeln hinter den Funktionen, gespeicherte Daten und bekannte Einschränkungen. Wie man die einzelnen
Funktionen Schritt für Schritt bedient, beschreibt das [Benutzerhandbuch](BENUTZERHANDBUCH.md).

---

## 1. Wozu PDF Utopia dient

PDF Utopia ist ein Editor zum Bearbeiten und Kommentieren von PDF-Dateien. Mit ihm können Sie

- Textabschnitte **kommentieren** (farbig umrandet und hinterlegt, mit Fußnotenzahl und Fußnotentext auf einer
  nach unten verlängerten Seite),
- mit Maus oder Stift **markieren, zeichnen und Rechtecke aufziehen**, auf Wunsch druckempfindlich,
- Markierungen mit dem **Radierer** wieder entfernen,
- Seiten **umsortieren, drehen und löschen**,
- einzelne Seiten oder Seitenbereiche **extrahieren**,
- im Dokument **suchen**,
- das Ergebnis als **PDF speichern** – auch passwortgeschützt – und **drucken**.

Alle Änderungen lassen sich rückgängig machen. Vorhandener Text einer PDF-Datei kann nicht verändert werden
(siehe [Einschränkungen](#9-bekannte-einschränkungen)).

---

## 2. Systemvoraussetzungen

| | |
|---|---|
| **Betriebssysteme** | Windows 10 und 11, Linux (x64, arm64), macOS in einer von .NET 10 unterstützten Version. macOS 10.13 wird **nicht** unterstützt. |
| **Laufzeit** | .NET 10. Im veröffentlichten Programmpaket ist sie bereits enthalten, sonst muss das .NET 10 SDK bzw. die Laufzeit installiert sein. |
| **Bildschirm** | ab 900 × 600 Pixel; empfohlen ab 1280 × 800 |
| **Eingabe** | Maus, Touchpad, Stift/Grafiktablett (z. B. Wacom) – Andruck und Radiererende werden unterstützt, wenn das Gerät sie meldet |

Getestet wurde unter Linux Mint (X11) mit einem Wacom Bamboo Fun CTH-661.

---

## 3. Installation und Start

### 3.1 Aus dem Quellcode starten

Voraussetzung ist das .NET 10 SDK.

```bash
dotnet run --project "src/PdfUtopia.App"
```

Eine oder mehrere PDF-Dateien lassen sich gleich beim Start öffnen:

```bash
dotnet run --project "src/PdfUtopia.App" -- "datei1.pdf" "datei2.pdf"
```

### 3.2 Eigenständiges Programmpaket erstellen

Das Paket enthält die .NET-Laufzeit und alle Bibliotheken; auf dem Zielrechner muss nichts installiert sein.

| Ziel | Befehl |
|---|---|
| Linux | `dotnet publish src/PdfUtopia.App -c Release -r linux-x64 --self-contained -o publish/linux-x64` |
| Windows | `dotnet publish src/PdfUtopia.App -c Release -r win-x64 --self-contained -o publish/win-x64` |
| macOS (Apple Silicon) | `dotnet publish src/PdfUtopia.App -c Release -r osx-arm64 --self-contained -o publish/osx-arm64` |
| macOS (Intel) | `dotnet publish src/PdfUtopia.App -c Release -r osx-x64 --self-contained -o publish/osx-x64` |

Gestartet wird anschließend die Datei `PdfUtopia` (unter Windows `PdfUtopia.exe`) im Ausgabeordner. Das
Linux-Paket wurde erstellt und gestartet (rund 112 MB); die Pakete für Windows und macOS sind nicht getestet.

**Als einzelne Datei:** Mit `-p:PublishSingleFile=true` entsteht statt eines Ordners eine einzige ausführbare
Datei, zum Beispiel für Linux:

```bash
dotnet publish src/PdfUtopia.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/single-linux-x64
```

Für die anderen Systeme wird `-r` wie in der Tabelle oben angepasst. Die Datei ist rund 103 MB groß und kann an
einen beliebigen Ort kopiert werden. Die drei nativen Bibliotheken (PDFium, Skia, HarfBuzz, zusammen rund 21 MB)
stecken mit in der Datei. Beim ersten Start entpackt .NET sie in einen Zwischenspeicher, unter Linux nach
`~/.cache/dotnet_bundle_extract/PdfUtopia`. Ein anderer Ort lässt sich über die Umgebungsvariable
`DOTNET_BUNDLE_EXTRACT_BASE_DIR` festlegen. Die Linux-Datei wurde erstellt und aus einem anderen Ordner gestartet;
Windows und macOS sind nicht getestet. Unter macOS ist eine einzelne Datei kein gewohntes Programmpaket (`.app`);
dafür wäre ein eigenes Bundle nötig.

### 3.3 Weitere Wege, Dateien zu öffnen

- über **Datei → Öffnen…** (Strg+O) oder die Schaltfläche **Öffnen**,
- per **Drag & Drop**: PDF-Dateien aus dem Dateimanager in das Fenster ziehen,
- über das **+** neben den Dokument-Tabs.

---

## 4. Die Oberfläche im Überblick

```
┌─────────────────────────────────────────────────────────────────────────┐
│ PDF Utopia   Datei  Bearbeiten  Ansicht  Sprache                  (Menü) │
├─────────────────────────────────────────────────────────────────────────┤
│ Öffnen Speichern Drucken │ ↶ ↷ │ Werkzeuge │ Extrahieren Drehen Löschen  │
│ │ − 100 % + │                                 [Suche … ×]  ● Akzentfarbe │
├─────────────────────────────────────────────────────────────────────────┤
│ Werkzeug: Farbe, Strichstärke, … (Optionsleiste)                 Hinweis │
├─────────────────────────────────────────────────────────────────────────┤
│ datei.pdf × │ vertrag.pdf × │ +                              (Tabs)      │
├───────────────┬─────────────────────────────────────────────────────────┤
│ Seiten        │                                                         │
│  ┌───┐        │                ┌───────────────┐                        │
│  │ 1 │        │                │    Seite      │                        │
│  └───┘        │                │               │                        │
│  ┌───┐        │                ├ ─ ─ ─ ─ ─ ─ ─ ┤                        │
│  │ 2 │        │                │  Fußnoten     │                        │
│  └───┘        │                └───────────────┘                        │
├───────────────┴─────────────────────────────────────────────────────────┤
│ Seite 1 von 14 · Rückgängig 2/50 · datei.pdf   Volle Höhe/Breite/Ganze Seite − 100 % + │
└─────────────────────────────────────────────────────────────────────────┘
```

| Bereich | Inhalt |
|---|---|
| Menüleiste | Datei, Bearbeiten, Ansicht (Design, Akzentfarbe, Zoom), Sprache |
| Werkzeugleiste | häufige Befehle, die Werkzeuge, Zoom, Suchfeld, Farbpunkt der Akzentfarbe. Bei schmalen Fenstern bricht sie in eine zweite Zeile um. |
| Optionsleiste | Einstellungen des gewählten Werkzeugs und ein Hinweis zur Bedienung |
| Tabs | jedes geöffnete Dokument hat einen Tab; ein Schloss kennzeichnet geschützte Dateien, ein Punkt ungespeicherte Änderungen |
| Seitenliste | Miniaturen aller Seiten in der aktuellen Reihenfolge; oben links zeigen kleine Symbole in der Akzentfarbe, welche Annotationsarten die Seite enthält (Kommentar, Marker, Freihand, Rechteck) |
| Seitenansicht | die angezeigte Seite mit Annotationen; darunter die Fußnoten der Kommentare |
| Statusleiste | Seite, Rückgängig-Schritte, Datei und Größe, Ansichten und Zoom |

---

## 5. Funktionsumfang und Regeln

### 5.1 Werkzeuge

| Werkzeug | Wirkung | Einstellungen |
|---|---|---|
| **Auswählen** | keine Bearbeitung; Seiten in der Seitenliste umsortieren | – |
| **Kommentar** | Absatz anklicken (wird erkannt) oder Bereich aufziehen | Farbe (Farbwahl) |
| **Marker** | waagerechter, halbtransparenter Balken wie ein Textmarker | 5 Textmarker-Farben, Höhe 8–30 |
| **Freihand** | freie Linie mit Maus oder Stift | Farbe (Farbwahl), Stärke 1–12, Glättung 0–100 %, druckempfindlich |
| **Rechteck** | Rechteck aufziehen | Farbe (Farbwahl), Stärke 1–12 |
| **Radierer** | entfernt berührte Marker, Freihandlinien und Rechtecke vollständig | Größe 2–40 |

Weitere Regeln:

- **Zeichnen nur bei Kontakt.** Ein Strich endet beim Loslassen, aber auch, sobald der Stift abgehoben wird.
  Eingaben werden nicht an ein bestimmtes Gerät gebunden; auch Tablets, die das Aufsetzen als Maus und die
  Bewegung als Stift melden, funktionieren.
- **Druckempfindlichkeit** gilt nur für Freihand: Andruck 0 → 30 %, Mittelwert → 100 %, voller Andruck → 170 %
  der eingestellten Stärke. Meldet der Stift keinen Andruck, bleibt die Linie gleich dick, und die
  Optionsleiste weist darauf hin. Mit der Maus wirkt die Einstellung nicht.
- **Glättung** verbindet die Punkte einer Freihandlinie mit Kurven statt geraden Stücken. Punkte, die näher als
  1,5 Punkte beieinanderliegen, werden verworfen (gegen Zittern).
- **Radierer:** Er entfernt immer ganze Markierungen. Rechtecke werden nur über ihren Rand erfasst, nicht über
  ihre Fläche. Kommentare radiert er nicht – sie werden über ihre Fußnotenzahl entfernt. Das **Radiererende des
  Stifts** radiert unabhängig vom gewählten Werkzeug. Ein Radierstrich ist ein Rückgängig-Schritt.
- **Sehr kleine Striche** (unter 4 Punkte Ausdehnung) gelten als versehentlicher Klick und werden verworfen.

### 5.2 Kommentare

- **Absatz anklicken:** PDF Utopia erkennt Absätze aus der Textebene der Seite (Zeichen auf gleicher Höhe bilden
  Zeilen, eng aufeinanderfolgende, ähnlich ausgerichtete Zeilen einen Absatz). Beim Darüberfahren wird der
  erkannte Absatz gestrichelt in der Kommentarfarbe umrandet, genau in der Größe des späteren Kommentars. Der Kommentarrahmen hat 8 Punkte Abstand zum Text.
- **Bereich aufziehen** geht immer – auch bei gescannten Seiten ohne Textebene oder bei Tabellen und Spalten,
  wo die Erkennung danebenliegen kann.
- **Darstellung:** Rahmen und Hinterlegung in der Kommentarfarbe, Fußnotenzahl oben rechts. Die Zahl ist hell
  oder dunkel – je nachdem, was auf der Farbe lesbar ist. Sehr helle Farben bekommen einen grauen Rahmen.
- **Fußnoten:** Unter der Seite stehen die Fußnoten aller Kommentare der Seite, nummeriert in der Reihenfolge,
  in der die Kommentare angelegt wurden. Der Text lässt sich direkt bearbeiten; er wird beim Verlassen des Felds
  übernommen (ein Rückgängig-Schritt).
- **Entfernen:** Mit der Maus über die Fußnotenzahl fahren – sie wird zum ×; ein Klick entfernt den Kommentar
  samt Fußnote, die übrigen werden neu nummeriert.

### 5.3 Seiten

- **Umsortieren** per Drag & Drop in der Seitenliste. Verschobene Seiten zeigen zusätzlich ihre ursprüngliche
  Seitennummer, z. B. „2 (Seite 14)“.
- **Drehen** dreht um 90° im Uhrzeigersinn; Annotationen drehen mit.
- **Blättern** in der Seitenansicht mit Mausrad oder Tasten (siehe 6). Ist die Seite größer als die Ansicht, scrollt
  das Mausrad zuerst innerhalb der Seite; erst die nächste Drehung am oberen bzw. unteren Rand wechselt die Seite.
  Rückwärts landet man am unteren Rand der vorigen Seite. Nach einem Seitenwechsel per Mausrad werden
  Radbewegungen 0,3 Sekunden lang nicht ausgewertet, damit ein Schwung auf dem Touchpad nicht mehrere Seiten
  überspringt.
- **Löschen:** Die letzte verbleibende Seite eines Dokuments kann nicht gelöscht werden. Annotationen einer
  gelöschten Seite bleiben für „Rückgängig“ erhalten.

### 5.4 Rückgängig und Wiederholen

Rückgängig machen lassen sich alle Änderungen am Dokument: Annotationen anlegen, ändern und entfernen
(auch radieren), Fußnotentexte, Seiten verschieben, drehen und löschen. Die Anzahl der Schritte ist einstellbar
(1–500, Standard 50); ältere Schritte werden verworfen. Ein neuer Schritt verwirft alle wiederholbaren Schritte.
Einstellungen (Farben, Theme, Sprache …) gehören nicht zum Verlauf.

### 5.5 Seiten extrahieren

Die Eingabe besteht aus einzelnen Seiten und Bereichen, durch Kommas getrennt, z. B. `2`, `4-8` oder
`2,4,8,13-15`. Es gelten diese Regeln:

| Regel | Beispiel (Dokument mit 14 Seiten) | Ergebnis |
|---|---|---|
| Seiten zählen ab 1 | `1` | Seite 1 |
| Die Reihenfolge der Eingabe bleibt erhalten | `8,2` | erst 8, dann 2 |
| Absteigende Bereiche werden aufsteigend gelesen | `8-4` | 4, 5, 6, 7, 8 (mit Hinweis) |
| Seiten über der Seitenzahl werden auf die letzte Seite begrenzt | `13-20` | 13, 14 (mit Hinweis) |
| Doppelte Seiten werden nur einmal übernommen | `2,3-5,2` | 2, 3, 4, 5 (mit Hinweis) |
| Ungültige Teile werden ignoriert | `1,abc,0` | 1 (mit Hinweis) |

Die Seitennummern beziehen sich auf die **aktuelle Seitenliste** links – also nach Umsortieren und Löschen.
Extrahierte Seiten übernehmen ihre Drehung und ihre Annotationen. Der Dialog zeigt während der Eingabe eine
Vorschau der neuen Datei und alle Hinweise.

### 5.6 Speichern

- Es wird immer **„Speichern unter“** verwendet; das Original bleibt unverändert, solange Sie es nicht als Ziel
  wählen. Auch das Speichern über die geöffnete Datei funktioniert.
- Gespeichert werden Reihenfolge, Drehung und Löschungen der Seiten sowie alle Annotationen.
- **Passwortschutz:** Vor der Dateiauswahl fragt PDF Utopia, wie mit dem Passwort verfahren wird:

  | Auswahl | Wirkung |
  |---|---|
  | Passwort behalten / Ohne Passwort (wie bisher) | wie die geöffnete Datei (Standard) |
  | Neues Passwort festlegen | Schutz mit neuem Passwort (zweimal einzugeben); auch für bisher ungeschützte Dateien |
  | Passwort entfernen | ohne Schutz speichern (nur bei geschützten Dateien angeboten) |

  Benutzer- und Besitzerpasswort werden gleich gesetzt.

- Gespeichert wird erst in eine temporäre Datei, die dann das Ziel ersetzt – ein Fehler beim Schreiben lässt
  eine vorhandene Datei unversehrt.

### 5.7 Wie Annotationen in der PDF-Datei stehen

Kommentare, Marker, Freihandlinien und Rechtecke werden als **echte PDF-Annotationen** gespeichert
(Square, Highlight, Ink, FreeText) – mit eigenem Erscheinungsbild, damit sie in jedem PDF-Betrachter
(Acrobat, Browser, Vorschau …) so aussehen wie in PDF Utopia. Seiten mit Kommentaren werden nach unten
verlängert; dort stehen die Fußnoten unter einer gestrichelten Trennlinie mit der Überschrift „Kommentare“
(in der Sprache der Oberfläche beim Speichern).

**Wiederöffnen:** Öffnen Sie eine mit PDF Utopia gespeicherte Datei erneut, sind die Annotationen wieder
bearbeitbar. Die Seitenverlängerung wird dabei zurückgenommen und beim nächsten Speichern neu angelegt – sie
wächst also nicht bei jedem Speichern. Annotationen anderer Programme bleiben unverändert erhalten und werden
mit angezeigt, sind aber in PDF Utopia nicht bearbeitbar.

### 5.8 Drucken

PDF Utopia speichert das Dokument mit allen Änderungen als temporäre, ungeschützte PDF-Datei und öffnet sie im
PDF-Betrachter des Systems. Gedruckt wird dort über dessen Druckdialog.

### 5.9 Suche

Gesucht wird im Text aller Seiten, ohne Unterscheidung von Groß- und Kleinschreibung. Das Suchfeld zeigt die
Anzahl der Treffer; **Enter** springt zur nächsten Seite mit Treffern; **×** oder **Esc** beendet die Suche.
Treffer werden auf der Seite in der Akzentfarbe hinterlegt. Die Deckkraft wird je Farbe so gewählt, dass dunkler
Text darauf gut lesbar bleibt; sehr helle Farben bekommen zusätzlich einen Rahmen. Gescannte Seiten ohne
Textebene liefern keine Treffer.

### 5.10 Ansichten und Zoom

| Ansicht | Wirkung |
|---|---|
| **Volle Höhe** | die Seitenhöhe füllt den Anzeigebereich |
| **Volle Breite** | die Seitenbreite füllt den Anzeigebereich |
| **Ganze Seite** | die ganze Seite ist sichtbar (Standard) |

Die gewählte Ansicht wird bei jeder Größenänderung des Fensters – auch beim Maximieren –, beim Seitenwechsel
und beim Drehen neu angewandt. Zoomen mit + oder − (Schritte von 10 %, 30–400 %) schaltet die Ansicht ab, bis
wieder eine gewählt wird.

### 5.11 Mehrere Dokumente

Jedes Dokument öffnet in einem eigenen Tab. Eine bereits geöffnete Datei wird nicht doppelt geöffnet – ihr Tab
wird angezeigt. Beim Schließen eines Tabs oder des Fensters mit ungespeicherten Änderungen fragt PDF Utopia:
**Speichern**, **Verwerfen** oder **Abbrechen**.

### 5.12 Erscheinungsbild, Sprache und Farben

- **Design:** System, Hell oder Dunkel (Menü **Ansicht** oder Einstellungen). Das helle Design verzichtet auf
  reines Weiß, das dunkle ist neutral grau; die PDF-Seite selbst bleibt immer weiß.
- **Sprache:** Deutsch oder Englisch (Menü **Sprache**). Ohne eigene Wahl richtet sie sich nach dem
  Betriebssystem – Deutsch bei deutscher Systemsprache, sonst Englisch.
- **Farbwahl** (Akzentfarbe sowie Farben für Freihand, Rechteck und Kommentar):

  | Gruppe | Inhalt |
  |---|---|
  | Pastell | Pastellblau, Mint, Pastellgelb, Pfirsich, Rosé, Lavendel; Sättigung 10–90 % (Standard 30 %) |
  | Kräftig | Indigo, Blau, Grün, Gelb, Orange, Rot, Violett |
  | Neutral | 95 % Schwarz, 95 % Weiß |
  | Benutzerdefiniert | eigene Farben per Hex-Code (`#RRGGBB`), höchstens 10, die älteste fällt bei mehr weg |

  Sättigung und eigene Farben gelten gemeinsam für alle Ziele. **Standard** setzt das jeweilige Ziel auf seine
  Standardfarbe zurück (Akzent und Kommentar Indigo, Freihand Rot, Rechteck Blau). Schrift auf der Akzentfarbe
  wird automatisch hell oder dunkel gesetzt; hebt sich eine Akzentfarbe zu wenig vom Hintergrund ab, bekommen
  Akzentflächen einen Rahmen in einer satteren Variante der Farbe (bei neutralen Farben in Textfarbe).

---

## 6. Tastenkürzel

| Kürzel | Befehl |
|---|---|
| Strg+O | Öffnen |
| Strg+Umschalt+S | Speichern unter |
| Strg+E | Seiten extrahieren |
| Strg+P | Drucken |
| Strg+W | Dokument schließen |
| Strg+Z | Rückgängig |
| Strg+Y oder Strg+Umschalt+Z | Wiederholen |
| Strg+Plus / Strg+Minus | Vergrößern / Verkleinern |
| Strg+F | Suchfeld |
| Enter (im Suchfeld) | nächste Seite mit Treffern |
| Esc (im Suchfeld) | Suche beenden |
| ← / → oder Bild↑ / Bild↓ | vorige / nächste Seite |
| ↑ / ↓ | scrollen; am oberen bzw. unteren Rand vorige / nächste Seite |
| Pos1 / Ende | erste / letzte Seite |

Die Blättertasten wirken nicht, solange ein Textfeld (z. B. eine Fußnote oder das Suchfeld), ein Schieberegler,
eine Auswahlliste oder ein Menü den Fokus hat.

Die Kürzel sind auf allen Systemen mit der Strg-Taste (macOS: control) hinterlegt, nicht mit Cmd.

---

## 7. Gespeicherte Einstellungen

PDF Utopia speichert seine Einstellungen bei jeder Änderung in einer JSON-Datei:

| System | Ort |
|---|---|
| Windows | `%APPDATA%\PDF Utopia\settings.json` |
| Linux | `~/.config/PDF Utopia/settings.json` |
| macOS | `~/Library/Application Support/PDF Utopia/settings.json` |

Gespeichert werden Design, Sprache, Akzentfarbe, Pastell-Sättigung, eigene Farben, Anzahl der
Rückgängig-Schritte sowie Farben und Einstellungen der Werkzeuge. Ist die Datei beschädigt oder enthält
ungültige Werte, startet PDF Utopia mit den Standardwerten bzw. ersetzt nur die ungültigen Werte. Zum
Zurücksetzen aller Einstellungen genügt es, die Datei zu löschen, während PDF Utopia geschlossen ist.

---

## 8. Datenschutz und Sicherheit

- PDF Utopia arbeitet vollständig lokal; es werden keine Daten übertragen.
- Passwörter werden nicht gespeichert. Sie werden nur für die geöffnete Datei im Arbeitsspeicher gehalten.
- Beim Drucken entsteht eine **ungeschützte** temporäre Kopie im Temp-Ordner des Systems
  („PDF Utopia – *Name*.pdf“). Bei vertraulichen Dokumenten sollte sie nach dem Drucken gelöscht werden.

---

## 9. Bekannte Einschränkungen

| Nr. | Einschränkung |
|---|---|
| E1 | macOS 10.13 wird nicht unterstützt (.NET 10 benötigt ein neueres macOS). |
| E2 | Vorhandener Text einer PDF-Datei kann nicht bearbeitet werden. |
| E3 | Gedruckt wird über den PDF-Betrachter des Systems; einen eigenen Druckdialog gibt es nicht. |
| E4 | In virtuellen Maschinen kommt der Stift-Andruck nur an, wenn das Tablet per USB direkt an die VM durchgereicht wird. Über die Maus-Integration der VM gibt es keinen Andruck. |
| E5 | Zeichen außerhalb des westeuropäischen Zeichensatzes (z. B. chinesische Schriftzeichen, Emojis) erscheinen im gespeicherten Fußnotentext als „?“. Der vollständige Text bleibt in der Annotation erhalten. |
| – | Annotationen anderer Programme werden angezeigt, sind aber nicht bearbeitbar. |
| – | Beim Speichern einer geschützten Fremddatei geht ein abweichendes Besitzerpasswort verloren; es wird durch das eingegebene Passwort ersetzt. |
| – | Die Absatzerkennung kann bei Spalten, Tabellen oder ungewöhnlichem Satz danebenliegen; dann einen Bereich aufziehen. |

---

## 10. Verwendete Bibliotheken und Lizenzen

PDF Utopia ist ein Open-Source-Projekt. Es verwendet ausschließlich Bibliotheken mit freien Lizenzen:

| Bibliothek | Version | Zweck | Lizenz |
|---|---|---|---|
| Avalonia (mit Fluent-Theme) | 12.1.4 | Oberfläche | MIT |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM-Grundlagen | MIT |
| PDFiumCore | 157.0.8100 | Anzeigen und Textauslesen | MIT (PDFium-Binärdateien: BSD-3 / Apache-2.0) |
| PDFsharp | 6.2.4 | Schreiben von PDF-Dateien | MIT |
| System.Security.Cryptography.Pkcs | 10.0.14 | Sicherheitsaktualisierung einer PDFsharp-Abhängigkeit | MIT |

PDFsharp wird bewusst in Version 6.2.4 verwendet: Ab Version 6.3 steht PDFsharp unter der AGPL-3.0 mit
Lizenzprüfung. Für Texte in gespeicherten Annotationen nutzt PDF Utopia die PDF-Standardschrift Helvetica, die
jeder PDF-Betrachter mitbringt; es wird keine Schriftdatei mitgeliefert.
