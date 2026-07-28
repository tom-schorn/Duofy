# Commitment

`commitments` · `backend/app/models/commitment.py`

Eine wiederkehrende Verpflichtung — **Vertrag, Sparziel oder Schuld**.

## Warum eine Tabelle für drei Dinge

Alle drei sind dasselbe Muster:

> ein Betrag, der in bestimmten Monaten fällig wird und daraus einen Posten
> im Plan erzeugt

Sie unterscheiden sich nur im Typ und in ein, zwei Zusatzfeldern. Drei
getrennte Tabellen hätten drei Generierungs-Routinen bedeutet, die dasselbe
tun. Im UI sind es trotzdem drei Verwaltungen — nur nach `type` gefiltert.

Wenn sie später auseinanderlaufen (Kredite mit Zinsrechnung, Forderungen mit
Mahnstufen), kann man immer noch aufteilen.

## Felder

| Feld | Typ | |
|---|---|---|
| `id` | UUID | |
| `owner_id` | FK → users | die Vertragsperson |
| `type` | enum | `contract` \| `savings_goal` \| `debt` |
| `name` | String(200) | „O2", „Urlaub", „Beitragsservice" |
| `amount` | Numeric(12,2) | der wiederkehrende Betrag |
| `category` | enum | siehe [enums.md](enums.md) |
| `rhythm` | enum | `monthly` \| `quarterly` \| `biannual` \| `annual` |
| `first_month` | int 1–12 | ab wann der Rhythmus zählt |
| `due_day` | int 1–31 | Tag im Monat |
| `active` | bool | |
| `target_amount` | Numeric(12,2) | nur `savings_goal` |
| `target_date` | date | nur `savings_goal` |
| `remaining_debt` | Numeric(12,2) | nur `debt` |
| `created_at` | timestamp | |

## Verträge gehören einer Person, nicht dem Haushalt

Jeder Vertrag hat genau **eine** Vertragsperson. Auch der WG-Internetvertrag
läuft auf den, der unterschrieben hat — juristisch haftet der Vertragspartner,
nicht die WG.

Ob die Kosten geteilt werden, entscheidet nicht das Eigentum am Vertrag,
sondern `household_id` am erzeugten **Posten**.

## Rhythmus und Fälligkeit

`rhythm` + `first_month` decken die ganze Jahresübersicht ab, ohne Array-Spalte
und ohne Sonderlogik:

| Beispiel | rhythm | first_month | ergibt |
|---|---|---|---|
| Miete | `monthly` | — | alle 12 Monate |
| GEZ | `quarterly` | 2 | Feb, Mai, Aug, Nov |
| AVD Automobilclub | `annual` | 9 | September |
| Kreditkartengebühr | `annual` | 12 | Dezember |

Die Prüfung steckt in `Commitment.is_due_in(month)`:

```python
if self.rhythm is Rhythm.MONTHLY:
    return True
start = self.first_month or 1
return (month - start) % self.rhythm.interval == 0
```

## Check-Constraints

Die Typregeln liegen in der **Datenbank**, nicht nur in Python — damit sie auch
bei CSV-Import oder direktem SQL greifen.

| Constraint | Regel |
|---|---|
| `ck_commitment_due_day` | `due_day` zwischen 1 und 31 |
| `ck_commitment_first_month` | `first_month` zwischen 1 und 12 |
| `ck_commitment_target_only_for_savings_goal` | `target_amount`/`target_date` nur bei `savings_goal` |
| `ck_commitment_remaining_debt_only_for_debt` | `remaining_debt` nur bei `debt` |
| `ck_commitment_first_month_required` | `first_month` Pflicht bei allem außer `monthly` |

Der letzte ist wichtig: Ohne `first_month` wüsste die Generierung bei
„quartalsweise" nicht, ab welchem Monat gezählt wird.

## Pflichtfelder je Typ

| Feld | contract | savings_goal | debt |
|---|---|---|---|
| `name` `amount` `category` `rhythm` `due_day` | ✓ | ✓ | ✓ |
| `target_amount` `target_date` | muss leer | optional | muss leer |
| `remaining_debt` | muss leer | muss leer | optional |

`target_amount` ist optional, weil „Notgroschen, 100 € im Monat, kein Ziel" ein
legitimer Fall ist. `remaining_debt` ist optional, weil man die Restschuld beim
Anlegen nicht immer kennt — sie soll den Eintrag nicht blockieren.

## Der Typ bestimmt den Block

Wichtig und leicht zu übersehen:

```
Urlaub sparen    savings_goal + vacation  →  SAVINGS
Urlaub buchen    kein Commitment          →  WANTS
```

Gleiche Kategorie, anderer Block. Siehe `resolve_block()` in
[enums.md](enums.md).

## Sichtbarkeit

Verpflichtungen sind **privat**. Jasmin sieht Toms O2-Vertrag nie — nur den
Posten, den er erzeugt, und auch nur wenn Tom ihn in den Haushalt gehängt hat.
Das trennt „meine Verträge" von „was wir zusammen planen".

## Offen

- Anbieter, Vertragsnummer, Laufzeit, Kündigungsfrist — bewusst nicht im MVP
- Plausibilitätsprüfung: abgebuchter Betrag weicht vom Vertrag ab → nachfragen
- Bei `debt`: Restschuld sinkt nicht automatisch mit den Tilgungsposten
