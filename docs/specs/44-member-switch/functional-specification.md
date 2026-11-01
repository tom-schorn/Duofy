# Functional specification: Personen- und Haushaltsumschalter immer sichtbar

Format: 2

## Summary

Der Umschalter und der Knopf Haushalt sind sofort sichtbar. Wer allein ist, sieht, was ihm fehlt, und kommt mit einem Tipp zum Haushalt.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #44 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R6.

## Components

None.

## Solution

Die bestehende Auswahl der Sicht wird immer gezeichnet. Der Knopf Haushalt kommt als eigenes Element dazu und erklärt seinen Zustand. Ob die Auswahl Personen anbietet, bleibt eine Frage der Freigaben.

## Behaviour

- R1: Für angemeldete Personen ist der Umschalter immer vorhanden, ohne Freigaben zeigt er nur Ich.
- R2: Der Haushalt-Knopf zeigt bei einem Haushalt ab zwei Mitgliedern die Haushaltssicht, sonst einen Hinweis mit Link zur Haushalt-Seite.
- R3: Freigebende Personen stehen mit Vornamen in der Auswahl.
- R4: Die gewählte Sicht bleibt beim Navigieren und ist erkennbar beschriftet.
- R5: Der Profil-Knopf sitzt unten in der Seitenleiste und öffnet das Menü; im App-Layout steht dieselbe Komponente.
- R6: Die Auswahl ändert keine Rechte; ohne Freigabe erscheinen keine fremden Daten.

## Data and interfaces

Geändert: Layout und Seiten der gemeinsamen Seiten. Keine neuen Daten, keine neue Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given eine angemeldete Person ohne Freigaben und ohne Haushalt, when das Layout gezeichnet wird, then ist der Umschalter vorhanden und bietet nur Ich an.
- [ ] A2 (R2): Given eine Person ohne Haushalt, when der Knopf Haushalt gezeichnet wird, then ist er als nicht verfügbar gekennzeichnet, nennt den Grund und verlinkt auf die Haushalt-Seite.
- [ ] A3 (R2): Given ein Haushalt mit zwei Mitgliedern, when der Knopf Haushalt angetippt wird, then zeigt die Plan-Seite den Haushaltsplan.
- [ ] A4 (R3): Given Anna hat Ben eine Freigabe erteilt, when Ben den Umschalter öffnet, then steht Anna mit Vornamen darin.
- [ ] A5 (R4): Given die Sicht Anna und ein Wechsel auf die Seite Buchungen, when die Seite gezeichnet wird, then ist die Sicht Anna weiter gewählt und beschriftet.
- [ ] A6 (R5): Given eine Desktop-Breite, when die Seitenleiste gezeichnet wird, then steht unten ein Profil-Knopf, der ein Menü mit Ich, Personen mit Freigabe, Haushalt, Konto und Abmelden öffnet, und dieselbe Komponente wird im App-Layout verwendet.
- [ ] A7 (R6): Given Ben hat von Anna nichts freigegeben bekommen, when er die Sicht Anna wählt, then erscheinen keine Daten von Anna.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R2 | A3 | pending |
| R3 | A4 | pending |
| R4 | A5 | pending |
| R5 | A6 | pending |
| R6 | A7 | pending |

## Non-goals

- Neue Rechte oder Freigabearten.
- Der Haushalt selbst (Gründen, Einladen), das bleibt wie es ist.
- Der Plan der Person oder des Haushalts inhaltlich.

## Risks

Der Umschalter in der Kopfleiste kann mit dem neuen Layout (#9, #10) kollidieren: erst nach deren Abschluss bauen.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10; entstanden nach Toms Hinweis um 12:34, geändert um 12:38 nach seinem Vorschlag, den Umschalter als Profil-Knopf unten in der Seitenleiste mit Menü (Ich, Personen, Haushalt, Konto, Abmelden) für Web und App wiederzuverwenden. Einwände trägt der Mensch im nächsten Meeting vor.
