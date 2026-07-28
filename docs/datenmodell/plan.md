# Plan, PlanPosition & PlanPositionChange

`plans` · `plan_positions` · `plan_position_changes` · `backend/app/models/plan.py`

Der Kern von Duofy. Hier liegt der Monatsplan, um den sich alles dreht.

---

## Plan

Der Monatsplan **einer Person**.

| Feld | Typ | |
|---|---|---|
| `id` | UUID | |
| `user_id` | FK → users | |
| `year` `month` | int | |
| `status` | enum | `draft` \| `confirmed` |
| `target_needs` | Numeric(5,2) | Default 50.00 |
| `target_wants` | Numeric(5,2) | Default 30.00 |
| `target_savings` | Numeric(5,2) | Default 20.00 |
| `buffer_percent` | Numeric(5,2) | wieviel bewusst unverplant bleibt |
| `confirmed_at` | timestamp | nullable |
| `created_at` | timestamp | |

**Unique:** `(user_id, year, month)` — ein Plan pro Person pro Monat.

### Die Quoten sind Richtwerte, keine Regel

Genau wie in der Excel: es gibt ein Soll, daneben steht das Ist, und man
schaut dass es passt. Die App rechnet **nichts** um und verteilt **nichts**
automatisch — sie zeigt die Abweichung.

Fairness-Berechnung und Prozentvorschläge sind für später vorgesehen, als
Auswertung. Nie als Teil der Generierung.

### Der Haushaltsplan ist keine Tabelle

`Plan` gehört immer einer Person. Der gemeinsame Plan entsteht als
Zusammenstellung aller Posten aller Mitglieder mit gesetzter `household_id`
für denselben Monat.

Warum keine zweite Tabelle: Der Posten existiert **einmal**. Ändert Tom den
Betrag in seinem Plan, stimmt der Haushaltsplan sofort — nichts zu
synchronisieren, nichts das auseinanderläuft. Genau der Fehler, den die zwei
Excel-Blätter hatten.

---

## PlanPosition

Ein Posten in genau einem Plan.

| Feld | Typ | |
|---|---|---|
| `id` | UUID | |
| `plan_id` | FK → plans | `CASCADE` |
| `commitment_id` | FK → commitments | **nullable**, `SET NULL` |
| `household_id` | FK → households | **nullable**, `SET NULL` |
| `label` | String(200) | |
| `amount_planned` | Numeric(12,2) | |
| `amount_actual` | Numeric(12,2) | nullable |
| `category` | enum | wofür sachlich |
| `block` | enum | 50/30/20-Zuordnung, **gespeichert** |
| `due_day` | int 1–31 | |
| `payment_method` | enum | nullable |
| `manually_changed` | bool | |
| `created_at` | timestamp | |

### Die drei Fremdschlüssel bedeuten drei verschiedene Dinge

```
plan_id        zu welchem Monatsplan gehört der Posten      Pflicht
commitment_id  aus welcher Verpflichtung entstanden er      leer bei Einmal-Posten
household_id   in welchen Haushaltsplan wandert er          leer = privat
```

### `block` wird gespeichert, nicht berechnet

Beim Anlegen aus `resolve_block(category, commitment_type)` abgeleitet und
dann festgeschrieben. Würde man ihn bei jedem Lesen neu bestimmen, schriebe
eine spätere Regeländerung rückwirkend alle alten Pläne um. Ein
abgeschlossener Monat muss zeigen, was damals entschieden wurde.

### `manually_changed`

Schützt Korrekturen. Sagt der Vertrag 34,99 € und du trägst 39,99 € ein, darf
die nächste Generierung das nicht stillschweigend zurücksetzen.

---

## PlanPositionChange

Änderungsprotokoll.

| Feld | Typ | |
|---|---|---|
| `id` | UUID | |
| `position_id` | FK → plan_positions | `CASCADE` |
| `changed_by_id` | FK → users | wer war's |
| `field` | String(50) | welches Feld |
| `old_value` | String(200) | nullable |
| `new_value` | String(200) | nullable |
| `created_at` | timestamp | wann |

### Warum Protokoll statt Sperre

Im gemeinsamen Haushalt dürfen **beide** Mitglieder Posten ändern, auch die des
anderen — am Küchentisch tippt, wer gerade die Tastatur hat. Die Alternative
wäre gewesen, fremde Posten schreibgeschützt zu machen, was den gemeinsamen
Termin umständlich macht.

Der Preis: eine Tabelle und ein Eintrag pro Änderung. Dafür ist hinterher
nachvollziehbar, wer was angefasst hat.

Werte liegen als **Text** vor. Das Protokoll muss lesbar sein, nicht rechenbar.

---

## Plan-Generierung

Noch nicht gebaut — hier der vorgesehene Ablauf:

```
Neuer Plan für 2026-09
  └── alle aktiven Commitments des Nutzers durchgehen
        └── commitment.is_due_in(9) ?
              └── PlanPosition anlegen
                    label            = commitment.name
                    amount_planned   = commitment.amount
                    category         = commitment.category
                    block            = resolve_block(category, commitment.type)
                    due_day          = commitment.due_day
                    commitment_id    = commitment.id
                    household_id     = wie im Vormonat
                    manually_changed = False
```

Posten mit `manually_changed = True` werden bei erneuter Generierung nicht
überschrieben.

## Offen

- Generierung ist nicht implementiert
- Wird `household_id` beim Generieren wirklich vom Vormonat übernommen, oder
  gehört die Zuordnung an das Commitment?
- Bei bestätigtem Plan (`status = confirmed`): Änderungen noch erlaubt?
- `amount_actual` wird bisher von nichts gefüllt — kommt mit dem CSV-Import
