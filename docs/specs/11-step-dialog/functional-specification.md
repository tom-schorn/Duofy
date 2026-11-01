# Functional specification: Dialog-Gerüst mit Schritten und Fortschritt

Format: 2

## Summary

Ein wiederverwendbarer Dialog-Rahmen mit Schritten, Fortschritt und Prüfung je Schritt.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #11 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Ein Baustein nimmt eine Liste von Schritten mit Inhalt und Prüfregel. Er hält die Eingaben, zeigt den Fortschritt und die Knöpfe an fester Stelle und passt seine Größe an den Breitenbereich an.

## Behaviour

- R1: Kopf zeigt Schritt x von y; Zurück links, Weiter rechts am unteren Rand.
- R2: Bis 599 px Vollbild, darüber ein Dialog mit begrenzter Breite.
- R3: Zurück und wieder Vor behalten die Werte.
- R4: Ein ungültiges Pflichtfeld verhindert Weiter, der Fehler steht am Feld mit role alert.
- R5: Abbrechen mit Eingaben fragt vorher.

## Data and interfaces

Neu: Komponente für den Schritt-Dialog und eine Testseite im Entwicklungsbetrieb (wie die der Bausteine, #6). Keine Daten, keine Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given ein Dialog mit drei Schritten im zweiten Schritt, when er gezeichnet wird, then steht Schritt 2 von 3 im Kopf und Zurück und Weiter stehen an ihren festen Stellen.
- [ ] A2 (R2): Given das Stylesheet des Dialogs, when seine Regeln gelesen werden, then ist er bis 599 px Vollbild und darüber höchstens 640 px breit.
- [ ] A3 (R3): Given ein Wert in Schritt 1 und der Wechsel zu Schritt 2 und zurück, when Schritt 1 wieder erscheint, then steht der Wert noch da.
- [ ] A4 (R4): Given ein leeres Pflichtfeld in Schritt 1, when Weiter gedrückt wird, then bleibt der Dialog in Schritt 1 und das Feld zeigt einen Fehler mit role alert.
- [ ] A5 (R5): Given eingegebene Daten, when Abbrechen gedrückt wird, then erscheint eine Rückfrage, und ohne Daten schließt der Dialog sofort.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |
| R5 | A5 | pending |

## Non-goals

- Bestehende Dialoge umbauen.
- Den Vertragsdialog selbst (#17 bis #19).
- Neue Felder oder Arten.

## Risks

MudBlazor-Dialoge haben eigene Größenregeln: im Test beide Breitenbereiche prüfen.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"). Die Spezifikation setzt Beschlüsse des Tages um und wurde nicht einzeln vorgelegt; Einwände trägt der Mensch im nächsten Meeting vor.
