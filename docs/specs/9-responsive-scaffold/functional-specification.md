# Functional specification: Responsives Gerüst für Handy, Tablet und Desktop

Format: 2

## Summary

Das Layout reagiert auf drei Breitenbereiche. Auf dem Desktop ist der Inhalt zentriert und begrenzt, auf dem Handy bleibt alles wie in der App.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #9 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Die Breakpoints und Abstände werden zentral festgelegt (Theme und Stylesheet) und in beiden Layouts (Browser und App) genutzt. Der Inhaltsbereich bekommt eine maximale Breite und Zentrierung ab Desktop. Tippflächen erhalten eine Mindestgröße.

## Behaviour

- R1: Die drei Bereiche sind in einer Quelle definiert und in der Doku genannt.
- R2: Ab 1280 px ist der Inhalt höchstens 1180 px breit und mittig.
- R3: Auf dem Handy bleiben die vier Ziele der unteren Leiste, Knöpfe und Navigationseinträge haben mindestens 44 px.
- R4: Die Hauptseiten haben bei den drei Prüfbreiten keinen horizontalen Überlauf.
- R5: Die Randwerte stehen an einer Stelle.

## Data and interfaces

Geändert: Stylesheet und Layouts der gemeinsamen Seiten, Doku. Keine Daten, keine Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given die Festlegung der Breakpoints, when sie gelesen wird, then nennt sie Handy bis 599, Tablet 600 bis 1279 und Desktop ab 1280 Pixel und die Doku wiederholt sie.
- [ ] A2 (R2): Given das Stylesheet, when die Regeln für den Inhaltsbereich gelesen werden, then ist er ab 1280 px auf 1180 px begrenzt und zentriert.
- [ ] A3 (R3): Given die untere Navigation und die Knöpfe, when ihre Mindestgrößen gelesen werden, then sind es mindestens 44 px in Höhe und Breite.
- [ ] A4 (R4): Given die Hauptseiten Start, Plan, Buchungen, Konten, Verträge und Mehr, when sie in einem Seitentest ohne Überlaufregel gezeichnet werden, then enthält das Stylesheet für den Rahmen eine Regel gegen horizontalen Überlauf und keine feste Breite über 100 Prozent.
- [ ] A5 (R5): Given die Randwerte, when sie gesucht werden, then stehen sie an einer Stelle und die Layouts verwenden sie.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/ResponsiveScaffoldTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/ResponsiveScaffoldTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/ResponsiveScaffoldTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/ResponsiveScaffoldTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/ResponsiveScaffoldTests.cs |

## Non-goals

- Seitennavigation für den Desktop (#10).
- Dialoge und Formulare (#11 und folgende).
- Neue Seiten oder neuer Inhalt.
- Gestaltung einzelner Seiten.

## Risks

Ein echter Browser fehlt für die Sichtprüfung; die Prüfung läuft über Regeltexte und Komponententests, die Sichtprüfung macht ein Mensch.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"). Die Spezifikation setzt Beschlüsse des Tages um und wurde nicht einzeln vorgelegt; Einwände trägt der Mensch im nächsten Meeting vor.
