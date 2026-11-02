# Functional specification: Kontoauswahl mit Symbol und Name

Format: 2

## Summary

Ein Baustein AccountPicker ersetzt die Auswahllisten für Konten in Buchung, Vertrag und Plan-Posten und zeigt Symbol und Name.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #16 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R5.

## Components

None.

## Solution

Baut auf #14 (wirksames Symbol des Kontos) und auf TablerIcon aus #7 auf. Ein neuer Baustein AccountPicker in den gemeinsamen Seiten ersetzt die einfachen Auswahllisten (NativeSelect) für gezahlt von und gezahlt zu in BookingDialog, CommitmentDialog und PlanPositionDialog. Eine native Liste kann kein Symbol zeigen; deshalb öffnet der Baustein eine Auswahlliste (Rolle listbox), deren Einträge Symbol und Name tragen. Geschlossen zeigt er das gewählte Konto mit Symbol und Name. Er erhält dieselbe Kontenliste, die die Dialoge heute schon filtern (aktive Konten, dazu das bereits gewählte), und ändert diese Regel nicht. Der Leereintrag (zum Beispiel Standardkonto oder kein Zielkonto) bleibt als erster Eintrag ohne Symbol erhalten. Lange Namen laufen im Eintrag über mehrere Zeilen oder werden nach zwei Zeilen mit Auslassung gekürzt; der volle Name steht als Beschriftung für Screenreader.

## Behaviour

- R1: Jeder Eintrag zeigt das wirksame Symbol des Kontos (siehe #14) und den Namen.
- R2: Das gewählte Konto steht mit Symbol und Namen im geschlossenen Feld; ohne Wahl steht der Leereintrag.
- R3: Die Liste enthält genau die Konten, die der Dialog heute anbietet: aktive Konten und das bereits gewählte. Nicht freigegebene Konten liefert die API nicht, sie erscheinen daher nicht.
- R4: Namen mit hundert Zeichen ohne Leerzeichen laufen nicht über den Rand der Auswahl; der Eintrag bricht um oder kürzt.
- R5: Pfeiltasten bewegen durch die Einträge, Eingabe wählt, Escape schließt; jeder Eintrag hat die Rolle option mit dem Namen des Kontos, das Symbol ist Zierde (aria-hidden), der Zustand gewählt ist gesetzt.

## Data and interfaces

Neu: der Baustein AccountPicker; geändert: die Kontofelder in den drei genannten Dialogen. Keine neuen Daten, keine Schnittstellenänderung.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given zwei Konten mit den Symbolen building-bank und pig-money, when die geöffnete Auswahl gezeichnet wird, then steht vor jedem Namen das passende Symbol.
- [ ] A2 (R2): Given ein gewähltes Konto, when die Auswahl geschlossen ist, then zeigt sie Symbol und Namen des Kontos; given keine Wahl, then zeigt sie den Leereintrag.
- [ ] A3 (R3): Given ein nicht aktives Konto, das nicht gewählt ist, when die Auswahl geöffnet wird, then fehlt es; given ein Vertrag mit einem inzwischen nicht aktiven Konto, when er bearbeitet wird, then ist es sichtbar und gewählt.
- [ ] A4 (R3): Given eine Person, der ein Konto nicht freigegeben ist, when sie die Auswahl öffnet, then erscheint dieses Konto nicht.
- [ ] A5 (R4): Given ein Konto mit einem Namen aus hundert Zeichen ohne Leerzeichen, when die Auswahl gezeichnet wird, then überläuft der Name den Rand nicht und es gibt keinen waagerechten Bildlauf.
- [ ] A6 (R5): Given die geöffnete Auswahl, when sie für Screenreader gelesen wird, then hat die Liste die Rolle listbox und jeder Eintrag die Rolle option mit dem Kontonamen und dem Zustand gewählt; given die Pfeiltasten und die Eingabetaste, then wird ein Konto gewählt.
- [ ] A7 (R1): Given die Dialoge Buchung (gezahlt von und gezahlt zu bei der Umbuchung), Vertrag und Plan-Posten, when sie geöffnet werden, then nutzen alle den Baustein AccountPicker.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R1 | A7 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R3 | A4 | pending |
| R4 | A5 | pending |
| R5 | A6 | pending |

## Non-goals

- Umstellung der Dialoge auf Schritte oder neues Aussehen (#17, #21, #22).
- Änderung der Freigaberegeln oder der Filterung der Konten.
- Der Kontofilter oben in der Buchungsliste (bleibt eine einfache Auswahl).
- Suche oder Gruppierung in der Auswahl.

## Risks

Ein eigener Auswahlbaustein ist am Telefon und mit Screenreader schwerer richtig zu bauen als die native Liste; die Prüfung per Tastatur und ein Blick am Telefon sind darum Teil der Abnahme. Der Baustein ist erst sinnvoll, wenn #14 das Symbol am Konto liefert.

## Open questions

None.

## Approval

Wartet auf die Freigabe im Kickoff.
