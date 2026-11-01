# Functional specification: Monatsseite mit Übersicht, Plan und Buchungen als Tabs

Format: 2

## Summary

Ein Monat öffnet eine Übersicht mit Blöcken, Soll-Ist-Balken und einem Lückendiagramm, den letzten fünf Buchungen, Anstehendem und schnellen Aktionen, daneben die Tabs Plan mit den abhakbaren Posten und Buchungen mit den zugeordneten Buchungen.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #24 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R12.

## Components

None.

## Solution

Die Monatsseite bekommt zwei Tabs. Die Übersicht setzt die Bausteine (Farbblock, Soll-Ist-Balken, Balkendiagramm) mit den vorhandenen Zahlen zusammen. Der Plan-Tab zeigt die Posten wie bisher als Linientabelle. Die Zahlen werden nicht neu gerechnet.

## Behaviour

- R1: Öffnen zeigt den Tab Übersicht; der Tab Plan ist per Tipp erreichbar, der gewählte Tab bleibt beim Blättern zwischen Monaten.
- R2: Die Blöcke zeigen geplante Einnahmen, feste Ausgaben und Frei.
- R3: Je Topf Soll (Quote des Plans), Ist und Prozent, Balken und Text.
- R4: Das Lückendiagramm zeigt je Topf die Differenz zwischen Geplantem und Tatsächlichem in Euro, positiv wie negativ ruhig dargestellt. R10: Die fünf neuesten Buchungen mit Datum und Betrag. R11: Anstehend und überfällig getrennt gekennzeichnet. R12: Schnelle Aktionen oben oder unten in der Übersicht. R9: Im Haushalt dieselben Elemente über alle gezählten Mitglieder, nur lesend.
- R5: Der Tab Plan zeigt Posten nach Topf, jede Zeile abhakbar, alle bestehenden Aktionen erreichbar.
- R6: Frei zeigt Alles verplant bei null.
- R7: Ab Tablet stehen die Blöcke nebeneinander, auf dem Handy untereinander.
- R8: Der Tab Buchungen listet die Buchungen mit diesem Planmonat, neueste zuerst, und kennzeichnet die, deren Datum in einem anderen Monat liegt.

## Data and interfaces

Geändert: die Seite des Monatsplans (Tabs, neue Übersicht). Keine neuen Daten, keine neue Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given die Adresse /plan/2026/10, when sie geöffnet wird, then zeigt die Seite zwei Tabs Übersicht und Plan und der Tab Übersicht ist aktiv.
- [ ] A2 (R2): Given ein Plan mit Einnahmen 3000.00, festen Posten 1200.00 und Frei 1800.00, when die Übersicht gezeichnet wird, then zeigen die drei Blöcke 3.000,00, 1.200,00 und 1.800,00.
- [ ] A3 (R3): Given Soll 50 Prozent und Ist 60 Prozent im Grundbedarf, when die Übersicht gezeichnet wird, then steht 60 Prozent als Text beim Balken des Grundbedarfs.
- [ ] A4 (R4): Given im Grundbedarf geplant 1000.00 und tatsächlich 1200.00, when die Übersicht gezeichnet wird, then zeigt das Lückendiagramm für Grundbedarf eine Lücke von 200,00 ohne Alarmfarbe.
- [ ] A10 (R10): Given sieben Buchungen im Monat, when die Übersicht gezeichnet wird, then stehen die fünf neuesten in einer Liste mit Datum und Betrag.
- [ ] A11 (R11): Given eine Zahlung die in zwei Tagen fällig ist und eine seit drei Tagen überfällige, when die Übersicht gezeichnet wird, then stehen beide in der Liste und die überfällige ist als überfällig gekennzeichnet.
- [ ] A12 (R12): Given die Übersicht, when sie gezeichnet wird, then gibt es die Aktionen neue Buchung, neuer Posten und Posten abhaken.
- [ ] A13 (R9): Given ein Haushalt mit zwei Mitgliedern, when die Übersicht des Haushalts gezeichnet wird, then zeigt sie Blöcke, Soll-Ist, Lücke, letzte fünf Buchungen und Anstehendes über beide und keine Aktion, die fremde Daten ändert.
- [ ] A5 (R5): Given Posten in zwei Töpfen, when der Tab Plan geöffnet wird, then stehen sie nach Topf gruppiert in einer Linientabelle und lassen sich abhaken.
- [ ] A6 (R6): Given ein Plan ohne freien Rest, when die Übersicht gezeichnet wird, then steht im Block Frei Alles verplant.
- [ ] A7 (R5): Given die bestehenden Seitentests des Monatsplans, when sie laufen, then bleiben sie grün (angepasst, nicht gelöscht).
- [ ] A9 (R8): Given eine Buchung vom 20. November, die dem Dezember zugeordnet ist, when der Tab Buchungen des Dezembers geöffnet wird, then steht sie dort mit dem Hinweis, dass ihr Datum im November liegt, und im Tab Buchungen des Novembers steht sie nicht.
- [ ] A8 (R7): Given die Stylesheet-Regeln, when sie gelesen werden, then stehen die Blöcke ab 600 px nebeneinander und darunter untereinander.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R10 | A10 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R11 | A11 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R12 | A12 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R9 | A13 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R6 | A6 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R5 | A7 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R8 | A9 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |
| R7 | A8 | Tests/Duofy.Shared.Tests/MonthPageTests.cs, Tests/Duofy.Shared.Tests/MonthViewTests.cs, Tests/Duofy.Backend.Tests/PlanTests.cs, Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs |

## Non-goals

- Weitere Diagramme und Auswertungen über das Lückendiagramm hinaus (folgen mit #26).
- Gemeinsame und eigene Sicht nebeneinander (#28).
- Neue Funktionen im Plan.

## Risks

Die Umstellung berührt viele bestehende Tests der Seite: die Tests werden angepasst, nicht gelöscht. Die Seite entsteht nach #23 und #12; vorher darf sie nicht gestartet werden.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"); geändert um 12:22, 12:25 und 12:33 nach Toms Klarstellungen (Tabs Übersicht, Plan, Buchungen; Inhalte der Übersicht: Soll-Ist je Topf, Lückendiagramm, letzte fünf Buchungen, Anstehendes und Überfälliges, schnelle Aktionen; im Haushalt dasselbe). Einwände trägt der Mensch im nächsten Meeting vor.
