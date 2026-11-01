# Functional specification: Tabler-Symbole als Baustein mit Lizenzdatei

Format: 2

## Summary

Es gibt einen Symbol-Baustein mit einem kleinen, festen Bestand von Tabler-Symbolen. Die Seiten stellen später darauf um.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #7 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Die benötigten SVG-Dateien werden aus dem Paket kopiert und mit der Anwendung ausgeliefert, zusammen mit dem Lizenztext. Ein Baustein liest die Datei zum Namen und gibt sie als Inline-SVG aus. Eine Liste hält fest, welche Symbole enthalten sind.

## Behaviour

- R1: Der Baustein gibt ein SVG aus, das die Textfarbe erbt.
- R2: Lizenztext und Doku-Eintrag sind vorhanden.
- R3: Im Auslieferungspaket liegen genau die Symbole der Liste.
- R4: Ohne Beschriftung trägt das SVG aria-hidden; mit Beschriftung eine Rolle img und einen Namen.
- R5: Alle acht Symbole des Anfangsbestands lassen sich anzeigen.

## Data and interfaces

Neu: SVG-Dateien der Symbole, Lizenzdatei, Liste der Symbole, ein Baustein. Keine Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given der Baustein mit dem Namen credit-card, when er gezeichnet wird, then enthält die Ausgabe ein SVG, dessen Strichfarbe die aktuelle Textfarbe ist.
- [ ] A2 (R2): Given das Repository, when nach dem Lizenztext von Tabler gesucht wird, then liegt er im Projekt und die Doku nennt Quelle und Version.
- [ ] A3 (R3): Given das Auslieferungspaket, when die Symboldateien gezählt werden, then sind es genau die der Liste.
- [ ] A4 (R4): Given ein Symbol mit Beschriftung und eines ohne, when sie gezeichnet werden, then trägt das erste einen Namen und die Rolle img und das zweite aria-hidden.
- [ ] A5 (R5): Given die acht Namen des Anfangsbestands, when der Baustein sie zeichnet, then entsteht für jeden ein SVG.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/TablerIconTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/TablerIconTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/TablerIconTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/TablerIconTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/TablerIconTests.cs |

## Non-goals

- Die Seiten von MudBlazor-Symbolen auf Tabler umstellen.
- Weitere Stile (filled) oder das ganze Paket.
- Ein Symbol je Konto zuweisen oder auswählen (Aufgaben zu Konten).

## Risks

Ein Symbol fehlt in der Liste und die Seite zeigt nichts: der Baustein meldet unbekannte Namen als Fehler im Entwicklungsbetrieb. Versionen: Das Paket wird beim Aktualisieren neu geprüft.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
