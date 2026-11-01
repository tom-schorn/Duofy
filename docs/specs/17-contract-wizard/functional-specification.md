# Functional specification: Vertrag anlegen in drei Schritten

Format: 2

## Summary

Der Vertragsdialog hat drei Schritte mit Fortschritt und Zusammenfassung und bietet nur noch klassische Arten an.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #17 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R6.

## Components

None.

## Solution

Der bestehende Dialog wird auf das Schritt-Gerüst umgestellt, mit den vorhandenen Feldern auf drei Schritte verteilt. Die Prüfregeln bleiben dieselben, nur je Schritt ausgelöst.

## Behaviour

- R1: Weiter ist ohne Name und Betrag gesperrt, der Fehler steht am Feld.
- R2: Ende vor Start wird abgelehnt, der nächste Termin steht sichtbar.
- R3: Die Zusammenfassung zeigt Name, Betrag, Rhythmus, Konten und Zahlart.
- R4: Die Artauswahl enthält nur Ausgabe und Einnahme.
- R5: Beim Bearbeiten sind alle Felder vorbelegt und ändern denselben Vertrag.
- R6: Nach dem Speichern liefert die API genau die eingegebenen Werte.

## Data and interfaces

Geändert: der Vertragsdialog der gemeinsamen Seiten und seine Texte. Keine neuen Daten, keine Schnittstellenänderung.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given Schritt 1 ohne Name, when Weiter gedrückt wird, then bleibt der Dialog in Schritt 1 und das Feld Name zeigt einen Fehler.
- [ ] A2 (R2): Given ein Ende vor dem Start in Schritt 2, when Weiter gedrückt wird, then wird es abgelehnt; given ein gültiger Rhythmus, then steht der nächste Fälligkeitstermin da.
- [ ] A3 (R3): Given alle drei Schritte ausgefüllt, when Schritt 3 erreicht ist, then zeigt die Zusammenfassung Name, Betrag, Rhythmus, Konten und Zahlart vor dem Speichern.
- [ ] A4 (R4): Given der Dialog, when die Art gewählt wird, then gibt es genau Ausgabe und Einnahme.
- [ ] A5 (R5): Given ein bestehender Vertrag, when er bearbeitet wird, then sind die Felder aller Schritte vorbelegt und Speichern ändert denselben Vertrag.
- [ ] A6 (R6): Given die Eingaben aus den drei Schritten, when gespeichert wird, then entspricht der Vertrag in der API den Eingaben, und ein bestehender Sparplan bleibt unverändert.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs, Tests/Duofy.Shared.Tests/CommitmentDraftTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs, Tests/Duofy.Shared.Tests/CommitmentDraftTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs, Tests/Duofy.Shared.Tests/CommitmentDraftTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs, Tests/Duofy.Shared.Tests/CommitmentDraftTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs, Tests/Duofy.Shared.Tests/CommitmentDraftTests.cs |
| R6 | A6 | Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs, Tests/Duofy.Shared.Tests/CommitmentDraftTests.cs |

## Non-goals

- Symbol-Buttons für Zahlart und Konten (#15, #16).
- Eigener Bereich für Sparpläne und Kredite (#29 bis #32).
- Neue Vertragsfelder (Anbieter, Kündigungsfrist und Ähnliches, V2).

## Risks

Sparplan und Kredit verschwinden aus dem Dialog, bestehen aber als Daten weiter: bis zum eigenen Bereich lassen sie sich nur ansehen.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"). Die Spezifikation setzt Beschlüsse des Tages um und wurde nicht einzeln vorgelegt; Einwände trägt der Mensch im nächsten Meeting vor.
