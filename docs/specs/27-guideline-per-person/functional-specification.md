# Functional specification: Richtwert-Prüfung je Person im Haushaltsplan

Format: 2

## Summary

Die Antwort des Haushaltsplans bekommt eine Liste je Mitglied mit Einnahmen, Quote und Abweichung je Topf. Der Haushalt als Ganzes ändert sich nicht.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #27 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R6.

## Components

None.

## Solution

Die vorhandene Berechnung der Haushaltszahlen wird für jedes gezählte Mitglied einzeln mit nur dessen Monat aufgerufen, und das Ergebnis zusammen mit dem Zielwert des Haushalts als Mitgliederliste ausgegeben. Die Rechenregel liegt unverändert an einer Stelle.

## Behaviour

- R1: Die Antwort enthält je gezähltem Mitglied Vorname, Einnahmen und je Topf Ziel, Ist und Abweichung.
- R2: Durchlaufende Posten und der Übertrag zählen bei einer Person nirgends mit, genau wie im Haushalt.
- R3: Die Abweichung ist Ist minus Ziel, mit einer Nachkommastelle, positiv heißt über dem Richtwert.
- R4: Die bisherigen Felder des Haushaltsplans bleiben gleich, die neue Liste kommt dazu.
- R5: Ohne Einnahmen ist jedes Ist 0 und ein Merkmal sagt, dass keine Einnahmen vorliegen.
- R6: Die Berechtigung des Aufrufs ist dieselbe wie beim Haushaltsplan.

## Data and interfaces

Geändert: die Antwort von GET /api/v1/households/{id}/plans/{year}/{month} bekommt die Liste members (userId, firstName, income, hasIncome, quotas mit pot, target, actual, deviation). Keine neue Datenhaltung, kein neuer Pfad.

External input: yes

## Acceptance criteria

- [ ] A1 (R1, R2, R4): Given Anna mit Einnahmen 3000.00 und gebuchten Grundbedarf-Ausgaben 1800.00 und Ben mit Einnahmen 2000.00 und 700.00 Grundbedarf, Haushalts-Quoten 50/30/20, when der Haushaltsplan abgerufen wird, then ist der Haushalt bei Grundbedarf 50.0, Anna bei 60.0 und Ben bei 35.0.
- [ ] A2 (R3): Given dasselbe Beispiel, when der Haushaltsplan abgerufen wird, then ist die Abweichung bei Anna +10.0 und bei Ben -15.0 Prozentpunkte gegenüber dem Ziel 50.
- [ ] A3 (R2): Given Annas durchlaufender Posten über 200.00 und ein Übertrag aus dem Vormonat, when der Haushaltsplan abgerufen wird, then zählt keiner von beiden bei Annas Einnahmen oder Ausgaben.
- [ ] A4 (R5): Given Ben hat im Monat keine Einnahmen, when der Haushaltsplan abgerufen wird, then ist Bens Ist je Topf 0, hasIncome ist false und es entsteht kein Fehler.
- [ ] A5 (R4): Given die bestehenden Haushaltszahlen, when der Haushaltsplan abgerufen wird, then bleiben Einnahmen, Töpfe, offene Beträge und Haushaltsquoten wie vorher.
- [ ] A6 (R6): Given Clara aus einem anderen Haushalt, when sie den Haushaltsplan von Annas Haushalt abruft, then antwortet die API mit 404 household_not_found.
- [ ] A7 (R1): Given Ben ist nicht freigegeben von Anna, when Ben den Haushaltsplan abruft, then stehen trotzdem die Zahlen beider Personen in members, wie die Posten im Haushaltsplan.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A1 | pending |
| R4 | A1 | pending |
| R3 | A2 | pending |
| R2 | A3 | pending |
| R5 | A4 | pending |
| R4 | A5 | pending |
| R6 | A6 | pending |
| R1 | A7 | pending |

## Threats

- Can someone pretend to be someone else? Nein, die Zugehörigkeit zum Haushalt kommt aus dem Token, nicht aus der Anfrage.
- Can someone change data? Nein, die Antwort ist nur lesend; es entsteht nichts außer den fehlenden Monaten, die der Haushaltsplan schon heute anlegt.
- Can someone deny an action? Nein, es wird nichts Neues gespeichert.
- Can someone read what they should not? Nur Mitglieder desselben Haushalts sehen die Zahlen, alle Mitglieder sehen alle gezählten Mitglieder, wie bei den Posten (Entscheidung des Menschen); Fremde bekommen 404.
- Can someone bring it to a halt? Die Berechnung läuft je Mitglied über wenige Datensätze; Monate sind auf 2000 bis 2100 begrenzt.
- Can someone gain rights they should not have? Nein, die Berechtigung ist die des Haushaltsplans, es entstehen keine neuen Rechte.

## Non-goals

- Eine Oberfläche für die Anzeige (Aufgabe zur gemeinsamen und eigenen Sicht, #28).
- Persönliche Richtwerte je Person oder ein Umschalten des Maßstabs.
- Private Posten oder Ausblenden von Posten vor dem Haushalt (V2).
- Die Quote des Haushalts oder deren Bearbeitung durch den Gründer zu ändern.

## Risks

Bei kleinem Einkommen einer Person können große Prozentwerte entstehen, die nicht falsch, aber auffällig sind: die Anzeige in #28 muss ruhig erklären. Die Rechenregel darf nicht doppelt gepflegt werden: sie bleibt an einer Stelle.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
