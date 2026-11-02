# Customer requirements: Symbole für Konten

Format: 2

## Background

Konten sehen in Listen und Auswahlfeldern alle gleich aus, nur der Name unterscheidet sie (Rundgang 29.10.2026). Tom möchte jedes Konto an einem Symbol erkennen. Als Symbole sind die Tabler-Symbole im Stil outline beschlossen (Baustein TablerIcon aus #7). Der Anfangsbestand ist cash, building-bank, credit-card, pig-money, wallet, repeat, arrows-exchange und coins.

## Goal

Jedes Konto hat ein Symbol: von Haus aus eines passend zur Kontoart, auf Wunsch ein anderes aus den acht Symbolen.

## Users

Alle Personen, die Konten anlegen und beim Buchen auswählen.

## Requirements

R1 (must): Jede Kontoart hat ein Standardsymbol: Girokonto building-bank, Sparkonto pig-money, Bargeld cash — because ein Konto auch ohne eigene Wahl sofort ein passendes Bild hat.
R2 (must): Im Kontodialog wählt die Person das Symbol des Kontos aus einem Raster aus Symbol-Buttons mit den acht Symbolen — because die Wahl auf einen Blick und mit einem Tipp gehen soll.
R3 (must): Das gewählte Symbol wird am Konto gespeichert; ohne Wahl gilt das Standardsymbol der Art, und wechselt die Art, wechselt das Symbol des Kontos ohne eigene Wahl mit — because nur eine bewusste Wahl festgehalten werden soll.
R4 (must): Bestehende Konten behalten ihr Verhalten und zeigen das Standardsymbol ihrer Art — because beim Update nichts kaputtgehen darf.
R5 (must): Das Symbol erscheint vor dem Namen in der Kontenliste und überall, wo Konten gelistet werden — because man das Konto am Bild wiedererkennen soll.
R6 (must): Beim Bearbeiten ist das gespeicherte Symbol im Raster markiert, und das Raster ist per Tastatur und Screenreader bedienbar — because alle den Dialog nutzen können sollen.

## Constraints

Baut auf #7 (Baustein TablerIcon und die acht Symbole). Die Auswahllisten in Buchung, Vertrag und Plan-Posten zeigen das Symbol erst mit #16. Neue Spalte am Konto: die Migration wird beschrieben, aber nicht angewendet; sie wartet auf Tom.

## Open questions

None.
