# Customer requirements: Dialog-Gerüst mit Schritten und Fortschritt

Format: 2

## Background

Dialoge, besonders das Anlegen eines Vertrags, sind zu lang und im Layout schwach (Rundgang 29.10.2026). Tom wünscht Schritte.

## Goal

Ein Baustein für mehrstufige Dialoge mit Fortschritt, fester Weiter-Position und Prüfung je Schritt.

## Users

Das Team beim Bauen der Dialoge; mittelbar alle Personen, die etwas anlegen.

## Requirements

R1 (must): Der Baustein zeigt Schritt x von y und hat Zurück und Weiter an fester Stelle — because man weiß, wo man ist, und die Knöpfe nicht suchen muss.
R2 (must): Auf dem Handy füllt der Dialog den Bildschirm, auf dem Desktop ist er ein breiterer Dialog — because beide Geräte passen sollen.
R3 (must): Eingaben bleiben beim Zurückgehen erhalten — because sonst alles neu getippt werden müsste.
R4 (must): Pflichtfelder werden je Schritt geprüft, Fehler stehen am Feld und werden vorgelesen — because Barrierefreiheit und Klarheit.
R5 (should): Abbrechen mit eingegebenen Daten fragt nach — because nichts versehentlich verloren geht.

## Constraints

Baut auf #6 (Bausteine, Knöpfe). Kein bestehender Dialog wird umgebaut; das Anlegen des Vertrags folgt in #17 bis #19.

## Open questions

None.
