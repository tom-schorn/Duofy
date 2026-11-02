# Customer requirements: Zahlart als Symbol-Buttons

Format: 2

## Background

Die Zahlart wird heute in einer Auswahlliste gewählt, in der man erst aufklappen muss (Rundgang 29.10.2026). Tom möchte sie als Symbol-Buttons mit Text darunter sehen und mit einem Tipp wählen. Als Symbole sind die Tabler-Symbole im Stil outline beschlossen (Baustein TablerIcon aus #7).

## Goal

Die Zahlart wird in einer Reihe aus Symbol-Buttons mit Beschriftung gewählt statt in einer Auswahlliste.

## Users

Alle Personen, die einen Vertrag anlegen oder bearbeiten.

## Requirements

R1 (must): Jede Zahlart (Lastschrift, Dauerauftrag, Überweisung, Abhebung, Sonstiges) ist ein Button mit Symbol und dem Namen darunter — because man die Möglichkeiten auf einen Blick sehen soll.
R2 (must): Ein Tipp wählt die Zahlart, und die gewählte ist deutlich markiert; ein Tipp auf die gewählte nimmt die Wahl zurück, denn die Zahlart ist optional — because man mit einem Tipp fertig sein soll und keine Angabe weiterhin erlaubt ist.
R3 (must): Die Buttons sind per Tastatur bedienbar und für Screenreader als Auswahlgruppe (radiogroup) mit Namen und Zustand lesbar — because Barrierefreiheit gefordert ist.
R4 (must): Beim Bearbeiten erscheint die gespeicherte Zahlart als gewählt, und gespeichert wird genau die gewählte — because nichts verloren gehen oder sich verändern darf.
R5 (must): Die Buttons sind auf dem Telefon groß genug zum Tippen und brechen bei schmaler Breite in eine zweite Zeile um — because der Dialog am Telefon benutzt wird.

## Constraints

Baut auf #6 (Bausteine) und #7 (Symbole). Die Schlüssel der Zahlarten und der Wert in Datenbank und API bleiben unverändert; geändert wird nur die Eingabe im Dialog.

## Open questions

None.
