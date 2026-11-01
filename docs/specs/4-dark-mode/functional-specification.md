# Functional specification: Dunkelmodus als eigenes Theme

Format: 2

## Summary

Der Dunkelmodus erhält ein eigenes Farbschema mit dunklem Marineblau als Grund. Die Seiten sind darin gut lesbar.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #4 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R4.

## Components

None.

## Solution

Der dunkle Teil des gemeinsamen Themes wird auf die bestätigten Werte gesetzt. Die Blöcke sind gedämpft, der Hauptknopf ist gelb mit dunkler Schrift.

## Behaviour

- R1: Die Farbwerte des dunklen Themes entsprechen der Tabelle.
- R2: Ein Test berechnet den Kontrast für alle Paare, auf Blöcken nur mit Haupttext.
- R3: Kein Farbwert des dunklen Themes ist #000000 oder #FFFFFF.
- R4: Die Hauptseiten lassen sich im Dunkelmodus öffnen, der Hauptinhalt ist sichtbar.

## Data and interfaces

Geändert: dunkler Teil des Themes. Keine Daten, keine Schnittstellen.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given das dunkle Theme, when seine Farbwerte gelesen werden, then entsprechen sie den bestätigten Werten.
- [ ] A2 (R2): Given alle Text-Hintergrund-Paare des dunklen Themes, when der Kontrast berechnet wird, then ist jeder Wert mindestens 4,5 zu 1.
- [ ] A3 (R3): Given das dunkle Theme, when alle Farbwerte durchsucht werden, then ist keiner reines Schwarz oder reines Weiß.
- [ ] A4 (R4): Given die Hauptseiten im Dunkelmodus, when sie in einem Browser-Test geöffnet werden, then erscheint kein Fehler und der Hauptinhalt ist sichtbar.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/ThemeDarkTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/ThemeDarkTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/ThemeDarkTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/ThemeDarkTests.cs (teilweise, Komponententest statt Browser) |

## Non-goals

- Umschalter und gespeicherte Wahl (Aufgabe #5).
- Neue Bausteine (Aufgabe #6).
- Anpassung des hellen Themes.

## Risks

Seiten mit festen Farben bleiben im Dunkelmodus hell: Sichtprüfung. Fremd-Komponenten haben eigene Farben: einzeln prüfen.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
