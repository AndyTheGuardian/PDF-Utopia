# PDF Utopia – Funktionsübersicht

Verdichteter Chatverlauf mit Fokus auf den eingebauten Funktionen. Stand: 2026-11-06.

**Grundlage:** C# / .NET 10 mit Avalonia 12. PDFium zeigt die Seiten an, PDFsharp 6.2.4 schreibt die Datei. Alle
Änderungen werden als normale PDF-Annotationen gespeichert, mit Erscheinungsbild, damit jeder PDF-Betrachter sie
zeigt. Zusätzlich bekommt jede Anmerkung eigene Daten mit, damit PDF Utopia sie später weiter bearbeiten kann.

## Phase 1 – Grundversion (abgenommen am 2026-11-03)

- **Kommentare mit Fußnoten:** Ein Klick auf einen Absatz oder ein aufgezogener Bereich erzeugt einen nummerierten
  Kommentar. Der Text steht als Fußnote auf der nach unten verlängerten Seite. Entfernen per ×.
- **Marker, Freihand, Rechteck (abgerundet):** mit Maus oder Stift. Freihand ist auf Wunsch druckempfindlich und
  lässt sich glätten.
- **Radierer:** Ein Strich entfernt jede Anmerkung, die er berührt, ganz. Er funktioniert als Werkzeug und über
  das Radiererende des Stifts, mit Radierkreis schon beim Schweben.
- **Seiten:** per Drag & Drop umsortieren, drehen und löschen.
- **Extrahieren:** Auswahl wie `2,4-8`, mit Vorschau.
- **Bedienung:** Rückgängig/Wiederholen (Anzahl einstellbar), Ansichten Volle Höhe/Volle Breite/Ganze Seite,
  Suche mit hinterlegten Treffern, Tabs für mehrere Dokumente.
- **Dateien:** Passwortschutz (öffnen, beim Speichern behalten/ändern/entfernen), Speichern immer per
  „Speichern unter“, Drucken über den PDF-Betrachter des Systems.
- **Oberfläche:** System/Hell/Dunkel, Akzentfarbe, Deutsch/Englisch. In der Seitenliste zeigen Symbole, welche
  Anmerkungen eine Seite hat. Blättern mit Mausrad und Pfeiltasten.
- **Lieferung:** Programm als einzelne Datei; Anwenderdokumentation, Benutzerhandbuch und README.

## Phase 2 – Ergänzungen danach

- Die Seitenliste scrollt automatisch zur aktuellen Seite.
- **Seiten aus anderer PDF-Datei einfügen:** per Dialog oder Drag & Drop. Anmerkungen kommen mit,
  Standardposition ist nach der aktuellen Seite.
- Der Ungespeichert-Punkt im Tab verschwindet, sobald wieder der gespeicherte Stand erreicht ist.
- Im Einfügen-Dialog gibt es „Auswahl aufheben“ und „Auswahl umkehren“.

## Phase 3 – Weitere Funktionen

Entwickelt im Branch `feature/weitere-funktionen`; für jede Funktion wurde zuerst ein Plan (bei neuer Oberfläche
ein Mockup) vorgelegt und erst nach Freigabe umgesetzt.

1. **Text-Werkzeug:** freier Text auf der Seite (FreeText).
2. **Formen:** ein Werkzeug für Rechteck, Ellipse, Linie und Pfeil. Pfeilspitzen sind offen oder geschlossen, am
   Anfang, am Ende oder an beiden Enden. Linien rasten in 15°-Schritten ein.
3. **Anmerkungsübersicht:** nach Seiten gruppiert, mit Filter nach Art; ein Klick springt zur Anmerkung. Die
   Reiter erscheinen nur, wenn es Anmerkungen gibt.
4. **Lesezeichen:**
   - Sie erscheinen als Baum und bleiben beim Speichern erhalten.
   - Die linke Leiste lässt sich in der Breite ändern, und die Miniaturen wachsen mit.
   - Der Zoom sitzt nur noch in der Fußleiste und lässt sich dort eintippen.
5. **Leere Seite und Mehrfachauswahl:**
   - Mehrere Seiten lassen sich mit Strg/Umschalt+Klick auswählen.
   - Strg+Klick dreht gegen den Uhrzeigersinn; das Symbol wird dabei gespiegelt und behält seine Größe.
6. **Unterschrift:** eigenes Werkzeug mit bis zu fünf gespeicherten Unterschriften, gesetzt als Stempel. Zwei
   weitere Farben: Dunkelrot und Hellrot.
7. **Formulare:**
   - 7a: Formulare bleiben beim Speichern und Extrahieren erhalten, Felder werden angezeigt.
   - 7b: Ausfüllen direkt auf der Seite (Text, Kästchen, Optionen, Listen), mit Schalter „Felder hervorheben“,
     Tab-Reihenfolge und Rückgängig je Feld.

## Noch offen

- Funktion 8 Bild/Stempel, 9 Schwärzen, 10 Texterkennung.
- Lizenzfrage (F24) und der Test des Radiererendes am Stift (A27), siehe `OFFENE-PUNKTE.md`.
