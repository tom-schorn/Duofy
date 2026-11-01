# Functional specification: Ursache der Meldung Rejoining Server klären

Format: 2

## Summary

Eine kurze Untersuchung mit schriftlichem Befund. Es wird nichts am Betrieb geändert.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #38 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R3.

## Components

None.

## Solution

Die Untersuchung wird als Vorfall-Datei festgehalten (Vorlage des Workday-Ablaufs). Sie nennt Zeitpunkt, gesichtete Quellen, Befund und Gegenmaßnahme.

## Behaviour

- R1: Die Datei nennt die gesichteten Logquellen und das Zeitfenster.
- R2: Die Datei hat einen Abschnitt Ursache, der eine Ursache nennt oder ausdrücklich „nicht reproduzierbar“ mit Begründung.
- R3: Wo eine Gegenmaßnahme nötig ist, verweist die Datei auf ein Issue.

## Data and interfaces

Neue Datei: Vorfall-Bericht unter .workday/incidents/. Keine Daten, keine Schnittstellen.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given der Vorfall-Bericht, when er gelesen wird, then nennt er die gesichteten Logquellen und das Zeitfenster.
- [ ] A2 (R2): Given der Vorfall-Bericht, when sein Abschnitt Ursache gelesen wird, then ist er nicht leer und enthält eine Ursache oder die Begründung nicht reproduzierbar.
- [ ] A3 (R3): Given der Vorfall-Bericht nennt eine Gegenmaßnahme, when das Issue gesucht wird, then verweist der Bericht auf eine Issue-Nummer.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |

## Non-goals

- Die Gegenmaßnahme selbst umsetzen.
- Änderungen am Server, am Tunnel oder an Cloudflare.
- Allgemeine Überwachung einführen.

## Risks

Die Logs reichen nicht weit genug zurück: dann steht „nicht reproduzierbar“ mit dieser Begründung im Bericht.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
