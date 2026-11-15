# PDF Utopia – Übergabe

Stand: 2026-11-15

Dieses Dokument enthält alles, um die Arbeit an PDF Utopia von einem anderen Konto (Mensch oder KI-Assistent) aus
fortzusetzen. Es ergänzt die übrige Dokumentation und wiederholt sie nicht. Lesereihenfolge für den Einstieg:

1. dieses Dokument,
2. [README.md](README.md) (Funktionen, Bauen, Bibliotheken),
3. [OFFENE-PUNKTE.md](OFFENE-PUNKTE.md) (Projekttagebuch: alle Entscheidungen, Annahmen, Einschränkungen),
4. [docs/ENTWICKLERDOKUMENTATION.md](docs/ENTWICKLERDOKUMENTATION.md) (Architektur, Kernkonzepte, Konventionen,
   Fallstricke).

---

## 1. Projektstand

| Thema | Stand |
|---|---|
| Programm | Funktionen 1–13 abgenommen und in `main`; zuletzt Funktion 13 **Komprimieren** (A57), abgenommen am 2026-11-15 |
| Aktueller Branch | `main` |
| Tests | Core 496, Pdf 163 (davon 3 nur ausdrücklich), App 35 – alle grün |
| Testversion | `publish/single-linux-x64/PdfUtopia` (Linux x64, Einzeldatei, Stand Funktion 13) |
| Offene Fragen | F24 Lizenz (zurückgestellt, Vorschlag MIT), F47 Android-Version (zurückgestellt) |
| Probleme | keine offenen |
| Repository | `https://github.com/AndyTheGuardian/PDF-Utopia.git` (Remote `origin`) |

### Branches

| Branch | Inhalt | In `main` |
|---|---|---|
| `main` | Funktionen 1–13 | – |
| `feature/komprimieren`, `feature/farbprofile`, `feature/seitenansichten`, `feature/weitere-funktionen` | frühere Funktionen | ja (können gelöscht werden) |

**Vor dem Wechsel pushen:** Der Anwender hat `main` am 2026-11-15 bis zum Merge von Funktion 13 auf GitHub
veröffentlicht; die danach entstandenen Abnahme-Commits sind nur lokal. Wer von einem anderen Rechner aus
weiterarbeiten will, muss vorher pushen (der Anwender pusht selbst, die Zugangsdaten liegen bei ihm). Ob etwas
fehlt, zeigt `git status` nach `git fetch`:

```bash
git push origin main
```

### Nächste Schritte

Es ist keine Arbeit angefangen. Mögliche Themen:

1. `main` auf GitHub pushen (macht der Anwender).
2. Zurückgestellte Fragen wieder aufgreifen, wenn der Anwender es wünscht: F24 Lizenz, F47 Android-Version.
3. Neue Funktionen wie gewohnt auf `feature/<name>` beginnen (Ablauf in Abschnitt 2); die Abnahme-Checkliste
   bekommt dafür einen neuen Abschnitt 14.
4. Erledigte Feature-Branches können gelöscht werden (nur auf Wunsch des Anwenders).

---

## 2. Arbeitsweise mit dem Anwender

### Projektregeln

Die Regeln stehen in `/home/andy/Claude/CLAUDE.md` – **außerhalb** des Repositorys. Hier der Inhalt sinngemäß, damit
er auf einem anderen Konto verfügbar ist:

- Rolle: erfahrener Entwickler für C#, KI- und Webtechnologien; lesbarer, testbarer Clean Code.
- Bei ungenauer oder vager Vorgabe **immer nachfragen**; auf Fehler und Ungenauigkeiten hinweisen.
- Vorschläge auf Best Practices der Softwareentwicklung beschränken.
- **OFFENE-PUNKTE.md** im selben Arbeitsschritt pflegen, in dem ein Punkt entsteht oder geklärt wird; Stand-Datum
  anpassen; jeder Punkt hat ID und Status; Erledigtes mit Datum nach „Erledigt“. Offene Fragen zusätzlich im Chat
  stellen.
- Nach der Abnahme: Entwickler-, Anwenderdokumentation und Benutzerhandbuch erstellen bzw. nachziehen.

### Ablauf je Funktion

1. Anfrage des Anwenders → Plan mit Rückfragen (F-Nummern in OFFENE-PUNKTE.md).
2. HTML-Mockup in `mockups/mockup-vN-<name>.html` (im Browser vorführen; Server: `.claude/launch.json`, Port 8799).
3. Mockup-Abnahme (M-Nummer) – erst dann umsetzen.
4. Umsetzung auf einem Branch `feature/<name>`, mit Unit-Tests.
5. Selbst testen in einer eigenen Instanz (siehe Abschnitt 4).
6. Dokumentation nachziehen: README, Anwenderdokumentation, Benutzerhandbuch, Entwicklerdokumentation,
   Funktionsübersicht, Abnahme-Checkliste (neuer Abschnitt), OFFENE-PUNKTE.md.
7. **Commit nur auf Aufforderung**; danach Testversion bauen (`publish/single-linux-x64`).
8. Abnahme durch den Anwender anhand der Checkliste → **Merge nur auf Aufforderung**.

### Vereinbarungen und Vorlieben

- Der Anwender schreibt im Chat meist Englisch; Antworten auf Englisch. **Dokumentation, Oberflächentexte (Standard
  Deutsch) und Code-Kommentare deutsch**, Bezeichner und Commit-Nachrichten englisch.
- Commit-Nachrichten enden mit der Zeile `Co-Authored-By: <Assistent> <noreply@anthropic.com>`.
- **Pushen nur auf Aufforderung** (Zugangsdaten beim Anwender).
- `SPECS.md` (ursprüngliche Spezifikation) **nicht ändern**.
- Passwörter (auch sudo) gibt der Anwender selbst ein.
- Die E-Mail-Adresse des Anwenders nicht an fremde Dienste weitergeben.
- Das Projekt **AI Asset Manager** (`/home/andy/Claude/AI Asset Manager`) nicht verändern; es dient nur als Vorlage
  (Farbprofile, Austauschformat). Weitere Vorlage: FileRenamer (`/home/andy/Projects/FileRenamer`, Akzentfarbe A14).
- Die **laufende Instanz des Anwenders und seine Einstellungen nicht anfassen**; vor dem Überschreiben von
  `publish/single-linux-x64/PdfUtopia` prüfen, dass sie nicht läuft (`ps -eo pid,args | grep PdfUtopia`).
- Prozesse gezielt per PID beenden (`ps -eo pid,args | awk …`), nicht mit `pkill -f`.

---

## 3. Entwicklungsumgebung

| Werkzeug | Version / Hinweis |
|---|---|
| Betriebssystem | Linux Mint (Cinnamon, X11) |
| .NET SDK | 10.0.114 (`/usr/lib/dotnet`), `global.json` → Microsoft Testing Platform |
| Tesseract | 5 mit `deu`, `eng`, `osd` (für Texterkennung und einen Test) |
| Xephyr, metacity | für Oberflächentests in einem verschachtelten X-Server |
| Poppler (`pdftoppm`) | Sichtprüfung gespeicherter PDFs |
| Python 3 | Mockup-Server, Hilfsskripte (Eingaben per XTEST, Bildschirmfotos per Xlib) |

Der Rechner hat **einen** Prozessorkern – Laufzeiten (z. B. Komprimieren) sind auf Rechnern des Anwenders kürzer.

Bauen, Starten, Testen und Veröffentlichen: siehe README bzw. Entwicklerdokumentation Abschnitt 2. Warnungen gelten
als Fehler. Bei langsamen Testläufen die Test-Executables direkt starten, z. B.
`tests/PdfUtopia.Pdf.Tests/bin/Debug/net10.0/PdfUtopia.Pdf.Tests`.

---

## 4. Oberfläche testen, ohne den Anwender zu stören

```bash
Xephyr :99 -screen 1280x800 &
DISPLAY=:99 metacity &
DISPLAY=:99 XDG_CONFIG_HOME=/tmp/pdfutopia-test dbus-run-session -- dotnet run --project src/PdfUtopia.App -- testdata/testdatei.pdf
```

- **`dbus-run-session` ist Pflicht:** Avalonia öffnet Dateidialoge über das xdg-desktop-portal auf dem
  Sitzungs-D-Bus. Ohne eigenen Bus erscheint der Dialog auf dem echten Bildschirm des Anwenders (passiert am
  2026-11-15). Erscheint doch einer, schließt ihn das Beenden der Testinstanz.
- Eigenes `XDG_CONFIG_HOME`, damit Einstellungen, Farbprofile und Unterschriften des Anwenders unberührt bleiben.
- Kein `muffin` starten; `metacity` im Xephyr ist in Ordnung.
- Keine Oberflächentests auf dem echten Bildschirm (`:0`).
- Nach dem Test Xephyr und die Testinstanz beenden.

---

## 5. Was nicht im Repository liegt

| Was | Wo | Bedeutung |
|---|---|---|
| Projektregeln | `/home/andy/Claude/CLAUDE.md` | Inhalt siehe Abschnitt 2 |
| Chatverläufe | `Chatverlauf-PDF-Utopia.pdf`, `Chatverlauf-PDF-Utopia-Teil-2.pdf` im Projektordner (nicht versioniert) | vollständiger Entwicklungsverlauf; das Wesentliche steht in OFFENE-PUNKTE.md und der Funktionsübersicht |
| Mockup-Server | `.claude/launch.json` (nicht versioniert) | `python3 -m http.server 8799 --bind 127.0.0.1 --directory mockups` |
| Gedächtnis des Assistenten | `~/.claude/projects/-home-andy-Claude/memory/` | eine Notiz: Oberflächentests unter `dbus-run-session` (in Abschnitt 4 enthalten) |
| Build-Ausgaben | `bin/`, `obj/`, `publish/` | per `.gitignore` ausgeschlossen, jederzeit neu erzeugbar |

---

## 6. Wichtige technische Leitplanken (Kurzfassung)

Ausführlich in der Entwicklerdokumentation, Abschnitt 11 „Fallstricke“.

- **PDFsharp bei 6.2.4 lassen** – ab 6.3 AGPL mit Lizenzprüfung zur Laufzeit (A21).
- PDFium nur unter `PdfiumLibrary.Sync` aufrufen (nicht threadsicher).
- Eigene Annotationen tragen ihre Daten in `/PdfUtopiaData`; nur so sind sie beim Wiederöffnen bearbeitbar (A25).
- Speichern ist immer „Speichern unter“, atomar über eine temporäre Datei; die Originaldatei bleibt erhalten.
- Neue Anmerkungsarten: Entwicklerdokumentation Abschnitt 9, und in `RoundTripTests.AllKinds` aufnehmen.
- Oberflächentexte immer in `Localization/Strings.cs` für Deutsch **und** Englisch.
- Neue Oberfläche erst als Mockup abnehmen lassen.
