# Customer requirements: Alle übrigen Dialoge neu gestalten

Format: 2

## Background

Das Layout der Dialoge ist schwach und sie sind zu lang (Rundgang 29.10.2026). Neben dem Vertrag betrifft das Buchung, Konto, Haushalt und die Plan-Posten.

## Goal

Die Dialoge für Buchung, Konto, Haushalt und Plan-Posten nutzen das neue Dialog-Gerüst und passen auf einen Bildschirm des Handys.

## Users

Alle Personen, die etwas anlegen oder ändern.

## Requirements

R1 (must): Die Dialoge Buchung, Konto, Haushalt und Plan-Posten nutzen das Dialog-Gerüst (#11) und die Bausteine (#6) — because sie einheitlich aussehen sollen.
R2 (must): Kein Dialog ist auf dem Handy höher als ein Bildschirm; was länger wäre, wird in Schritte geteilt — because man nicht in einem Dialog scrollen soll.
R3 (must): Alle bisherigen Funktionen und Prüfungen der Dialoge bleiben erhalten — because nichts verloren gehen darf.
R4 (must): Fokus, Tastatur und Escape funktionieren, Fehler stehen am Feld — because Barrierefreiheit gefordert ist.
R5 (should): Hell und Dunkel sind lesbar — because beide Modi gelten.

## Constraints

Baut auf #11 und #6. Der Vertragsdialog ist #17 und nicht Teil davon. Bestehende Tests werden angepasst, nicht gelöscht.

## Open questions

None.
