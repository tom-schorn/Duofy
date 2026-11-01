# Functional specification: Alle übrigen Dialoge neu gestalten

Format: 2

## Summary

Buchung, Konto, Haushalt und Plan-Posten bekommen das gleiche Dialog-Gerüst und werden kürzer.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #22 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Jeder Dialog wird auf das Schritt-Gerüst oder die kompakte Form gebracht, ohne Funktionen zu ändern.

## Behaviour

- R1: Jeder genannte Dialog nutzt das gemeinsame Gerüst.
- R2: Jeder Dialog passt auf dem Handy auf einen Bildschirm oder hat Schritte.
- R3: Bestehende Prüfungen greifen weiter.
- R4: Fokus, Escape und Fehleranzeige wie im Gerüst.
- R5: Kontrast in beiden Themes mindestens 4,5 zu 1.

## Data and interfaces

Geändert: die vier Dialoge der gemeinsamen Seiten und ihre Texte. Keine neuen Daten, keine Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given jeder der vier Dialoge, when er gezeichnet wird, then wird das Dialog-Gerüst verwendet.
- [ ] A2 (R2): Given die Stylesheet-Regeln der Dialoge, when sie gelesen werden, then ist keiner höher als der Bildschirm ohne Schritte.
- [ ] A3 (R3): Given die bestehenden Seitentests der vier Dialoge, when sie laufen, then bleiben sie grün (angepasst, nicht gelöscht).
- [ ] A4 (R4): Given ein Dialog, when Escape gedrückt wird, then schließt er; given ein ungültiges Feld, then steht der Fehler am Feld.
- [ ] A5 (R5): Given die Farbpaare der Dialoge, when der Kontrast in beiden Themes berechnet wird, then ist jeder Wert mindestens 4,5 zu 1.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |
| R5 | A5 | pending |

## Non-goals

- Der Vertragsdialog (#17).
- Neue Felder oder Funktionen.
- Symbole für Zahlart und Konten (#15, #16).

## Risks

Viele bestehende Tests hängen an den Dialogen: anpassen statt löschen.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"). Die Spezifikation setzt Beschlüsse des Tages um und wurde nicht einzeln vorgelegt; Einwände trägt der Mensch im nächsten Meeting vor.
