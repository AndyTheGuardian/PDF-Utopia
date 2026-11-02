# Project name: PDF Utopia

PDF Utopia ist ein universeller und umfangreicher Editor für PFD-Dateien


## Was soll getan werden

Es soll eine PDF Datei geöffnet und diese bearbeitet werden können. Folgende Funktionen soll das Programm beinhalten:
* Text editieren: Texte sollen sich editieren lassen
* Kommentarfunktion: zu Textabschnitten sollen Kommentare gemacht werden können. Hierzu wird der Absatz farbig umrandet und farbig hinterlegt. In der oberen rechten ecke erscheint eine Fußnotenzahl und unter der Seite erscheinen die Fußnoten mit der Erklärung
* Markierfunktion: Mit einem Stylus oder der Maus sollen zeichnungen auf der PDF-Seite möglich sein. Dazu soll für die Bearbeitung ein Overlay erzeugt werden, dass als Canvas dient. In einem Menü sollen Farbe und Strichtärke auswählbar ein. Mögliche Methoden: Marker (der Text soll wie mit einem Textmaker farbig hinterlegt werden, dazu stehen verschiedene, Textmarkern ähnliche Farben zur Verfügung. Freehand (mit der Maus kann im Dokument auf einem extra layer gezeichnet werden). Rechteck ( mit der maus soll ein Rechteck um Objekte gezeichnet werden können)
* Speichern: die Editierte PDF-Datei soll als PDF gespeichert werden
* Extract: es soll möglich sein, einzelne Seiten (Einzeln: 2, Bereich: 4-8 oder Auswahl: 2,4,8,13-15) zu extrahieren und als neue Datei zu speichern
* Umsortieren: die Reihenfolge der Seiten sollen sich interaktiv verändern lassen.

Links soll sich eine liste mit den einzelnen Seiten befinden und rechts davon eine Detailansicht der Seite. Oben soll eine Reihe mit den oben beschriebenen Funktionen.

Das Programm soll einen Light- und Dark-mode unterstützen. Als Auswahl für das Theme sollen drei Möglichkeiten bestehen: System, Light, Dark.


## Was soll benutzt werden

Erstelle das Programm in C# und Avalonia. Findest Du eine bessere Umsetzungsmöglichkeit, schlage sie bitte vor und begründe sie. Benutze dabei so wenig Abhängigkeiten wie möglich. Wenn möglich achte darauf, dass Versionen verwendet werden, die auf allen Platformen unterstützt werden.


## Wer nutzt das Projekt auf welcher Platform

Das Projekt kann von jedem benutzt werden. Es soll Platformübergreifend auf Windows, Linux und macOS (ab version 10.13) laufen.


## Wie wird der Erfolg geprüft

Das Projekt hat folgende Bewertungsrichlinien:
1. Gestaltung
2. Fachliche korrektheit

Für die Gestaltung erzeuge bitte zunächst ein Mockup, das abgenommen werden muss. Erzeuge bitte der Reihe nach Mockups, bis eins genehmight wird.

Die Korrektheit wird anhand von Tests gemessen. Erzeuge einen Ordner mit Testdateien, die die Verschiedenen Umbennenungsverfahren abbilden.

Prüfe die Korrektheit der Umbenennungen. Sowohl in der Vorschau, als auch bei der tatsächlichen Umbenennung.