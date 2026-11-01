# Functional specification: Plan-Übersicht mit Monatsauswahl

Format: 2

## Summary

Plan zeigt zuerst eine Monatsliste mit dem laufenden Monat hervorgehoben und dem Frei-Betrag je Monat; ein Tipp öffnet den Monat.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #23 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R6.

## Components

None.

## Solution

Eine neue Seite listet die Monate. Die Daten kommen aus den vorhandenen Plänen der Person; reicht der vorhandene Endpunkt nicht, wird eine lesende Liste ergänzt. Der Monatsplan bekommt seine eigene Adresse mit Jahr und Monat.

## Behaviour

- R1: /plan zeigt die Monatsliste.
- R2: Liste aus vorhandenen Plänen plus drei kommende Monate, neueste oben.
- R3: Der laufende Monat trägt eine Hervorhebung und eine Beschriftung für Screenreader.
- R4: Bei einem Plan steht der Frei-Betrag, sonst noch nicht angelegt.
- R5: Tipp öffnet /plan/{Jahr}/{Monat}.
- R6: Im Monatsplan führt ein Link zur Übersicht.

## Data and interfaces

Neu: Seite Plan-Übersicht, Adresse /plan/{Jahr}/{Monat} für den Monatsplan; gegebenenfalls ein lesender Endpunkt, der die Monate mit Plan und deren Frei-Betrag liefert. Keine neuen Daten.

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given eine angemeldete Person, when sie /plan öffnet, then sieht sie die Monatsliste und nicht den Monatsplan.
- [ ] A2 (R2): Given Pläne für September und Oktober, when die Übersicht im Oktober gezeichnet wird, then stehen vier Zeilen für Oktober bis Januar plus September, neueste zuerst.
- [ ] A3 (R3): Given der laufende Monat Oktober, when die Übersicht gezeichnet wird, then ist die Zeile Oktober hervorgehoben und als aktueller Monat beschriftet.
- [ ] A4 (R4): Given ein Plan mit Frei-Betrag und ein Monat ohne Plan, when die Übersicht gezeichnet wird, then zeigt die erste Zeile den Betrag und die zweite noch nicht angelegt.
- [ ] A5 (R5): Given die Zeile September, when sie angetippt wird, then öffnet /plan/2026/9 den Monatsplan des Septembers.
- [ ] A6 (R6): Given der Monatsplan, when er gezeichnet wird, then enthält er einen Link zu /plan.
- [ ] A7 (R2): Given eine Person aus einem anderen Haushalt, when sie die Übersicht des Haushalts abruft, then bekommt sie 404.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |
| R5 | A5 | pending |
| R6 | A6 | pending |
| R2 | A7 | pending |

## Threats

- Can someone pretend to be someone else? Nein, die Person kommt aus dem Token.
- Can someone change data? Nein, die Übersicht ist lesend; ein Monat ohne Plan wird erst beim Öffnen wie bisher angelegt.
- Can someone deny an action? Nichts Neues wird gespeichert.
- Can someone read what they should not? Nur die eigenen Monate (und beim Haushalt die der Mitglieder); Fremde bekommen 404.
- Can someone bring it to a halt? Die Zahl der Monate ist begrenzt.
- Can someone gain rights they should not have? Nein, es entstehen keine neuen Rechte.

## Non-goals

- Neugestaltung des Monatsplans (#24).
- Diagramme und Soll-Ist-Balken (#25, #26).
- Monate löschen oder umbenennen.

## Risks

Der Frei-Betrag je Monat kostet je Monat eine Berechnung: Anzahl der Monate begrenzen (alle vorhandenen plus drei) und die Berechnung wiederverwenden.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Pauschal freigegeben im Chat am 29.10.2026 gegen 12:10 ("Das was heute besprochen wurde wird spezifiziert und umgesetzt"). Die Spezifikation setzt Beschlüsse des Tages um und wurde nicht einzeln vorgelegt; Einwände trägt der Mensch im nächsten Meeting vor.
