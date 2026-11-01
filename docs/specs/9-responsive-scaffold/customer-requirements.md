# Customer requirements: Responsives Gerüst für Handy, Tablet und Desktop

Format: 2

## Background

Das Layout ist für das Smartphone gebaut. Im Browser auf dem Desktop wirkt die Web-Version wie ein gestrecktes Handy (Parkplatz P-034, Rundgang 29.10.2026). Das Layout soll sich der Bildschirmbreite anpassen.

## Goal

Drei klar festgelegte Breitenbereiche (Handy, Tablet, Desktop) bestimmen Inhaltsbreite und Abstände; auf dem Desktop ist der Inhalt begrenzt und zentriert.

## Users

Alle Personen, die Plagevo im Browser oder in der App benutzen.

## Requirements

R1 (must): Es gibt drei Breakpoints: Handy bis 599 px, Tablet 600 bis 1279 px, Desktop ab 1280 px, einmal festgelegt und dokumentiert — because alle Seiten dieselben Umbruchstellen nutzen sollen.
R2 (must): Auf dem Desktop ist die Inhaltsbreite auf höchstens 1180 px begrenzt und zentriert, auf Tablet und Handy füllt sie die Breite mit seitlichem Abstand — because ein gestrecktes Handy-Layout vermieden werden soll.
R3 (must): Auf dem Handy bleibt die untere Navigation, und jede Tippfläche ist mindestens 44 px hoch und breit — because man mit dem Daumen treffen muss.
R4 (must): Bei 360, 768 und 1280 px Breite läuft keine Hauptseite horizontal aus dem Bild — because seitliches Scrollen unbrauchbar ist.
R5 (should): Die Abstände (Rand, Abstand zwischen Blöcken) hängen am Breitenbereich und stehen als wenige Werte an einer Stelle — because spätere Seiten sie wiederverwenden.

## Constraints

Baut auf dem Theme aus #3 und den Bausteinen aus #6. Die Seitennavigation für den Desktop ist Aufgabe #10, Dialoge sind #11. Bestehende Seiten werden nicht umgebaut, nur ihr Rahmen passt sich an.

## Open questions

None.
