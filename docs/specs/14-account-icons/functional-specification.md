# Functional specification: Symbole für Konten

Format: 2

## Summary

Das Konto bekommt ein optionales Symbol; die Kontoart liefert das Standardsymbol, der Kontodialog bietet die acht Symbole als Raster an, die Kontenliste zeigt es.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #14 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R6.

## Components

None.

## Solution

Baut auf dem Baustein TablerIcon in Shared/Duofy.Shared/Icons/ aus #7 (Zweig feat/icons-tabler) auf. Eine kleine Liste der erlaubten Symbolschlüssel steht einmal im Backend (Domain) und einmal in den gemeinsamen Seiten, in der Reihenfolge des Anfangsbestands von #7. Die Kontoart liefert über eine Zuordnung ihr Standardsymbol (checking building-bank, savings pig-money, cash cash). Das Konto bekommt ein optionales Feld Icon mit einem Schlüssel aus der Liste. Leer (null) bedeutet: Standardsymbol der Art. Eine Hilfsfunktion löst das wirksame Symbol auf (Feld, sonst Standard der Art). Im Kontodialog kommt ein Raster aus Symbol-Buttons (Auswahlgruppe radiogroup, Symbol je Button, Name für Screenreader) hinzu; wer die Art wechselt, ohne selbst ein Symbol gewählt zu haben, sieht das neue Standardsymbol. Die Kontenliste zeigt das wirksame Symbol vor dem Namen.

Schema und Migration (nur beschrieben, nicht angewendet): Die Tabelle accounts bekommt die Spalte icon, Typ Text, höchstens 30 Zeichen, nullable, ohne Vorgabewert; bestehende Zeilen bleiben null. Die Migration heißt AddAccountIcon, ist umkehrbar und wird von Tom angewendet.

## Behaviour

- R1: Ein Konto der Art Girokonto ohne gewähltes Symbol zeigt building-bank, Sparkonto pig-money, Bargeld cash.
- R2: Das Raster zeigt genau die acht Symbole als Buttons; ein Tipp wählt eines und markiert es.
- R3: Speichern schreibt den gewählten Schlüssel; wurde nichts gewählt, bleibt das Feld leer. Die API lehnt einen Schlüssel außerhalb der Liste mit einem Fehlercode ab. Wechselt die Art ohne eigene Wahl, gilt das Standardsymbol der neuen Art.
- R4: Konten mit leerem Feld verhalten sich wie vorher und zeigen das Standardsymbol ihrer Art; die Antwort der API enthält das Feld als null.
- R5: Die Kontenliste zeigt Symbol und Name; geschlossene Konten ebenso.
- R6: Beim Bearbeiten ist das wirksame Symbol markiert; Pfeiltasten wechseln die Auswahl, jeder Button hat einen Namen (zum Beispiel "Sparschwein").

## Data and interfaces

Neu: Feld Icon (optional, Text) am Konto in Domain, Datenbank, API-Anfrage und -Antwort; eine Migration (beschrieben, nicht angewendet); eine Zuordnung Art zu Standardsymbol und die Liste der erlaubten Symbole; ein Fehlercode für ein unbekanntes Symbol; die Symbolwahl im Kontodialog und das Symbol in der Kontenliste.

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given je ein Konto der Arten Girokonto, Sparkonto und Bargeld ohne gewähltes Symbol, when das wirksame Symbol aufgelöst wird, then lautet es building-bank, pig-money und cash.
- [ ] A2 (R2): Given der Kontodialog, when das Raster gezeichnet wird, then enthält es genau die acht Symbole cash, building-bank, credit-card, pig-money, wallet, repeat, arrows-exchange und coins als Buttons.
- [ ] A3 (R3): Given ein neues Girokonto, when das Symbol coins gewählt und gespeichert wird, then liefert die API für das Konto den Schlüssel coins; given kein gewähltes Symbol, then liefert sie null; given ein Schlüssel außerhalb der Liste, then lehnt die API mit einem Fehlercode ab.
- [ ] A4 (R3): Given ein neues Konto ohne eigene Symbolwahl, when die Art von Girokonto auf Sparkonto wechselt, then zeigt der Dialog pig-money.
- [ ] A5 (R4): Given ein bestehendes Konto mit leerem Symbolfeld, when die Kontenliste gelesen wird, then zeigt es das Standardsymbol seiner Art, und Kontostand und Verhalten sind unverändert.
- [ ] A6 (R5): Given zwei Konten mit unterschiedlichen Symbolen, when die Kontenliste gezeichnet wird, then steht vor jedem Namen das jeweilige Symbol.
- [ ] A7 (R6): Given ein gespeichertes Symbol wallet, when das Konto bearbeitet wird, then ist wallet im Raster markiert, die Auswahlgruppe hat die Rolle radiogroup und jeder Button einen zugänglichen Namen.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R3 | A4 | pending |
| R4 | A5 | pending |
| R5 | A6 | pending |
| R6 | A7 | pending |

## Threats

- Can someone pretend to be someone else? Nein, das Symbol wird wie das übrige Konto nur für die angemeldete Person geschrieben; die Besitzerprüfung der Konten-API bleibt.
- Can someone change data? Nur die Besitzerin oder der Besitzer ändert das Konto, und die API nimmt nur Schlüssel aus der festen Liste an, keinen freien Text.
- Can someone deny an action? Nein, die Änderung läuft wie jede Kontoänderung über die API und das bestehende Protokoll.
- Can someone read what they should not? Nein, das Symbol kommt mit dem Konto und folgt denselben Sichtrechten und Freigaben wie der Kontoname.
- Can someone bring it to a halt? Nein, das Feld ist höchstens 30 Zeichen lang und hat eine feste Liste; es entsteht keine teure Abfrage.
- Can someone gain rights they should not have? Nein, das Feld vergibt keine Rechte und ändert keine Freigaben.

## Non-goals

- Eigene Bilder oder Symbole außerhalb der acht (Tom hat den Anfangsbestand festgelegt).
- Symbole in den Auswahlfeldern von Buchung, Vertrag und Plan-Posten (#16).
- Umstellung der Dialoge selbst (gehört zu #17, #21, #22).
- Die Migration anwenden (wartet auf Tom).

## Risks

Die Liste der erlaubten Symbole steht an zwei Orten (Backend und gemeinsame Seiten) und kann auseinanderlaufen; ein Test vergleicht beide. Die Migration ändert die Datenbank erst, wenn Tom sie anwendet; bis dahin darf der Code mit dem neuen Feld nicht ausgeliefert werden.

## Open questions

None.

## Approval

Wartet auf die Freigabe im Kickoff.
