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

### Drin — sieben Tabellen

Umgesetzt und in der Datenbank. Details in [../datenmodell/](../datenmodell/).

**Haushalt** — `households` · `household_members`
- Haushalt anlegen, Mitglieder mit Rolle `owner` oder `member`
- Nur der Name, sonst nichts — der Haushalt besitzt nichts
- Mehrere Haushalte pro Person erlaubt

**Verpflichtungen** — `commitments`
- Vertrag, Sparziel und Schuld in **einer** Tabelle mit `type`
- Gehören immer genau einer Person
- `rhythm` + `first_month` decken monatlich, quartalsweise, halbjährlich und
  jährlich ab — GEZ landet von selbst nur in Feb/Mai/Aug/Nov
- Einfach gehalten: keine Vertragsnummer, kein Anbieter, keine
  Kündigungsfrist

**Monatsplan** — `plans`
- Gehört **einer Person**, nicht dem Haushalt
- Ein Plan pro Person pro Monat
- Quoten 50/30/20 als **Richtwerte** und Puffer-Prozentsatz
- Status Entwurf / bestätigt

**Posten** — `plan_positions`
- Entstehen aus einer Verpflichtung oder von Hand als Einmal-Posten
- Kategorie wählen, der **Block** wird daraus abgeleitet und eingefroren
- `household_id` entscheidet: leer = privat, gesetzt = im Haushaltsplan
- Fälligkeitstag, Zahlungsart, Betrag geplant und Ist
- `manually_changed` schützt Korrekturen vor der nächsten Generierung

**Änderungsprotokoll** — `plan_position_changes`
- Beide Haushaltsmitglieder dürfen Posten ändern, auch die des anderen
- Jede Änderung mit Feld, altem Wert, neuem Wert und Urheber

### Noch zu bauen

- **Plan-Generierung** — der Knopf, der aus fälligen Verpflichtungen Posten
  erzeugt
- Schemas und Endpunkte (`permissions.py` steht, ruft aber niemand auf)
- Auswertung: Soll gegen Ist je Block, Monatsverlauf entlang der Fälligkeiten
- Frontend

### Bewusst nicht drin

| Was | Warum später |
|---|---|
| **Konten** | Der Posten weiß, wer zahlt. `Account` kommt mit dem CSV-Import |
| **Aufteilungsschlüssel** | Kein automatisches Ausrechnen von Anteilen — 50/30/20 bleibt Richtwert |
| **Ausgleich, Allocations** | Fairness-Rechnung ist eine Auswertung, kein Planungsschritt |
| Vertragsdetails | Anbieter, Laufzeit, Kündigungsfrist, Plausibilitätsprüfung |
| Sparttöpfe | `savings_goal` reicht erstmal, Töpfe mit Stand kommen später |
| Forderungen, Mahnstufen | wichtig, aber kein Blocker fürs Planen |
| CSV-Import, Abgleich | zuerst muss das Planen sitzen |
| Bankanbindung | V2, braucht PSD2-Onboarding |
| Szenarien (mit/ohne Kg) | schön, aber nicht existenziell |
| Haushaltsvereinbarung | Regeln greifen erst, wenn's was zu regeln gibt |
| Mobile App (Capacitor) | Web reicht für den Samstag am Küchentisch |

---

## Danach — Reihenfolge

### V1.1 · Vertragsdetails
Anbieter, Vertragsnummer, Laufzeit, Kündigungsfrist auf `Commitment`.
Erinnerung vor Ablauf der Kündigungsfrist. Plausibilitätsprüfung: weicht der
abgebuchte Betrag vom Vertrag ab, wird nachgefragt.

*Bringt:* Genau die Frage, die beim GEZ-Fall ein Jahr lang fehlte.

### V1.2 · Konten & Sparttöpfe
`Account` mit Typ (Giro, Tagesgeld, Depot, Bar), gehört immer einer Person.
Sparttöpfe mit Stand, gespeist aus den `savings_goal`-Posten.

*Bringt:* Blatt 3 der Excel fällt weg. Und Konten sind Voraussetzung für den
Abgleich in V1.4.

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
