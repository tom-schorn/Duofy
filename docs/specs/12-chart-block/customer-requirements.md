# Customer requirements: Diagramm-Baustein mit Farben und Leerzustand

Format: 2

## Background

Für Auswertungen am Monatsende fehlen Diagramme (Rundgang 29.10.2026). Die Marke setzt auf ruhige Flächen und die Palette aus Salbei, Korall und Gelb. Das alte Issue zum Leerzustand der Diagramme (Archiv #29) fließt hier ein.

## Goal

Ein Diagramm-Baustein zeigt Balken und Verteilungen in den Farben des Themes, mit Textalternative und Leerzustand.

## Users

Das Team beim Bauen der Auswertungen; mittelbar alle, die ihren Monat auswerten wollen.

## Requirements

R1 (must): Ein Balkendiagramm zeigt je Eintrag Bezeichnung, Wert und einen Balken, dessen Länge dem Wert im Verhältnis zum größten entspricht — because Vergleiche auf einen Blick möglich sein sollen.
R2 (must): Ein Soll-Ist-Balken zeigt Soll und Ist mit Prozentwert als Text und als Länge, über 100 Prozent ruhig und deutlich ohne Alarmfarbe — because man Abweichungen sehen soll, ohne zu erschrecken.
R3 (must): Die Farben kommen aus der Palette des Themes in Hell und Dunkel — because alles zur Marke gehören soll.
R4 (must): Ohne Daten erscheint ein Leerzustand mit kurzem Hinweis statt eines leeren Diagramms — because ein leerer Bereich verwirrt.
R5 (must): Jedes Diagramm hat eine Textalternative als Tabelle oder Beschreibung, und Farbe allein trägt keine Information — because Barrierefreiheit gefordert ist.

## Constraints

Eigene einfache Darstellung aus HTML und CSS, keine Diagramm-Bibliothek und keine Fremdaufrufe. Baut auf Theme und Bausteinen aus #3 und #6. Testseite nur im Entwicklungsbetrieb (wie #6).

## Open questions

None.
