# Customer requirements: Bausteine Farbblock, Linientabelle und eckige Knöpfe

Format: 2

## Background

Die Marke setzt auf Farbblöcke für Einnahmen, Festes und Freies, Linien statt Schatten-Karten und eckige Knöpfe. Heute gibt es dafür keine wiederverwendbaren Bausteine, jede Seite baut sich ihre Darstellung selbst (Rundgang 29.10.2026, Marken-Runde).

## Goal

Farbblock, Linientabelle und Knöpfe stehen als Bausteine bereit, hell und dunkel, mit Fokus und Beschriftung für Screenreader.

## Users

Das Team beim Bauen der Seiten; mittelbar alle Personen, die Plagevo benutzen.

## Requirements

R1 (must): Ein Baustein Farbblock zeigt Titel und Betrag in den Varianten Einnahmen, Festes und Frei mit den Farben des Themes — because der Plan aus drei Blöcken das Kernbild der Marke ist.
R2 (must): Ein Baustein Linientabelle zeigt Zeilen mit Bezeichnung und Betrag, trennt sie mit Linien und setzt Beträge mit tabellarischen Ziffern — because Zahlen in Spalten untereinander stehen müssen.
R3 (must): Knöpfe gibt es primär, sekundär und gefährlich, eckig, mit sichtbarem Fokus von 3 Pixeln in Tintenfarbe — because Bedienbarkeit per Tastatur Pflicht ist.
R4 (must): Jeder Baustein ist für Screenreader sinnvoll beschriftet und erreicht in Hell und Dunkel mindestens 4,5 zu 1 Kontrast — because Barrierefreiheit gefordert ist.
R5 (should): Eine Testseite zeigt alle Bausteine hell und dunkel nebeneinander und ist nur im Entwicklungsbetrieb erreichbar — because das Team sie so prüfen kann, ohne dass sie in die Produktion gelangt.

## Constraints

Baut auf den Themes aus #3 und #4 auf. Keine neuen Farben, nur die des Themes. Keine Änderung der bestehenden Seiten in dieser Aufgabe.

## Open questions

None.
