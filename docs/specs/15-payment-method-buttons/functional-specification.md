# Functional specification: Zahlart als Symbol-Buttons

Format: 2

## Summary

Ein Baustein zeigt die fünf Zahlarten als Symbol-Buttons mit Text darunter und ersetzt im Vertragsdialog die Auswahlliste.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #15 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Baut auf den Bausteinen aus #6 und auf TablerIcon aus #7 auf. Ein neuer Baustein PaymentMethodPicker in den gemeinsamen Seiten zeigt zu den Schlüsseln aus PaymentMethods.All je einen Button mit Symbol und Text darunter, in einer Gruppe mit der Rolle radiogroup. Er bindet wie die bisherige Auswahlliste an den Schlüssel im Entwurf des Vertrags. Der Vertragsdialog (CommitmentDialog) nutzt ihn anstelle der Auswahlliste; der Wert in Entwurf, API und Datenbank ändert sich nicht.

Zuordnung der Symbole zu den Zahlarten (aus den acht des Anfangsbestands):

| Zahlart | Schlüssel | Symbol |
| --- | --- | --- |
| Lastschrift | direct_debit | credit-card |
| Dauerauftrag | standing_order | repeat |
| Überweisung | transfer | arrows-exchange |
| Abhebung | withdrawal | cash |
| Sonstiges | other | coins |

## Behaviour

- R1: Die fünf Buttons erscheinen in der Reihenfolge der Tabelle, jeder mit Symbol und dem übersetzten Namen darunter.
- R2: Ein Tipp setzt die Zahlart und markiert den Button (Rahmen und Füllung, nicht nur Farbe); ein Tipp auf den markierten Button leert die Zahlart. Ohne Wahl ist keiner markiert.
- R3: Die Gruppe hat die Rolle radiogroup und einen Namen (Zahlungsart); jeder Button hat die Rolle radio und den Zustand gewählt oder nicht. Pfeiltasten bewegen die Auswahl, Leertaste wählt, Tab verlässt die Gruppe.
- R4: Beim Bearbeiten ist der Button der gespeicherten Zahlart markiert; gespeichert wird der Schlüssel der Markierung, bei keiner Markierung keine Zahlart.
- R5: Jeder Button ist mindestens 44 Pixel hoch und breit; bei schmaler Breite laufen die Buttons in eine zweite Zeile.

## Data and interfaces

Neu: der Baustein PaymentMethodPicker und die Zuordnung Zahlart zu Symbol in den gemeinsamen Seiten. Geändert: der Vertragsdialog (nur dieses Feld). Keine neuen Daten, keine Schnittstellenänderung; der Schlüssel in der Anfrage bleibt der aus PaymentMethodKeys.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given der Vertragsdialog, when die Zahlart gezeichnet wird, then gibt es fünf Buttons mit den Symbolen credit-card, repeat, arrows-exchange, cash und coins und dem Namen darunter.
- [ ] A2 (R2): Given keine gewählte Zahlart, when der Button Überweisung getippt wird, then ist genau dieser markiert und der Entwurf enthält transfer; when er erneut getippt wird, then ist keiner markiert und der Entwurf enthält keine Zahlart.
- [ ] A3 (R3): Given die Buttons, when sie für Screenreader gelesen werden, then gibt es eine Gruppe radiogroup mit Namen, und jeder Button hat die Rolle radio, seinen Namen und den Zustand gewählt oder nicht.
- [ ] A4 (R3): Given der Fokus auf der Gruppe, when die Pfeiltaste nach rechts und danach die Leertaste gedrückt wird, then wird die nächste Zahlart gewählt.
- [ ] A5 (R4): Given ein gespeicherter Vertrag mit der Zahlart standing_order, when er bearbeitet wird, then ist Dauerauftrag markiert, und Speichern ohne Änderung liefert standing_order.
- [ ] A6 (R5): Given ein Telefonfenster mit 360 Pixel Breite, when der Dialog gezeichnet wird, then ist jeder Button mindestens 44 Pixel hoch und breit und es gibt keinen waagerechten Bildlauf.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R3 | A4 | pending |
| R4 | A5 | pending |
| R5 | A6 | pending |

## Non-goals

- Umstellung des Vertragsdialogs auf drei Schritte (#17); hier wird nur das eine Feld getauscht.
- Neue Zahlarten oder Änderung der Schlüssel in Datenbank und API.
- Zahlart in weiteren Dialogen (Plan-Posten zeigt sie weiter als Text).
- Eigene Symbole außerhalb der acht.

## Risks

Das Symbol credit-card für Lastschrift ist eine Annäherung (es gibt keine Karte als Zahlart); die Beschriftung darunter beseitigt Zweifel. Der Telefon-Umbruch ist ohne echtes Gerät schwer zu sehen: ein Mensch sieht den Dialog einmal an.

## Open questions

None.

## Approval

Wartet auf die Freigabe im Kickoff.
