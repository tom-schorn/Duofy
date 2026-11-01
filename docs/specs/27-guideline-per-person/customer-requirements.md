# Customer requirements: Richtwert-Prüfung je Person im Haushaltsplan

Format: 2

## Background

Der Haushaltsplan rechnet die Quoten (zum Beispiel 50-30-20) nur für den Haushalt als Ganzes. Dort kann alles aufgehen, während eine Person über dem Richtwert liegt und die andere darunter. Das ist der Kern von Plagevo: die Lücke der Einzelnen sichtbar machen (Tom, 29.10.2026).

## Goal

Der Haushaltsplan liefert zusätzlich je Person die Quote pro Topf und die Abweichung vom Richtwert des Haushalts, nach derselben Rechenregel wie die Haushaltsquote.

## Users

Alle Mitglieder eines Haushalts mit mindestens zwei Personen, beim Öffnen des Haushaltsplans.

## Requirements

R1 (must): Der Haushaltsplan eines Monats nennt für jedes gezählte Mitglied die Quote je Topf (Grundbedarf, Wünsche, Sparen) — because man sieht, wer wie viel seiner eigenen Einnahmen in welchen Topf gibt.
R2 (must): Die Quote einer Person wird mit derselben Regel gerechnet wie die des Haushalts: gebuchte Ausgabeposten des Topfs ohne durchlaufende Posten geteilt durch ihre Einnahmen ohne durchlaufende Posten und ohne Übertrag — because beide Werte vergleichbar bleiben müssen.
R3 (must): Als Richtwert gilt für alle Personen der Zielwert des Haushalts, und die Abweichung ist Ist minus Ziel in Prozentpunkten — because ein gemeinsamer Maßstab die Lücke zeigt.
R4 (must): Die Zahlen des Haushalts im Ganzen bleiben unverändert — because bestehendes Verhalten und seine Tests nicht brechen dürfen.
R5 (must): Hat eine Person keine Einnahmen im Monat, ist ihre Quote 0 und die Antwort kennzeichnet das — because durch null geteilt wird nie, und die Anzeige soll das erklären können.
R6 (must): Wer die Zahlen sehen darf, bleibt wie beim Haushaltsplan: jedes Mitglied sieht alle gezählten Mitglieder, Fremde bekommen 404 — because der Mensch das für den Haushaltsplan so entschieden hat.

## Constraints

Aufbau auf dem Haushaltsplan (Spec 69). Es gibt keine eigene Oberfläche in dieser Aufgabe; die Anzeige folgt in der Aufgabe zur gemeinsamen und eigenen Sicht (#28). Beträge nur als decimal. Keine echten Daten in Tests.

## Open questions

None.
