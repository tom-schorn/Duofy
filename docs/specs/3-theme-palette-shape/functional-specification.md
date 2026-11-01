# Functional specification: Theme mit Palette, Formensprache und Linien

Format: 2

## Summary

Das Theme der App erhält die neue Palette, eckige Formen und Linien. Die Oberfläche sieht nach der Umstellung wie die neue Marke aus.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #3 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Das gemeinsame Theme wird auf die bestätigten Werte umgestellt, ebenso die Standard-Rundung. Seiten, die Karten mit Schatten benutzen, erhalten Linien statt Schatten. Die Typografie-Skala wird im Theme hinterlegt.

## Behaviour

- R1: Primär-, Erfolgs-, Warn- und Flächenfarben entsprechen der Palette.
- R2: Ein Test rechnet für alle Text-Hintergrund-Paare des Themes den Kontrast und schlägt unter 4,5 zu 1 fehl.
- R3: Die Standard-Rundung ist klein oder null; es gibt keinen Schatten an Karten.
- R4: Die Skala nennt Größe, Gewicht und Zeilenhöhe je Rolle.
- R5: Die Hauptseiten (Start, Plan, Buchungen, Konten, Verträge, Mehr, Anmeldung) lassen sich öffnen und bedienen.

## Data and interfaces

Geändert: das Theme der gemeinsamen Seiten (Palette, Rundung, Typografie). Keine Daten, keine Schnittstellen.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given das helle Theme, when seine Farbwerte gelesen werden, then entsprechen Primär-, Einnahmen-, Fest-, Frei- und Grundfarbe den bestätigten Palettenwerten.
- [ ] A2 (R2): Given alle Text-Hintergrund-Paare des hellen Themes, when der Kontrast berechnet wird, then ist jeder Wert mindestens 4,5 zu 1.
- [ ] A3 (R3): Given das Theme, when seine Standard-Rundung gelesen wird, then ist sie höchstens 4 px.
- [ ] A4 (R4): Given das Theme, when seine Typografie gelesen wird, then gibt es Einträge für Titel, Abschnitt, Betrag, Text und Zusatz.
- [ ] A5 (R5): Given die Hauptseiten, when sie in einem Browser-Test geöffnet werden, then erscheint kein Fehler und der Hauptinhalt ist sichtbar.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/ThemePaletteTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/ThemePaletteTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/ThemePaletteTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/ThemePaletteTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/PlanPageTests.cs, Tests/Duofy.Shared.Tests/BookingsPageTests.cs, Tests/Duofy.Shared.Tests/MainLayoutTests.cs (Komponententests, kein echter Browser) |

## Non-goals

- Dunkelmodus (Aufgabe #4).
- Neue Bausteine wie Farbblock und Linientabelle (Aufgabe #6).
- Neues Layout und Navigation (responsives Gerüst).
- Symbole und Logo.

## Risks

Seiten mit eigenen festen Farben werden nicht überall durch das Theme erfasst: Sichtprüfung der Hauptseiten. Kontrast kann an einer Stelle unter 4,5 fallen: der Test macht es sichtbar.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
