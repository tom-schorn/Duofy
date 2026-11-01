# Customer requirements: Seitennavigation für den Desktop

Format: 2

## Background

Auf dem Desktop bleibt die Navigation unten wie auf dem Handy. Eine Seitenleiste nutzt den Platz besser (Parkplatz P-034, Rundgang 29.10.2026).

## Goal

Ab Desktop-Breite erscheint eine Seitenleiste mit denselben Zielen wie die untere Leiste, der aktuelle Eintrag ist erkennbar.

## Users

Alle Personen, die Plagevo im Browser auf großen Bildschirmen benutzen.

## Requirements

R1 (must): Ab 1280 px Breite zeigt die Web-Version eine Seitenleiste statt der unteren Leiste — because der Platz auf dem Desktop dafür da ist.
R2 (must): Die Seitenleiste zeigt das volle Menü direkt: Start, Plan, Buchungen, Konten, Verträge, Haushalt, Benutzerkonto, Admin (nur für Personen, die den Server verwalten), Lizenzen, die Rechtstexte des Servers und Abmelden; "Mehr" gibt es dort nicht. Die untere Leiste bleibt bei Start, Plan, Buchungen, Mehr — because am PC der Platz da ist und der Umweg über "Mehr" wie am Handy wirkt.

**Änderung vom 01.11.2026, Toms Entscheidung:** R2 lautete vorher "dieselben Ziele in derselben Reihenfolge wie die untere Leiste". Tom hat am PC das volle Menü verlangt.
R3 (must): Der aktuelle Eintrag ist erkennbar und für Screenreader gekennzeichnet — because man wissen muss, wo man ist.
R4 (must): Die Seitenleiste ist per Tastatur bedienbar mit sinnvoller Fokusreihenfolge — because Barrierefreiheit gefordert ist.
R5 (should): Ein eigener Menüpunkt Sparen erscheint dort, sobald er existiert (Aufgabe #29), ohne dass die Leiste umgebaut werden muss — because die Navigation wachsen soll.

## Constraints

Baut auf #9. Auf Tablet und Handy bleibt die untere Leiste. Die Android-App bleibt ohne Seitenleiste.

## Open questions

None.
