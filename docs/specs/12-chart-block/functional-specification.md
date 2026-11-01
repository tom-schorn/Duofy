# Functional specification: Diagramm-Baustein mit Farben und Leerzustand

Format: 2

## Summary

Zwei Diagramm-Bausteine (Balken, Soll-Ist) mit Leerzustand und Textalternative stehen bereit; die Seiten nutzen sie später.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #12 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Die Bausteine werden als Komponenten in den gemeinsamen Seiten gebaut, mit Flächen und Linien nach der Formensprache. Eine Testseite im Entwicklungsbetrieb zeigt sie hell und dunkel.

## Behaviour

- R1: Der längste Wert füllt die volle Breite, die anderen anteilig.
- R2: Soll-Ist zeigt Ist von Soll in Prozent, auch über 100; die Darstellung über 100 bleibt in der Fläche, ohne Rot.
- R3: Farben sind Theme-Werte, kein fester Hexwert in der Komponente.
- R4: Ohne Einträge steht der Hinweistext.
- R5: Eine Tabelle oder ein beschriebener Text ist für Screenreader vorhanden, und jeder Wert steht auch als Zahl.

## Data and interfaces

Neu: Komponenten, Testseite, Stylesheet-Abschnitt. Keine Daten, keine Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given Einträge mit Werten 100, 50 und 25, when das Balkendiagramm gezeichnet wird, then haben die Balken 100, 50 und 25 Prozent der vollen Breite.
- [ ] A2 (R2): Given Soll 200 und Ist 250, when der Soll-Ist-Balken gezeichnet wird, then steht 125 Prozent als Text da und die Darstellung nutzt keine Fehlerfarbe.
- [ ] A3 (R3): Given die Komponenten, when ihre Farben gelesen werden, then stammen sie aus Variablen des Themes.
- [ ] A4 (R4): Given keine Einträge, when das Diagramm gezeichnet wird, then erscheint der Leerzustand mit Hinweistext.
- [ ] A5 (R5): Given ein Diagramm, when es für Screenreader gelesen wird, then gibt es eine Tabelle oder Beschreibung mit allen Werten.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/ChartBlockTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/ChartBlockTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/ChartBlockTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/ChartBlockTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/ChartBlockTests.cs |

## Non-goals

- Kreis- oder Liniendiagramme.
- Daten aus der Datenbank laden (folgt in #24 und #26).
- Interaktive Auswahl oder Zoom.

## Risks

Ohne echten Browser ist die Optik nur schwer zu sehen: ein Mensch sieht die Testseite einmal an.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"). Die Spezifikation setzt Beschlüsse des Tages um und wurde nicht einzeln vorgelegt; Einwände trägt der Mensch im nächsten Meeting vor.
