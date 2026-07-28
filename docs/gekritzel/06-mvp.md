# MVP & Roadmap

Stand: 28.07.2026

## Der Leitsatz

> **Der MVP ist erreicht, wenn Tom & Jasmin am letzten Samstag im Monat die
> Excel-Tabelle nicht mehr öffnen.**

Nicht mehr, nicht weniger. Alles, was dafür nicht nötig ist, kommt später.

## Architektur-Prinzip

Die **Monatsplanung** ist der Kern. Alle Verwaltungen sind **Quellen**, die
Positionen in die Planung einspeisen.

```
                    MONATSPLANUNG
                          ▲
        ┌─────────┬───────┴────┬──────────┬─────────────┐
    einmalige  Verträge   Sparpläne    Kredite     Forderungen
     Posten                                 └───────────┘
                                             Gläubiger
```

Jede Position trägt ihre `quelle`. Dadurch lässt sich jede Verwaltung einzeln
nachrüsten, ohne die Planung anzufassen.

**Fachliche Zuordnung:** Kredite, Tilgung und Schulden laufen im Block
**Sparen (20 %)** — es ist derselbe Vorgang: Geld, das gebunden wird statt
verbraucht.

---

## MVP — Monatsplanung

### Drin

**Haushalt**
- Haushalt anlegen, zweite Person einladen
- Konten pro Person (Giro, Tagesgeld, Depot, Bar) — reine Stammdaten

**Planungsperiode**
- Periode anlegen mit explizitem Start und Ende (nicht Kalendermonat)
- Status: Entwurf → beide bestätigt → abgeschlossen
- **Vormonat übernehmen** — kopiert alle Positionen als Startpunkt
- Puffer-Faktor: wieviel Prozent des Einkommens überhaupt verplant werden
- Quoten 50/30/20 einstellbar

**Positionen**
- Von Hand anlegen, **wiederkehrend oder einmalig**
- **Kategorie** wählen (Wohnen, Mobilität, Abos …) — der **Block**
  (Bedarf 50 % · Wunsch 30 % · Sparen 20 %) leitet sich daraus ab und wird
  beim Anlegen eingefroren
- Haushaltsregel für strittige Kategorien (z. B. Sprit) — einmal entschieden,
  gilt für beide
- Fälligkeitstag im Monat
- Zahlungsart (Abhebung, Überweisung, Dauerauftrag, Lastschrift, Besonderheit)
- Konto — von welchem Konto läuft es
- Sichtbarkeit: gemeinsam oder privat
- Betrag geplant / Betrag Ist

**Auswertung während des Planens**
- Soll gegen Ist pro Block, live
- Verfügbar · Belegt · Puffer
- Monatsverlauf entlang der Fälligkeitstage

### Bewusst nicht drin

| Was | Warum später |
|---|---|
| Vertragsverwaltung | „Vormonat übernehmen" löst den Schmerz schon |
| Sparpläne, Töpfe | Sparen geht erstmal als normale Position |
| Kredite, Gläubiger | dito |
| Forderungen, Mahnstufen | wichtig, aber kein Blocker fürs Planen |
| CSV-Import, Abgleich | zuerst muss das Planen sitzen |
| Bankanbindung | V2, braucht PSD2-Onboarding |
| Szenarien (mit/ohne Kg) | schön, aber nicht existenziell |
| Haushaltsvereinbarung | Regeln greifen erst, wenn's was zu regeln gibt |
| Mobile App (Capacitor) | Web reicht für den Samstag am Küchentisch |
| Aufteilungsschlüssel | erst wenn die Planung steht |

---

## Danach — Reihenfolge

### V1.1 · Vertragsverwaltung
Anbieter, Betrag, Rhythmus, Fälligkeit, Konto, Person, Laufzeit,
Kündigungsfrist. Erzeugt Positionen automatisch — auch quartalsweise und
jährliche. Ersetzt „Vormonat übernehmen" für alles Wiederkehrende.

*Bringt:* GEZ landet von selbst nur in Feb/Mai/Aug/Nov. Erinnerung vor
Kündigungsfrist. Plausibilitätsprüfung bei Abweichung.

### V1.2 · Sparpläne & Töpfe
Töpfe mit Zielbetrag und Zieldatum (Auto, Urlaub, Zähne). Zuordnung, welcher
gesparte Euro in welchen Topf geht. Stand je Topf.

*Bringt:* Blatt 3 der Excel fällt weg.

### V1.3 · Gläubiger, Kredite, Forderungen & Privatdarlehen
Gläubiger als Stammdaten. Kredit mit Restschuld, Rate, Laufzeit. Forderung mit
Zeitraum, Ursprungsbetrag, Mahnstufen und Teilzahlungen.

Dazu **Privatdarlehen** — geliehenes Geld unter Freunden, in beide Richtungen,
mit offenem Saldo je Person. Verleihen und Zurückbekommen laufen am 50/30/20
vorbei, nur Tilgungsraten zählen im 20-%-Block.

*Bringt:* Der GEZ-Fall wird sichtbar — laufender Beitrag und Altrückstand
getrennt, mit Datum, wann der Rückstand durch ist. Und man weiß endlich, wer
wem noch was schuldet.

### V1.4 · Abgleich & Auswertung
CSV-Import. Regel-Matching Buchung → geplante Position. Nur Reste landen beim
Nutzer als **ungeplante Position**, der eine Kategorie gegeben wird.

*Bringt:* Das „Ist" trägt sich weitgehend selbst ein. Und weil jede Position
schon eine Kategorie hat, fällt das **Tracking als Nebenprodukt** raus —
Ausgaben je Kategorie über Monate, ohne dass jemand Buchungen kategorisiert.

Damit wird Finanzblick überflüssig: Planung und Tracking laufen auf denselben
Daten statt in zwei getrennten Programmen.

### V1.5 · Haushalt & Fairness
Aufteilungsschlüssel, Kapitalaufteilung pro Person, Haushaltsvereinbarung.

### V2 · Bankanbindung & Mobile
GoCardless (PSD2) für Zahlungskonten. Capacitor-App. Depots bleiben manuell.

---

## Reality-Check

Der MVP ist ein Formular mit einer Liste, ein paar Prozentrechnungen und einer
Kopierfunktion. Das ist absichtlich klein — die eigentliche Arbeit steckt
darin, dass es sich am Küchentisch **besser anfühlt** als Excel.

Wenn das nicht der Fall ist, hilft auch keine Vertragsverwaltung.
