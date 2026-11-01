# Functional specification: Standardkonto beim Registrieren, sprachabhängig

Format: 2

## Summary

Beim Registrieren legt das System ein erstes Konto an, dessen Name zur gewählten Sprache passt.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #13 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R4.

## Components

None.

## Solution

Die Registrierung ergänzt einen Schritt, der nach dem Anlegen der Person das erste Konto erzeugt. Der Name stammt aus den vorhandenen Übersetzungen. Es gibt keine Migration für bestehende Personen.

## Behaviour

- R1: Nach der Registrierung liefert die Kontenliste der Person genau ein Konto.
- R2: Der Name wird über die Sprache nachgeschlagen (deutsch „Standardkonto“, englisch „Default account“); die Registrierung ruft den Namen vorerst immer mit Deutsch ab.
- R3: Eine bereits registrierte Person hat nach dem Update dieselben Konten wie vorher.
- R4: Eine nicht unterstützte Sprache ergibt den deutschen Namen.

## Data and interfaces

Neu: ein Konto je neu registrierter Person. Der Name kommt aus den Übersetzungen der Anwendung. Keine neue Schnittstelle.

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given eine neue Registrierung, when sie erfolgreich ist, then hat die Person genau ein Konto.
- [ ] A2 (R2): Given eine neue Registrierung, when die Kontenliste gelesen wird, then heißt das Konto „Standardkonto“.
- [ ] A3 (R2): Given die Namensauflösung wird mit der Sprache Englisch aufgerufen, when der Name gelesen wird, then lautet er „Default account“.
- [ ] A4 (R3): Given eine bereits registrierte Person mit zwei Konten, when das Update eingespielt ist, then hat sie weiterhin genau diese zwei Konten.
- [ ] A5 (R4): Given die Namensauflösung wird mit einer nicht unterstützten Sprache aufgerufen, when der Name gelesen wird, then lautet er „Standardkonto“.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Backend.Tests/DefaultAccountTests.cs |
| R2 | A2 | Tests/Duofy.Backend.Tests/DefaultAccountTests.cs |
| R2 | A3 | Tests/Duofy.Backend.Tests/Domain/DefaultAccountNameTests.cs |
| R3 | A4 | Tests/Duofy.Backend.Tests/DefaultAccountTests.cs |
| R4 | A5 | Tests/Duofy.Backend.Tests/Domain/DefaultAccountNameTests.cs |

## Threats

- Can someone pretend to be someone else? Nein, das Konto wird für die gerade registrierte Person angelegt und nicht für eine vom Aufrufer genannte.
- Can someone change data? Nein, der Name stammt aus festen Übersetzungen und nicht aus Eingaben.
- Can someone deny an action? Nein, das Anlegen wird wie jede Kontoänderung im bestehenden Protokoll geführt.
- Can someone read what they should not? Nein, das neue Konto gehört nur der Person, Freigaben bleiben aus.
- Can someone bring it to a halt? Nein, der Schritt ist ein einzelnes Anlegen innerhalb der Registrierung und lässt sich nicht wiederholt auslösen.
- Can someone gain rights they should not have? Nein, es entstehen keine Rechte außer dem eigenen Konto.

## Non-goals

- Symbol für das Konto (Aufgabe #14).
- Mehrere automatische Konten, etwa Sparkonto oder Bargeld.
- Nachträgliches Anlegen für bestehende Personen.
- Eine Sprachauswahl in der App oder bei der Registrierung und englische Texte der Oberfläche (eigenes Thema, noch nicht eingeplant).
- Ein neues Sprachfeld in der Registrierungsanfrage (Entscheidung P-040, Antwort C).

## Risks

Schlägt das Anlegen des Kontos fehl, darf die Registrierung nicht halb fertig bleiben: beides in einer Transaktion.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.

Geändert am 29.10.2026 nach Entscheidung P-040 (Antwort C): Die Registrierung nutzt vorerst immer Deutsch; Englisch wird nur über die Namensauflösung belegt. Kein neues Sprachfeld.
