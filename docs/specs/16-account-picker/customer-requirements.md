# Customer requirements: Kontoauswahl mit Symbol und Name

Format: 2

## Background

Wer bucht oder einen Vertrag oder Plan-Posten anlegt, wählt das Konto aus einer Liste, die nur Namen zeigt (Rundgang 29.10.2026). Mit den Kontosymbolen aus #14 soll man das Konto am Bild und am Namen erkennen.

## Goal

Die Auswahl der Konten (gezahlt von, gezahlt zu) in Buchung, Vertrag und Plan-Posten zeigt Symbol und Name.

## Users

Alle Personen, die buchen oder wiederkehrende Posten planen.

## Requirements

R1 (must): Jede Kontoauswahl (gezahlt von und gezahlt zu) zeigt je Konto das Symbol vor dem Namen — because man das Konto auf einen Blick erkennt.
R2 (must): Das gewählte Konto zeigt Symbol und Name auch im geschlossenen Zustand der Auswahl — because man sieht, was gewählt ist.
R3 (must): Konten, die der Person nicht freigegeben oder nicht aktiv sind, erscheinen wie bisher nicht; ein bereits gewähltes, inzwischen nicht aktives Konto bleibt beim Bearbeiten sichtbar — because sich die Regeln nicht ändern sollen.
R4 (must): Lange Kontonamen brechen sauber um oder werden gekürzt, ohne das Layout zu sprengen — because Namen frei wählbar sind.
R5 (must): Die Auswahl ist per Tastatur und Screenreader bedienbar, mit Name je Eintrag und dem Symbol nur als Zierde — because Barrierefreiheit gefordert ist.

## Constraints

Baut auf #14 (Symbol am Konto). Die Dialoge selbst werden nicht umgebaut (#17, #21, #22); hier wird nur die Kontoauswahl getauscht. Die Sicht- und Freigaberegeln bleiben, wie sie sind.

## Open questions

None.
