# Functional specification: Seitennavigation für den Desktop

Format: 2

## Summary

Auf großen Bildschirmen steht links eine Navigation, auf kleinen bleibt die untere Leiste.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #10 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Beide Leisten lesen ihre Ziele aus `AppDestinations`: die untere die vier aus `All`, die Seitenleiste die festen aus `Side`. Was vom Server abhängt (Admin, Rechtstexte) und das Abmelden steht in `SideAccountEntries`. Ab Desktop wird die Seitenleiste gezeigt und die untere ausgeblendet; darunter umgekehrt.

**Änderung vom 01.11.2026, Toms Entscheidung:** Die Seitenleiste zeigt das volle Menü statt der vier Ziele der unteren Leiste. Das ändert R2, R4, R5, A2, A4 und A5 sowie das Risiko unten; am Handy und Tablet ändert sich nichts.

## Behaviour

- R1: Ab 1280 px sieht man die Seitenleiste, darunter die untere Leiste.
- R2: Die Seitenleiste zeigt das volle Menü direkt und kein "Mehr"; die untere Leiste behält Start, Plan, Buchungen, Mehr. Admin sieht nur, wer den Server verwaltet; die Rechtstexte sind die, die der Server anbietet. In der App steht dort auch der Schalter für hell und dunkel.
- R3: Der aktive Eintrag trägt eine Markierung und aria-current.
- R4: Tab durchläuft die Einträge in der Reihenfolge des Menüs. Abmelden ist ein Knopf, alle anderen Einträge sind Links.
- R5: Ein neues Ziel in der jeweiligen Liste erscheint in der Leiste, die sie liest; die Leisten selbst nennen keine festen Ziele.

## Data and interfaces

Geändert: Layout und Stylesheet der gemeinsamen Seiten. Keine Daten, keine Schnittstelle.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given eine Breite ab 1280 px, when das Layout gezeichnet wird, then ist die Seitenleiste vorhanden und die untere Leiste nicht sichtbar.
- [ ] A2 (R2): Given eine angemeldete Person, when beide Leisten gezeichnet werden, then zeigt die Seitenleiste das volle Menü ohne "Mehr" und die untere Leiste Start, Plan, Buchungen, Mehr; Admin steht nur bei Personen, die den Server verwalten.
- [ ] A3 (R3): Given die Seite Plan ist offen, when die Seitenleiste gezeichnet wird, then trägt der Eintrag Plan aria-current und eine sichtbare Markierung.
- [ ] A4 (R4): Given die Seitenleiste, when sie gezeichnet wird, then sind alle Einträge außer Abmelden Links mit Fokuskontur in der Reihenfolge des Menüs.
- [ ] A5 (R5): Given ein zusätzliches Ziel in der Liste einer Leiste, when sie gezeichnet wird, then erscheint es dort.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/SideNavigationTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/SideNavigationTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/SideNavigationTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/SideNavigationTests.cs |
| R5 | A5 | Tests/Duofy.Shared.Tests/SideNavigationTests.cs |

## Non-goals

- Neue Ziele anlegen (Sparen kommt mit #29).
- Umbau der Seiten selbst.
- Navigation der Android-App ändern.

## Risks

Zwei Navigationen können auseinanderlaufen: Seit dem 01.11.2026 sind es zwei Listen in `AppDestinations`. Eine neue Seite gehört in beide Wege, in `Side` und auf die Seite "Mehr".

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"). Die Spezifikation setzt Beschlüsse des Tages um und wurde nicht einzeln vorgelegt; Einwände trägt der Mensch im nächsten Meeting vor.
