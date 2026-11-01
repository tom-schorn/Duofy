# Functional specification: Bausteine Farbblock, Linientabelle und eckige Knöpfe

Format: 2

> **Alt (Stand vor der Radzen-Umstellung, Issue 76):** Diese Spezifikation nennt die frühere Oberflächenbibliothek MudBlazor und die Tabler-Symbole. Heute gelten Radzen und Material Symbols, siehe `docs/plans/radzen-plan.md`. Fachliche Regeln und Abläufe bleiben gültig.

## Summary

Das Team bekommt drei wiederverwendbare Bausteine im neuen Look, die die Seiten später nutzen.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #6 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Die Bausteine entstehen als eigene Komponenten der gemeinsamen Seiten, aufbauend auf dem Theme und den vorhandenen MudBlazor-Bausteinen. Eine Testseite zeigt sie in beiden Modi.

## Behaviour

- R1: Der Farbblock zeigt Titel und Betrag; die Variante bestimmt die Fläche; der Text ist immer der Haupttext.
- R2: Die Linientabelle zeigt pro Zeile Bezeichnung und Betrag, mit einer Linie zwischen den Zeilen.
- R3: Knöpfe ändern bei Fokus ihre Kontur sichtbar; der gefährliche Knopf nutzt das Fehlerrot des Themes nicht als Textfarbe auf Rosa.
- R4: Die Bausteine haben eine zugängliche Beschriftung; ein Test berechnet die Kontraste in beiden Themes.
- R5: Die Testseite ist im Entwicklungsbetrieb erreichbar und sonst nicht.

## Data and interfaces

Neu: Komponenten für Farbblock, Linientabelle und Knöpfe in den gemeinsamen Seiten, eine Testseite. Keine Daten, keine Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given ein Farbblock der Variante Einnahmen mit Titel und Betrag, when er gezeichnet wird, then zeigt er beides in der Fläche der Variante und der Text ist der Haupttext.
- [ ] A2 (R2): Given eine Linientabelle mit zwei Zeilen, when sie gezeichnet wird, then stehen Bezeichnung und Betrag je Zeile, eine Linie trennt die Zeilen und der Betrag hat tabellarische Ziffern.
- [ ] A3 (R3): Given ein Knopf, when er den Fokus bekommt, then hat er eine Kontur von 3 Pixeln in Tintenfarbe.
- [ ] A4 (R4): Given alle Bausteine, when der Kontrast in Hell und Dunkel berechnet wird, then ist jedes Text-Hintergrund-Paar mindestens 4,5 zu 1 und jeder Baustein hat eine zugängliche Beschriftung.
- [ ] A5 (R5): Given der Entwicklungsbetrieb, when die Testseite aufgerufen wird, then zeigt sie alle Bausteine hell und dunkel; given der Produktionsbetrieb, then ist sie nicht erreichbar.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/BuildingBlocksTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/BuildingBlocksTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/BuildingBlocksTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/BuildingBlocksTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/BuildingBlocksTests.cs, Tests/Duofy.Web.Tests/BuildingBlocksPageTests.cs |

## Non-goals

- Die Seiten auf die neuen Bausteine umstellen (folgt mit den Aufgaben zu Plan und Startseite).
- Weitere Bausteine wie Dialoge, Navigation oder Diagramme.
- Neue Farben oder Schriften.

## Risks

MudBlazor färbt Fehler- und Warnfarben eigenwillig: im Test mit beiden Themes prüfen. Eine Testseite darf nie in der Produktion erreichbar sein: Test dazu.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
