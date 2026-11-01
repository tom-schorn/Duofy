# Functional specification: Umschalter Hell, Dunkel und wie das Gerät

Format: 2

## Summary

Der Knopf mit zwei Zuständen wird durch eine Auswahl mit drei Möglichkeiten ersetzt. Die Wahl wird gemerkt, „wie das Gerät“ ist die Voreinstellung.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #5 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Die gemerkte Wahl bekommt drei Werte (hell, dunkel, keine Wahl gleich Gerät). Der gemeinsame Teil der Layouts liest sie, wertet bei „keine Wahl“ die Geräteeinstellung aus und hört auf deren Änderung. Die Oberfläche zeigt die drei Möglichkeiten an der Stelle des alten Knopfes.

## Behaviour

- R1: Die Auswahl zeigt Hell, Dunkel und Gerät; die aktive Möglichkeit ist erkennbar und für Screenreader gekennzeichnet.
- R2: Nach dem Speichern liefert das Laden dieselbe Wahl.
- R3: Ohne gespeicherte Wahl ist „wie das Gerät“ aktiv.
- R4: Meldet das Gerät einen Wechsel, ändert sich die Anzeige, solange „wie das Gerät“ gilt.
- R5: Eine früher gespeicherte Wahl hell oder dunkel wird als Wahl Hell oder Dunkel gelesen.

## Data and interfaces

Geändert: der Speicher der Wahl liefert und nimmt drei Werte statt zwei; gespeicherte Werte bleiben gültig. Keine Schnittstelle am Server.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given der Umschalter, when er gezeichnet wird, then bietet er die drei Möglichkeiten Hell, Dunkel und Gerät an und genau eine ist als aktiv gekennzeichnet.
- [ ] A2 (R2): Given die Wahl Dunkel wird gespeichert, when die Wahl neu geladen wird, then ist sie Dunkel.
- [ ] A3 (R3): Given keine gespeicherte Wahl, when die Seite gezeichnet wird, then ist Gerät aktiv.
- [ ] A4 (R4): Given die Wahl Gerät und das Gerät wechselt auf dunkel, when die Änderung gemeldet wird, then ist die Anzeige dunkel, ohne dass die Seite neu geladen wird.
- [ ] A5 (R5): Given eine früher gespeicherte Wahl hell oder dunkel, when sie gelesen wird, then entspricht sie Hell beziehungsweise Dunkel.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/ThemeSwitchTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/ThemeSwitchTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/ThemeSwitchTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/ThemeSwitchTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/ThemeSwitchTests.cs |

## Non-goals

- Weitere Farbschemata oder Akzentfarben.
- Speichern der Wahl auf dem Server oder je Gerät synchronisieren.
- Änderungen am Aussehen von Hell oder Dunkel selbst.

## Risks

Die Geräteeinstellung ist erst nach dem ersten Zeichnen lesbar: kurz kann die falsche Farbe stehen. Das Verhalten steht heute schon so und wird nicht schlechter.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
