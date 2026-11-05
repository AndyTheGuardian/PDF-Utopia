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
- mit Maus oder Stift **markieren, zeichnen, Formen (Rechteck, Ellipse, Linie, Pfeile) und Text einfügen**, auf Wunsch druckempfindlich,
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
│ Öffnen Speichern Drucken │ ↶ ↷ │ Werkzeuge │ Extrahieren Einfügen Drehen Löschen │
│                                                 [Suche … ×]  ● Akzentfarbe │
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
│ Seite 1 von 14 · Rückgängig 2/50 · datei.pdf   Volle Höhe/Breite/Ganze Seite − [100 %] + │
└─────────────────────────────────────────────────────────────────────────┘
```

| Bereich | Inhalt |
|---|---|
| Menüleiste | Datei, Bearbeiten, Ansicht (Design, Akzentfarbe, Zoom), Sprache |
| Werkzeugleiste | häufige Befehle, die Werkzeuge, Suchfeld, Farbpunkt der Akzentfarbe. Bei schmalen Fenstern bricht sie in eine zweite Zeile um. |
| Optionsleiste | Einstellungen des gewählten Werkzeugs und ein Hinweis zur Bedienung |
| Tabs | jedes geöffnete Dokument hat einen Tab; ein Schloss kennzeichnet geschützte Dateien, ein Punkt ungespeicherte Änderungen. Führt Rückgängig/Wiederholen genau zum gespeicherten Stand zurück, verschwindet der Punkt wieder. |
| Linke Leiste | Reiter **Seiten**, **Anmerkungen** (mit Anzahl, sobald das Dokument Anmerkungen hat) und **Lesezeichen** (wenn die Datei welche hat); ohne beides nur die Seitenliste. Die Leiste lässt sich am rechten Rand von 180 bis 420 Pixel breit ziehen (Standard 220, wird gespeichert); die Seitenminiaturen wachsen und schrumpfen mit und werden kurz nach dem Ziehen scharf neu gezeichnet; passen die drei Reiter nicht nebeneinander, steht der dritte darunter. **Seiten:** Miniaturen aller Seiten in der aktuellen Reihenfolge; oben links zeigen kleine Symbole in der Akzentfarbe, welche Annotationsarten die Seite enthält (Kommentar, Marker, Freihand, Formen, Text); die Liste scrollt beim Blättern mit, sodass die aktuelle Seite immer sichtbar ist; eingefügte Seiten haben einen Rahmen in der Akzentfarbe und zeigen ihre Herkunft (z. B. „anhang.pdf S. 2“) |
| Seitenansicht | die angezeigte Seite mit Annotationen; darunter die Fußnoten der Kommentare |
| Statusleiste | Seite, Rückgängig-Schritte, Datei und Größe, Ansichten und Zoom (− / Eingabefeld / +) |

---

## 5. Funktionsumfang und Regeln

### 5.1 Werkzeuge

| Werkzeug | Wirkung | Einstellungen |
|---|---|---|
| **Auswählen** | keine Bearbeitung; Seiten in der Seitenliste umsortieren | – |
| **Kommentar** | Absatz anklicken (wird erkannt) oder Bereich aufziehen | Farbe (Farbwahl) |
| **Marker** | waagerechter, halbtransparenter Balken wie ein Textmarker | 5 Textmarker-Farben, Höhe 8–30 |
| **Freihand** | freie Linie mit Maus oder Stift | Farbe (Farbwahl), Stärke 1–12, Glättung 0–100 %, druckempfindlich |
| **Formen** | Rechteck, Ellipse, Linie, Pfeil mit offener oder geschlossener (gefüllter) Spitze aufziehen; Pfeilspitzen am Ende, am Anfang oder an beiden Enden; mit Umschalt rasten Linien in 15°-Schritten ein, Rechteck und Ellipse werden zu Quadrat und Kreis | Form, Lage der Pfeilspitze, Farbe (Farbwahl), Stärke 1–12 (gemeinsam für alle Formen); die Pfeilspitze wächst mit der Stärke |
| **Text** | freien Text auf die Seite schreiben; klicken (Standardbreite 200 pt) oder die Breite aufziehen | Farbe (Farbwahl, Standard fast Schwarz), Schriftgröße 8–36 pt (Standard 12) |
| **Unterschrift** | gespeicherte Unterschrift auf die Seite setzen: klicken (Breite 150 pt) oder Breite aufziehen; gesetzte Unterschrift ziehen zum Verschieben | Auswahl unter bis zu 5 gespeicherten Unterschriften, „+ Neue“, „Verwalten…“ |
| **Radierer** | entfernt berührte Marker, Freihandlinien, Formen, Texte und Unterschriften vollständig | Größe 2–40 |

Weitere Regeln:

- **Zeichnen nur bei Kontakt.** Ein Strich endet beim Loslassen, aber auch, sobald der Stift abgehoben wird.
  Eingaben werden nicht an ein bestimmtes Gerät gebunden; auch Tablets, die das Aufsetzen als Maus und die
  Bewegung als Stift melden, funktionieren.
- **Druckempfindlichkeit** gilt nur für Freihand: Andruck 0 → 30 %, Mittelwert → 100 %, voller Andruck → 170 %
  der eingestellten Stärke. Meldet der Stift keinen Andruck, bleibt die Linie gleich dick, und die
  Optionsleiste weist darauf hin. Mit der Maus wirkt die Einstellung nicht.
- **Glättung** verbindet die Punkte einer Freihandlinie mit Kurven statt geraden Stücken. Punkte, die näher als
  1,5 Punkte beieinanderliegen, werden verworfen (gegen Zittern).
- **Radierer:** Er entfernt immer ganze Markierungen. Rechtecke und Ellipsen werden nur über ihren Rand erfasst, nicht über
  ihre Fläche; Linien und Pfeile entlang der Strecke, Texte über ihre ganze Fläche. Kommentare radiert er nicht – sie werden über ihre Fußnotenzahl entfernt. Das **Radiererende des
  Stifts** radiert unabhängig vom gewählten Werkzeug. Ein Radierstrich ist ein Rückgängig-Schritt.
- **Sehr kleine Striche** (unter 4 Punkte Ausdehnung) gelten als versehentlicher Klick und werden verworfen.
- **Text:** Zeilen brechen an der Breite des Textfelds um, die Höhe wächst mit; Enter beginnt eine neue Zeile,
  Esc oder ein Klick daneben schließt die Eingabe ab. Mit dem Text-Werkzeug öffnet ein Klick auf einen Text die
  Bearbeitung, Ziehen verschiebt ihn. Ein geleerter Text wird gelöscht. Anlegen, Ändern, Verschieben und Löschen
  sind je ein Rückgängig-Schritt. Farbe und Größe in der Optionsleiste gelten für neue Texte und den gerade
  bearbeiteten. Geschrieben wird in Helvetica; Zeichen außerhalb des westeuropäischen Zeichensatzes erscheinen
  in der gespeicherten Datei als „?“ (E5). Auf einer gedrehten Seite steht neuer Text aufrecht und dreht sich
  danach mit der Seite. Gespeichert wird der Text als Standard-Annotation „FreeText“.

### 5.0 Formulare

Enthält eine Datei ein PDF-Formular (AcroForm), zeigt PDF Utopia über der Seite den Hinweis „Dieses Dokument
enthält ein Formular mit N Feldern.“; das × schließt ihn für dieses Dokument.
Die Felder werden mit ihren aktuellen Werten angezeigt.

**Ausfüllen:** Im Werkzeug **Auswählen** lassen sich Textfelder (ein- und mehrzeilig, mit Höchstlänge),
Kontrollkästchen, Optionsfelder, Klapplisten und Auswahllisten direkt auf der Seite bearbeiten. Mit einem anderen
Werkzeug sind die Felder gesperrt, der Hinweis nennt dann „Zum Ausfüllen das Werkzeug ‚Auswählen‘ wählen“. Der
Schalter **Felder hervorheben** in der Hinweisleiste hinterlegt die Felder dezent in der Akzentfarbe; die
Einstellung wird gespeichert (Standard: an). **Tab** springt zum nächsten Feld der Seite, in Leserichtung von
oben nach unten und links nach rechts. Schreibgeschützte Felder sind grau und nicht bearbeitbar. Text wird beim
Verlassen des Felds übernommen. Jede Feldänderung ist ein Rückgängig-Schritt (Strg+Z).

Beim **Speichern** und **Extrahieren** bleibt das Formular ausfüllbar: Felder gelöschter bzw. nicht extrahierter
Seiten entfallen, gleichnamige Felder aus eingefügten Dateien werden umbenannt („Name_2“), damit sie keinen
gemeinsamen Wert bekommen. Geänderte Werte bekommen ein neues Erscheinungsbild in Helvetica, damit jeder
PDF-Betrachter sie zeigt; unveränderte Felder bleiben, wie sie waren.

Einschränkungen: Die Miniaturen zeigen bis zum Speichern die ursprünglichen Werte. Textfelder auf Seiten, die
schon in der Datei gedreht sind, werden beim Bearbeiten nicht mitgedreht. Formulare eingefügter Seiten werden
angezeigt, lassen sich aber erst nach Speichern und erneutem Öffnen ausfüllen. Nicht unterstützt werden
XFA-Formulare, JavaScript (Berechnungen, Prüfungen, Formate), Schaltflächen mit Aktionen und digitale
Signaturfelder; eigene Schriften des Formulars werden für neue Feldinhalte durch Helvetica ersetzt.

### 5.1a Unterschrift

Mit dem Werkzeug **Unterschrift** setzen Sie eine zuvor geschriebene Unterschrift auf die Seite. Ist noch keine
gespeichert, öffnet die Wahl des Werkzeugs den Dialog **Unterschriften**:

- Links stehen fünf Plätze mit Vorschau, Name und × zum Löschen, darunter „1 von 5“.
- Rechts liegt das Unterschriftenfeld (weiß, mit Grundlinie und ×). Geschrieben wird mit Stift oder Maus; auf Wunsch
  folgt die Strichstärke dem Andruck. Farben: Dunkelblau (Standard), Schwarz, Blau, Dunkelrot, Rot. „Feld leeren“
  beginnt neu, **Speichern** legt die Unterschrift auf dem gewählten Platz ab.
- Die Unterschriften werden als Linien (nicht als Bild) in `signatures.json` neben der Einstellungsdatei gespeichert
  und verlassen den Rechner nicht.

In der Optionsleiste wählen Sie die Unterschrift (Vorschau anklicken), legen mit **+ Neue** eine weitere an oder
öffnen den Dialog mit **Verwalten…**. Über der Seite folgt eine blasse Vorschau dem Mauszeiger. Ein **Klick** setzt
die Unterschrift 150 pt breit mittig auf den Klickpunkt, **Ziehen** bestimmt die Breite (das Seitenverhältnis
bleibt). Eine gesetzte Unterschrift lässt sich mit dem Werkzeug verschieben und mit dem Radierer entfernen; jede
Aktion ist ein Rückgängig-Schritt. Gespeichert wird sie als Standard-Annotation „Ink“ und bleibt in PDF Utopia
bearbeitbar. In der Anmerkungsübersicht steht sie als „Unterschrift“ (Filter „Freihand“).

> **Wichtig:** Das ist eine **sichtbare** Unterschrift wie auf Papier, **keine digitale (kryptografische)
> Signatur**. Sie belegt nicht, wer unterschrieben hat, und nicht, dass das Dokument danach unverändert ist.

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
- **Drehen** dreht um 90° im Uhrzeigersinn; Annotationen drehen mit. Mit **Strg+Klick** auf einen Drehen-Knopf
  (Werkzeugleiste oder Miniatur) dreht es gegen den Uhrzeigersinn; solange Strg gedrückt ist, zeigen diese Knöpfe
  ein gespiegeltes Symbol. Im Menü **Bearbeiten** und im Kontextmenü der Seitenliste gibt es dafür einen eigenen
  Eintrag „gegen den Uhrzeigersinn“.
- **Blättern** in der Seitenansicht mit Mausrad oder Tasten (siehe 6). Ist die Seite größer als die Ansicht, scrollt
  das Mausrad zuerst innerhalb der Seite; erst die nächste Drehung am oberen bzw. unteren Rand wechselt die Seite.
  Rückwärts landet man am unteren Rand der vorigen Seite. Nach einem Seitenwechsel per Mausrad werden
  Radbewegungen 0,3 Sekunden lang nicht ausgewertet, damit ein Schwung auf dem Touchpad nicht mehrere Seiten
  überspringt.
- **Löschen:** Die letzte verbleibende Seite eines Dokuments kann nicht gelöscht werden. Annotationen einer
  gelöschten Seite bleiben für „Rückgängig“ erhalten.
- **Leere Seite:** **Bearbeiten → Leere Seite einfügen** (Strg+Umschalt+N) oder Rechtsklick auf eine Miniatur →
  „Leere Seite danach einfügen“. Die Seite kommt hinter die angezeigte bzw. angeklickte Seite, in deren
  angezeigter Größe und Ausrichtung, und wird angezeigt. In der Seitenliste steht „(leer)“. Ein Rückgängig-Schritt;
  beim Speichern entsteht eine echte leere Seite.
- **Mehrfachauswahl** in der Seitenliste: **Strg+Klick** nimmt eine Seite hinzu oder heraus, **Umschalt+Klick**
  wählt den Bereich ab der zuletzt angeklickten Seite, **Strg+A** alle Seiten (wenn die Seitenliste angeklickt
  wurde), **Esc** oder ein einfacher Klick hebt die Auswahl auf. Gewählte Seiten haben einen Rahmen in der
  Akzentfarbe, die Statusleiste zeigt „3 Seiten ausgewählt“. **Drehen** und **Löschen** (Werkzeugleiste, Menü,
  Knöpfe an der Miniatur, **Entf** in der Seitenliste) wirken auf alle gewählten Seiten – je ein Rückgängig-Schritt,
  mindestens eine Seite bleibt. **Ziehen** einer gewählten Seite verschiebt alle gewählten als Block vor bzw.
  hinter die Zielseite. **Extrahieren** ist mit der Auswahl vorbelegt (z. B. „2-4,7“). Gehört die angeklickte bzw.
  angezeigte Seite nicht zur Auswahl, wirken die Befehle nur auf sie.
- **Kontextmenü:** Rechtsklick auf eine Miniatur bietet Drehen, Löschen, Extrahieren und „Leere Seite danach
  einfügen“.

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

### 5.5a Seiten aus anderer PDF-Datei einfügen

Das Gegenstück zum Extrahieren: **Einfügen** (Werkzeugleiste), **Datei → Seiten einfügen…** oder **Strg+I**.
Alternativ eine PDF-Datei aus dem Dateimanager **auf die Seitenliste ziehen**; eine Linie zeigt, wo die Seiten
hinkommen. Wird die Datei woanders im Fenster abgelegt, öffnet sie sich wie bisher in einem eigenen Tab.

| Einstellung | Bedeutung |
|---|---|
| Quelle | eine Datei („Datei wählen…“) oder ein anderes geöffnetes Dokument. Bei einem geöffneten Dokument wird der zuletzt **gespeicherte** Stand eingefügt; hat es ungespeicherte Änderungen, weist der Dialog darauf hin. |
| Passwort | erscheint nur bei geschützten Dateien. Es wird nur zum Lesen gebraucht. Bei geöffneten Dokumenten ist es schon bekannt. |
| Seiten der Quelle | Eingabe wie beim Extrahieren (Regeln in 5.5); vorgewählt sind alle Seiten (z. B. `1-13`). Ein Klick auf eine Miniatur nimmt die Seite in die Auswahl auf oder heraus. Die Knöpfe **Alle**, **Keine** und **Umkehren** wählen alle Seiten, heben die Auswahl auf oder kehren sie um. |
| Einfügen | **nach der aktuellen Seite** (Standard), am Anfang, am Ende oder nach einer bestimmten Seite. Darunter steht, welche Positionen die neuen Seiten bekommen. |

Nach dem Einfügen wird die erste neue Seite angezeigt. Das Einfügen ist **ein** Rückgängig-Schritt. Die Seiten
bringen ihre Anmerkungen mit: von PDF Utopia gespeicherte sind danach bearbeitbar, die anderer Programme bleiben
unverändert sichtbar. Eingefügte Seiten lassen sich wie alle anderen umsortieren, drehen, löschen, bearbeiten,
extrahieren und durchsuchen. Gespeichert wird mit dem Passwortschutz des aktuellen Dokuments.

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

Kommentare, Marker, Freihandlinien, Formen und Texte werden als **echte PDF-Annotationen** gespeichert
(Square, Circle, Line, Highlight, Ink, FreeText) – mit eigenem Erscheinungsbild, damit sie in jedem PDF-Betrachter
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
wieder eine gewählt wird. Der Zoom lässt sich auch direkt in das Feld zwischen − und + eintippen, z. B. `150`,
`150 %` oder `87,5`; **Enter** übernimmt den Wert (begrenzt auf 30–400 %). Ungültige Eingaben, **Esc** oder das
Verlassen des Felds ohne Enter stellen den aktuellen Zoom wieder her. Zoomen gibt es nur in der Statusleiste,
im Menü **Ansicht** und per Tastatur, nicht mehr in der Werkzeugleiste.

### 5.10b Lesezeichen

Hat die Datei Lesezeichen (Inhaltsverzeichnis), erscheint in der linken Leiste der Reiter **Lesezeichen**. Er zeigt
die Lesezeichen als Baum; die erste Ebene ist aufgeklappt, tiefere Ebenen wie in der Datei vorgegeben. Ein Klick
zeigt die Zielseite – auch nach Umsortieren oder Einfügen von Seiten. Das Lesezeichen des Kapitels, in dem die
angezeigte Seite liegt, ist markiert. Lesezeichen, deren Zielseite gelöscht wurde, und Gliederungspunkte ohne Ziel
sind ausgegraut.

Beim **Speichern** und **Extrahieren** bleiben die Lesezeichen erhalten und zeigen auf die neue Seitenfolge.
Lesezeichen gelöschter (bzw. nicht extrahierter) Seiten entfallen, ihre Unterpunkte rücken an ihre Stelle;
Gliederungspunkte ohne Ziel bleiben, solange sie Unterpunkte haben. Lesezeichen lassen sich nicht anlegen oder
umbenennen.

### 5.10a Anmerkungsübersicht

Der Reiter **Anmerkungen** in der linken Leiste erscheint, sobald das Dokument Anmerkungen hat; ohne Anmerkungen
zeigt die Leiste nur die Seitenliste, und nach dem Entfernen der letzten Anmerkung kehrt sie dorthin zurück. Er
listet alle Anmerkungen des Dokuments, nach Seiten gruppiert in
der aktuellen Seitenreihenfolge. Je Seite stehen zuerst die Kommentare in Fußnotenreihenfolge, dann alle übrigen
in der Reihenfolge, in der sie gezeichnet wurden.

| Art | Eintrag |
|---|---|
| Kommentar | Fußnotenzahl und Kommentartext |
| Markierung | „Markierung:“ und der markierte Text der Seite; bei Seiten ohne Textebene (Scans) nur „Markierung“ |
| Text | der Text |
| Freihand, Rechteck, Ellipse, Linie, Pfeil | die Art (gedämpft) |

Jeder Eintrag zeigt das Symbol der Art in der Akzentfarbe und einen Punkt in der Farbe der Anmerkung. Lange Texte
werden nach zwei Zeilen gekürzt. Oben lässt sich nach Art filtern (Alle Arten, Kommentare, Markierungen,
Freihand, Formen, Text). Ein Klick zeigt die Seite, holt die Anmerkung in den sichtbaren Bereich und hebt sie
1,6 Sekunden lang mit einem gestrichelten Rahmen in der Akzentfarbe hervor; der Eintrag bleibt markiert. Die
Übersicht folgt jeder Änderung, auch Rückgängig, Umsortieren und eingefügten Seiten. Anmerkungen anderer
Programme erscheinen nicht, weil sie nicht bearbeitbar sind.

### 5.11 Mehrere Dokumente

Jedes Dokument öffnet in einem eigenen Tab. Eine bereits geöffnete Datei wird nicht doppelt geöffnet – ihr Tab
wird angezeigt. Beim Schließen eines Tabs oder des Fensters mit ungespeicherten Änderungen fragt PDF Utopia:
**Speichern**, **Verwerfen** oder **Abbrechen**.

### 5.12 Erscheinungsbild, Sprache und Farben

- **Design:** System, Hell oder Dunkel (Menü **Ansicht** oder Einstellungen). Das helle Design verzichtet auf
  reines Weiß, das dunkle ist neutral grau; die PDF-Seite selbst bleibt immer weiß.
- **Sprache:** Deutsch oder Englisch (Menü **Sprache**). Ohne eigene Wahl richtet sie sich nach dem
  Betriebssystem – Deutsch bei deutscher Systemsprache, sonst Englisch.
- **Farbwahl** (Akzentfarbe sowie Farben für Freihand, Formen, Kommentar und Text):

  | Gruppe | Inhalt |
  |---|---|
  | Pastell | Pastellblau, Mint, Pastellgelb, Pfirsich, Rosé, Lavendel; Sättigung 10–90 % (Standard 30 %) |
  | Kräftig | Indigo, Blau, Grün, Gelb, Orange, Rot, Violett |
  | Neutral | 95 % Schwarz, 95 % Weiß |
  | Benutzerdefiniert | eigene Farben per Hex-Code (`#RRGGBB`), höchstens 10, die älteste fällt bei mehr weg |

  Sättigung und eigene Farben gelten gemeinsam für alle Ziele. **Standard** setzt das jeweilige Ziel auf seine
  Standardfarbe zurück (Akzent und Kommentar Indigo, Freihand Rot, Formen Blau, Text fast Schwarz). Schrift auf der Akzentfarbe
  wird automatisch hell oder dunkel gesetzt; hebt sich eine Akzentfarbe zu wenig vom Hintergrund ab, bekommen
  Akzentflächen einen Rahmen in einer satteren Variante der Farbe (bei neutralen Farben in Textfarbe).

---

## 6. Tastenkürzel

| Kürzel | Befehl |
|---|---|
| Strg+O | Öffnen |
| Strg+Umschalt+S | Speichern unter |
| Strg+E | Seiten extrahieren |
| Strg+I | Seiten aus anderer PDF-Datei einfügen |
| Strg+Umschalt+N | Leere Seite nach der angezeigten Seite einfügen |
| Strg+Klick / Umschalt+Klick (Seitenliste) | Seite zur Auswahl hinzunehmen / Bereich wählen |
| Strg+A / Entf / Esc (Seitenliste) | alle Seiten wählen / gewählte Seiten löschen / Auswahl aufheben |
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

Gespeicherte Unterschriften liegen im selben Ordner in `signatures.json`.

---

## 8. Datenschutz und Sicherheit

- PDF Utopia arbeitet vollständig lokal; es werden keine Daten übertragen.
- Unterschriften werden nur lokal in `signatures.json` gespeichert (siehe 7) und lassen sich im Dialog
  **Unterschriften** jederzeit löschen.
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
| – | Beim Einfügen von Seiten werden Lesezeichen und Verknüpfungen der eingefügten Datei nicht übernommen; ihre Formularfelder schon (gleichnamige werden umbenannt). Aus einem geöffneten Dokument wird der zuletzt gespeicherte Stand eingefügt. |

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
