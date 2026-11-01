# Customer requirements: Monatsseite mit Übersicht, Plan und Buchungen als Tabs

Format: 2

## Background

Das Layout des Monatsplans gefällt Tom nicht, und es fehlen Soll-Ist-Balken und Diagramme (Rundgang 29.10.2026). Tom, 29.10. 12:22: Wer in der Plan-Auswahl einen Monat wählt, soll eine monatsbasierte Startseite bekommen und daneben einen Tab mit dem Plan und einen mit den Buchungen. Die Diagramme geben den schnellen Einblick. Buchungen gehören einem Planmonat, der nicht der Monat ihres Datums sein muss: eine Buchung vom 20. November kann dem Dezember zugeordnet sein.

## Goal

Ein Monat öffnet eine Seite mit drei Tabs: Übersicht (schneller Einblick mit Blöcken, Soll-Ist-Balken und Diagrammen), Plan (Posten zum Abhaken) und Buchungen (die diesem Monat zugeordneten Buchungen).

## Users

Alle Personen, die ihren Monat planen und kurz nachschauen wollen, mehrmals pro Woche.

## Requirements

R1 (must): Die Monatsseite unter /plan/{Jahr}/{Monat} hat drei Tabs, Übersicht, Plan und Buchungen, und öffnet mit der Übersicht — because die Übersicht der schnelle Einstieg in den Monat ist und der Plan das Arbeitswerkzeug.
R2 (must): Die Übersicht zeigt oben drei Farbblöcke mit den Beträgen Einnahmen, Festes und Frei des Monats — because der Monat so auf einen Blick lesbar ist.
R3 (must): Die Übersicht zeigt je Topf (Grundbedarf, Wünsche, Sparen) einen Soll-Ist-Balken mit Prozent als Text — because die Richtwerte sichtbar werden sollen.
R4 (must): Die Übersicht zeigt für den schnellen Einblick ein Diagramm der Lücke, also des Unterschieds zwischen Geplantem und Tatsächlichem je Topf in Euro — because man auf einen Blick sieht, wo Plan und Wirklichkeit auseinanderlaufen.
R10 (must): Die Übersicht zeigt die letzten fünf Buchungen des Monats — because man kurz prüfen will, was zuletzt gebucht wurde.
R11 (must): Die Übersicht zeigt baldig anstehende und überfällige Zahlungen getrennt gekennzeichnet — because man nichts Fälliges übersehen soll.
R12 (must): Die Übersicht bietet schnelle Aktionen: neue Buchung, neuen Posten anlegen und einen Posten abhaken — because die häufigsten Handgriffe ohne Umweg erreichbar sein sollen.
R9 (must): Im Haushalt zeigt die Übersicht dasselbe über alle gezählten Mitglieder, nur lesend, ohne Aktionen auf fremden Daten — because der Haushaltsplan nur zum Lesen ist.
R5 (must): Der Tab Plan zeigt die Posten als Linientabelle nach Topf gruppiert, weiterhin abhakbar, und alle bisherigen Funktionen bleiben: anlegen, ändern, abhaken, Übertrag, Wechsel zur Person oder zum Haushalt — because nichts verloren gehen darf.
R6 (must): Die Begriffe Frei, Anstehend und Grundbedarf gelten wie im Entscheid des alten Issues (Frei: noch keinem Posten zugeteilt, bei null Alles verplant; Anstehend: verplant, aber noch nicht abgehakt oder bezahlt) — because ein Wort für eine Zahl gelten soll.
R8 (must): Der Tab Buchungen zeigt die Buchungen, die diesem Planmonat zugeordnet sind, mit Datum, Betrag, Konto und Hinweis, wenn das Datum in einem anderen Monat liegt — because eine Buchung dem Monat gehört, dem sie zugeordnet wurde, nicht zwingend dem ihres Datums.
R7 (should): Die Seite ist auf Handy, Tablet und Desktop gut lesbar (Blöcke nebeneinander ab Tablet, untereinander auf dem Handy) — because das Layout responsiv sein soll.

## Constraints

Baut auf #6 (Bausteine), #12 (Diagramm-Baustein), #9 (Gerüst) und #23 (Adresse /plan/{Jahr}/{Monat} und Monatsliste). Die Zahlen kommen aus dem vorhandenen Plan (PlanFigures), nichts wird doppelt gerechnet. Diese Aufgabe deckt auch das Issue zu den Soll-Ist-Balken (#25) und den Kern der Monatsende-Auswertung (#26) ab; weitere Diagramme und Auswertungen folgen später.

## Open questions

None.
