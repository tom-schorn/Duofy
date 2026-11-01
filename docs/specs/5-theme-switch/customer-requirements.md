# Customer requirements: Umschalter Hell, Dunkel und wie das Gerät

Format: 2

## Background

Der Umschalter für den Dunkelmodus ist heute ein Knopf mit zwei Zuständen. Die gemerkte Wahl kennt schon den Zustand „nichts gewählt“, dann gilt die Einstellung des Systems. Tom wünscht drei Möglichkeiten (Rundgang 29.10.2026).

## Goal

Man kann zwischen Hell, Dunkel und „wie das Gerät“ wählen, die Wahl bleibt erhalten, und bis dahin gilt das Gerät.

## Users

Alle Personen, die Plagevo oder Duofy benutzen, selten, meist einmal.

## Requirements

R1 (must): Es gibt drei Auswahlmöglichkeiten: Hell, Dunkel und wie das Gerät — because eine Person das Aussehen unabhängig von der Geräteeinstellung wählen können soll.
R2 (must): Die Wahl bleibt nach einem Neustart in Web und App erhalten — because man sie nicht bei jedem Start neu treffen will.
R3 (must): Wer noch nichts gewählt hat, bekommt „wie das Gerät“ — because das die erwartete Voreinstellung ist.
R4 (should): Bei „wie das Gerät“ folgt die Anzeige einer Änderung der Geräteeinstellung, ohne dass die Seite neu geladen wird — because Handys abends automatisch auf dunkel wechseln.
R5 (must): Bereits gespeicherte Wahlen (hell oder dunkel) gelten unverändert weiter — because Personen ihre Einstellung nicht verlieren sollen.

## Constraints

Baut auf den Themes aus #3 und #4 auf. Der Umschalter sitzt dort, wo heute der Knopf sitzt (Kopfzeile, Anmeldeseite). Die Gestaltung folgt den Bausteinen aus #6, soweit vorhanden.

## Open questions

None.
