# Customer requirements: Plan-Übersicht mit Monatsauswahl

Format: 2

## Background

Wer auf Plan tippt, landet heute direkt im laufenden Monat. Tom wünscht eine Übersichtsseite davor, auf der man auch den Vormonat wählen kann (Rundgang 29.10.2026).

## Goal

Plan öffnet zuerst eine Übersicht der Monate, aus der man einen Monat wählt.

## Users

Alle Personen, die ihren Plan ansehen, mehrmals pro Woche.

## Requirements

R1 (must): Die Adresse /plan zeigt eine Übersicht der Monate und nicht direkt den laufenden Monat — because man den Monat wählen können soll.
R2 (must): Die Übersicht zeigt alle Monate mit einem Plan der Person und die drei kommenden Monate, den neuesten zuerst — because man zurückblicken und vorausplanen will.
R3 (must): Der laufende Monat ist hervorgehoben — because man ihn sofort findet.
R4 (must): Jeder Monat zeigt seinen Namen und, wenn ein Plan besteht, den Betrag Frei, sonst den Hinweis noch nicht angelegt — because man den Stand auf einen Blick sieht.
R5 (must): Ein Tipp auf einen Monat öffnet dessen Monatsplan unter /plan/{Jahr}/{Monat}; ein Monat ohne Plan wird dabei wie heute angelegt — because der Weg in den Monat gleich bleibt.
R6 (should): Der Monatsplan hat einen Weg zurück zur Übersicht — because man sonst den Monat nicht wechseln kann.

## Constraints

Baut auf #6 (Bausteine) und #9 (Gerüst). Der Monatsplan selbst wird in #24 neu gestaltet, hier bleibt er wie er ist. Für den Haushalt gilt dieselbe Übersicht mit den Monaten des Haushaltsplans.

## Open questions

None.
