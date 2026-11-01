# Customer requirements: Ursache der Meldung Rejoining Server klären

Format: 2

## Background

Am 29.10.2026 gegen 07:40 zeigte die Web-Version bei Tom kurz „Rejoining server“. Die API antwortete währenddessen, das Portal ebenfalls. Die Meldung erscheint, wenn die Dauerverbindung der Web-Version abreißt.

## Goal

Die Ursache ist benannt oder als nicht reproduzierbar begründet, und eine Gegenmaßnahme ist beschlossen, falls nötig.

## Users

Alle Personen, die die Web-Version benutzen, vor allem bei längerer Nutzung.

## Requirements

R1 (must): Die Logs des Web-Containers und von Cloudflare zum Zeitpunkt sind gesichtet — because sonst die Ursache geraten wäre.
R2 (must): Die Ursache ist benannt (Neustart, Zeitüberschreitung der Dauerverbindung, Cloudflare oder anderes) oder als nicht reproduzierbar begründet — because Tom wissen will, ob es wiederkommt.
R3 (should): Ist eine Gegenmaßnahme nötig, liegt sie als eigenes Issue vor — because das Beheben ein eigener Auftrag ist.

## Constraints

Nur lesender Zugriff auf Logs. Keine Änderung am Server ohne Toms Zustimmung. Der Zugang zum Server läuft über das private Netz.

## Open questions

None.
