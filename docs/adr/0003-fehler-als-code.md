# 0003 Fehler als Code, nie als Satz

- Datum: 26.09.2026
- Status: angenommen

## Kontext
Die Oberfläche ist vorerst deutsch, soll aber von anderen übersetzt werden
können, ohne Code anzufassen. Sätze aus dem Backend ließen sich dafür nicht
übersetzen.

## Entscheidung
Das Backend liefert Codes, nie Sätze. Fehler entstehen mit
`require(bedingung, "code")` und kommen als `{"code": "..."}` an. Das Frontend
übersetzt den Code.

## Verworfene Alternativen
- **Fertige Fehlermeldungen im Backend:** einfacher, aber an eine Sprache
  gebunden.

## Folgen
- Alle sichtbaren Texte stehen im Frontend und lassen sich über den Katalog
  übersetzen (#98).
- Hinweise im Plan folgen derselben Linie: das Backend liefert `code`,
  `severity`, `position_id` und `params` (#88).
- Ein neuer Fehlercode braucht einen Text im Frontend.
