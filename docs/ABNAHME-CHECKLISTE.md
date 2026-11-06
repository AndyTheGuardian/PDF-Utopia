# PDF Utopia – Abnahme-Checkliste Funktionen 1–10

Stand: 2026-11-06 · Branch `feature/weitere-funktionen` · Testversion: `publish/single-linux-x64/PdfUtopia`

Jede Zeile: Schritte ausführen, Ergebnis vergleichen, abhaken. Abweichungen bitte mit Nummer melden (z. B. „4.3:
Lesezeichen springt auf falsche Seite“).

## Vorbereitung

- [ ] **0.1** Testversion starten: `publish/single-linux-x64/PdfUtopia`
- [ ] **0.2** Testdateien liegen in `testdata/`: `testdatei.pdf` (14 Seiten, Seite 3 in der Datei gedreht),
      `testdatei-lesezeichen.pdf`, `testdatei-formular.pdf`, `testdatei-geschuetzt.pdf` (Passwort `utopia`)
- [ ] **0.3** Für Funktion 10 eine gescannte PDF ohne Text bereithalten (oder eine Seite vorher schwärzen und
      speichern – geschwärzte Seiten sind reine Bilder)
- [ ] **0.4** Tesseract ist installiert (`tesseract --list-langs` zeigt `deu`, `eng`, `osd`)

Am Ende jedes Abschnitts: **Speichern unter** in eine neue Datei, diese wieder öffnen und prüfen, ob alles noch da
und bearbeitbar ist (außer bei Schwärzen – dort ist das Ergebnis endgültig).

## 1. Text-Werkzeug

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 1.1 | Werkzeug **Text**, auf die Seite klicken, tippen, daneben klicken | Text steht dort, ein Rückgängig-Schritt | ☐ |
| 1.2 | Bereich waagerecht aufziehen, langen Text tippen | Text bricht auf die gezogene Breite um | ☐ |
| 1.3 | Farbe und Schriftgröße in der Optionsleiste ändern, während der Text bearbeitet wird | Gilt sofort für den Text | ☐ |
| 1.4 | Gesetzten Text ziehen; Text anklicken und ändern | Verschiebt bzw. bearbeitet ihn | ☐ |
| 1.5 | Seite drehen, neuen Text setzen | Neuer Text steht aufrecht, alter dreht mit | ☐ |
| 1.6 | Speichern, in einem anderen PDF-Betrachter öffnen | Text sieht gleich aus | ☐ |

## 2. Formen (Rechteck, Ellipse, Linie, Pfeil)

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 2.1 | Werkzeug **Formen**, nacheinander Rechteck, Ellipse, Linie, Pfeil aufziehen | Jede Form erscheint in Farbe und Stärke | ☐ |
| 2.2 | Mit **Umschalt** ziehen | Rechteck/Ellipse quadratisch bzw. rund, Linien rasten in 15° ein | ☐ |
| 2.3 | Pfeil: offene/geschlossene Spitze, Ende/Anfang/beide | Spitzen wie gewählt | ☐ |
| 2.4 | Radierer über die Formen | Formen verschwinden | ☐ |

## 3. Anmerkungsübersicht

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 3.1 | Dokument ohne Anmerkungen öffnen | Keine Reiter, nur die Seitenliste | ☐ |
| 3.2 | Einige Anmerkungen anlegen, Reiter **Anmerkungen** | Nach Seiten gruppiert, mit Farbe und Text | ☐ |
| 3.3 | Filter wählen, Eintrag anklicken | Liste gefiltert; Sprung zur Seite, Anmerkung blinkt kurz | ☐ |

## 4. Lesezeichen, linke Leiste, Zoom

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 4.1 | `testdatei-lesezeichen.pdf` öffnen, Reiter **Lesezeichen** | Baum mit Kapiteln und Unterpunkten | ☐ |
| 4.2 | Lesezeichen anklicken | Springt zur richtigen Seite | ☐ |
| 4.3 | Seiten umsortieren/löschen, speichern, wieder öffnen | Lesezeichen zeigen auf die neue Seitenfolge | ☐ |
| 4.4 | Linke Leiste breiter/schmaler ziehen | Miniaturen wachsen mit; Breite bleibt nach Neustart | ☐ |
| 4.5 | Zoom unten rechts eintippen (z. B. 150) + Enter | Seite in 150 % | ☐ |

## 5. Leere Seite und Mehrfachauswahl

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 5.1 | **Strg+Umschalt+N** | Leere Seite nach der aktuellen, gleiche Größe | ☐ |
| 5.2 | Strg+Klick und Umschalt+Klick in der Seitenliste | Mehrere Seiten gewählt, Statusleiste zeigt Anzahl | ☐ |
| 5.3 | Drehen, Löschen, Extrahieren mit Auswahl | Wirkt auf alle gewählten Seiten | ☐ |
| 5.4 | **Strg+Klick** auf „Drehen“ | Dreht gegen den Uhrzeigersinn, Symbol gespiegelt | ☐ |

## 6. Unterschrift

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 6.1 | Werkzeug **Unterschrift** (erstes Mal) | Dialog „Unterschriften“ öffnet sich | ☐ |
| 6.2 | Mit Stift/Maus unterschreiben, Farbe wählen, speichern | Unterschrift in der Optionsleiste | ☐ |
| 6.3 | Klicken bzw. aufziehen, danach verschieben | Gesetzt in Standardbreite bzw. gezogener Breite | ☐ |
| 6.4 | Fünf Unterschriften anlegen, eine löschen | „5 von 5“, danach wieder ein freier Platz | ☐ |

## 7. Formulare

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 7.1 | `testdatei-formular.pdf` öffnen | Hinweisleiste mit Feldanzahl und „Felder hervorheben“ | ☐ |
| 7.2 | Werkzeug **Auswählen**, Textfeld ausfüllen, **Tab** | Springt zum nächsten Feld; ein Rückgängig-Schritt | ☐ |
| 7.3 | Kontrollkästchen und Optionsfeld (auch auf Seite 3) | Wird umgeschaltet, Rückgängig wirkt | ☐ |
| 7.4 | Speichern, in einem anderen Betrachter öffnen | Werte sichtbar, Formular weiter ausfüllbar | ☐ |

## 8. Stempel und Bilder

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 8.1 | Werkzeug **Stempel**, Vorlage wählen, Datum/Name anhaken (Name unter Einstellungen) | Vorschau in der Optionsleiste mit Datum und Name | ☐ |
| 8.2 | Klicken bzw. aufziehen | Stempel gesetzt und angewählt (Rahmen, Eckgriffe, Drehgriff) | ☐ |
| 8.3 | Verschieben, Ecke ziehen, Drehgriff ziehen (mit/ohne Umschalt) | Größe mit festem Seitenverhältnis; Drehung in 15°-Schritten bzw. stufenlos | ☐ |
| 8.4 | **Entf** / **Esc** | Löscht bzw. hebt Auswahl auf | ☐ |
| 8.5 | „+ Eigener Text…“, Stempel anlegen; mit × löschen | Erscheint unter „Eigene“, bleibt nach Neustart | ☐ |
| 8.6 | **Bild…** mit großem PNG (> 2000 px) | Statusleiste meldet Verkleinerung; Transparenz bleibt | ☐ |
| 8.7 | PNG/JPEG aus dem Dateimanager auf die Seite ziehen | Bild an der Ablagestelle, Werkzeug „Stempel“ aktiv | ☐ |
| 8.8 | Bild kopieren (z. B. Bildschirmfoto), **Strg+V** | Bild mittig auf der Seite | ☐ |

## 9. Schwärzen

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 9.1 | Werkzeug **Schwärzen**, Bereich aufziehen | Rot schraffiert, Inhalt noch lesbar | ☐ |
| 9.2 | Begriff eingeben (z. B. „Lorem“), **Alle markieren** | Trefferzahl stimmt, alle Fundstellen markiert, ein Rückgängig-Schritt | ☐ |
| 9.3 | Markierung anklicken, **Entf** | Nur diese Markierung entfernt (nicht die Seite) | ☐ |
| 9.4 | **Speichern unter** | Dialog „Schwärzungen anwenden“ mit Seiten und Hinweis | ☐ |
| 9.5 | Gespeicherte Datei in anderem Betrachter: Text markieren/suchen auf geschwärzten Seiten | Schwarze Flächen; kein Text kopierbar oder auffindbar | ☐ |
| 9.6 | Extrahieren und Drucken mit Schwärzungen | Gleicher Dialog, Ergebnis geschwärzt | ☐ |

## 10. Texterkennung

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 10.1 | Gescannte PDF öffnen | Hinweis „Seiten ohne Text (z. B. Scans): N …“ | ☐ |
| 10.2 | **Texterkennung…**, Standardwerte, **Erkennen** | Fortschritt je Seite; Meldung mit Seiten und Wörtern | ☐ |
| 10.3 | Suche und „Suchen und schwärzen“ auf den erkannten Seiten | Treffer an der richtigen Stelle | ☐ |
| 10.4 | Während der Erkennung **Abbrechen** | Bereits erkannte Seiten bleiben | ☐ |
| 10.5 | Speichern, in anderem Betrachter Text markieren/kopieren | Text liegt unsichtbar unter dem Bild, in Leserichtung | ☐ |
| 10.6 | Tesseract vorübergehend nicht auffindbar (z. B. anderer Rechner) | Dialog „Tesseract nicht gefunden“ mit Installationsweg | ☐ |

## 11. Seitenansichten (Funktion 11, A55)

| Nr. | Schritte | Erwartet | ✓ |
|---|---|---|---|
| 11.1 | `testdatei.pdf` öffnen, in der Statusleiste nacheinander **Einzelseite**, **Fortlaufend**, **Doppelseite**, **Doppelseite fortlaufend** | Eine Seite / alle untereinander / ein Paar / alle Paare; Seite 1 steht bei Doppelseite allein rechts | ☐ |
| 11.2 | Menü **Ansicht → Erste Seite einzeln** ausschalten | Paare 1\|2, 3\|4 …; Eintrag nur bei Doppelseite wählbar | ☐ |
| 11.3 | **Fortlaufend**: mit dem Mausrad scrollen | Statusleiste und Seitenliste folgen der Seite, die die Ansicht füllt; Rahmen um die aktuelle Seite | ☐ |
| 11.4 | **Fortlaufend**: Miniatur von Seite 10 anklicken, dann **Bild↓**, **Ende**, **Pos1** | Ansicht scrollt jeweils zu der Seite | ☐ |
| 11.5 | **Doppelseite**: Mausrad am unteren Rand, **Bild↓** / **Bild↑** | Das Paar wird durch das nächste bzw. vorige ersetzt | ☐ |
| 11.6 | Mit Freihand auf der nicht umrandeten Seite eines Paars zeichnen | Strich erscheint, diese Seite wird aktuell (Rahmen, Statusleiste) | ☐ |
| 11.7 | **Fortlaufend**: Kommentar auf einer Seite setzen | Fußnote steht unter dieser Seite | ☐ |
| 11.8 | **Fortlaufend**: aktuelle Seite drehen | Zoom bleibt, die Seite bleibt im Blick | ☐ |
| 11.9 | Anmerkung einer weit entfernten Seite in der Anmerkungsübersicht anklicken | Ansicht scrollt dorthin, Anmerkung blinkt kurz | ☐ |
| 11.10 | Fenster schmal ziehen | Ansichtsknöpfe zeigen nur Symbole mit Tooltip; breit wieder mit Namen | ☐ |
| 11.11 | PDF Utopia neu starten | Gewählte Ansicht und „Erste Seite einzeln“ sind erhalten | ☐ |

## Ergebnis

- [ ] Alle Punkte bestanden bzw. Abweichungen gemeldet und behoben
- [ ] Abnahme erteilt am: ____________
