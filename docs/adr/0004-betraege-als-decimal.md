# 0004 Beträge als Decimal

- Datum: 26.09.2026
- Status: angenommen

## Kontext
Beträge werden addiert, verglichen und auf Konten summiert. Gleitkommazahlen
liefern dabei Rundungsfehler, die sich in einem Haushaltsbuch als Cent-Differenzen
zeigen.

## Entscheidung
Beträge sind immer `Numeric` in der Datenbank und `Decimal` im Code, nie `Float`.

## Verworfene Alternativen
- **Float:** einfach, aber ungenau.

## Folgen
- Summen und Vergleiche sind exakt.
- Beträge müssen an den Schnittstellen bewusst als Decimal gelesen und
  geschrieben werden.
