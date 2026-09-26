# 0001 Kontostand wird nicht gespeichert

- Datum: 26.09.2026
- Status: angenommen

## Kontext
Ein Haushaltsbuch zeigt zu jeder Zeit einen Kontostand. Ein gespeicherter Wert
muss bei jeder Buchung, Änderung oder Löschung mitgepflegt werden und kann
dabei von den Buchungen abweichen.

## Entscheidung
Der Kontostand wird nicht gespeichert. Er ergibt sich aus `opening_balance`
plus den Buchungen des Kontos.

## Verworfene Alternativen
- **Kontostand als Spalte mitführen:** schnell zu lesen, aber jede Buchung
  schreibt doppelt, und ein Fehler bleibt unbemerkt bestehen.

## Folgen
- Es gibt nur eine Wahrheit: die Buchungen.
- Der Stand wird bei Bedarf berechnet.
- Der Monatsübertrag legt den Startstand eines Kontos im Monat fest, bewegt aber
  keinen Kontostand (#94).
