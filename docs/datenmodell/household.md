# Household & HouseholdMember

`households` · `household_members` · `backend/app/models/household.py`

## Household

Die Planungsebene für mehrere Personen. **Besitzt nichts** — keine Konten,
keine Verträge, keine Posten.

| Feld | Typ | |
|---|---|---|
| `id` | UUID | Primärschlüssel |
| `name` | String(100) | „Haushalt Schorn" |
| `created_at` | timestamp | |

Mehr nicht. Aufteilungsschlüssel, Selbstbehalt und Ausgleichsmodus stehen im
Konzept ([../gekritzel/07-haushalt-fairness.md](../gekritzel/07-haushalt-fairness.md)),
sind aber **nicht Teil des MVP** — 50/30/20 bleibt zunächst ein Richtwert, den
man von Hand einhält.

### Warum der Haushalt so leer ist

Er ist kein Konto und keine Rechtsperson, sondern eine Sicht. Bei Tom und
Jasmin gibt es wegen der laufenden Insolvenz real **kein gemeinsames Konto** —
der Haushalt existiert nur in Duofy. Dasselbe Modell passt aber auch für Paare
mit echtem Gemeinschaftskonto, weil dort trotzdem jemand Kontoinhaber ist.

## HouseholdMember

Verbindet Nutzer und Haushalt.

| Feld | Typ | |
|---|---|---|
| `id` | UUID | |
| `household_id` | FK → households | `ON DELETE CASCADE` |
| `user_id` | FK → users | `ON DELETE CASCADE` |
| `role` | enum | `owner` \| `member` |
| `created_at` | timestamp | |

**Unique:** `(household_id, user_id)` — niemand zweimal im selben Haushalt.
Ohne diesen Constraint entstehen beim Einladen stillschweigend
Doppelmitgliedschaften.

## Mehrere Haushalte sind erlaubt

`household_members` ist eine n-zu-m-Tabelle, also sind mehrere Haushalte der
**Normalfall des Modells**. Auf einen zu begrenzen wäre zusätzliche Arbeit.

Und die Richtung der Reue ist eindeutig:

| Später ändern | Aufwand |
|---|---|
| von „mehrere" auf „einer" | Constraint hinzufügen |
| von „einer" auf „mehrere" | Migration, Abfragen, UI umbauen |

Echte Fälle, die früher kommen als man denkt: WG **und** Partnerin
gleichzeitig, Auszug (alter Haushalt bleibt für die Historie), Trennung.

Im MVP-UI merkt man davon nichts — man ist in einem Haushalt, und das Häkchen
am Posten heißt schlicht „gemeinsam".

## Beziehung zu Posten

Ein Posten wandert über `PlanPosition.household_id` in **genau einen**
Haushaltsplan. `NULL` heißt privat.

```
plan_positions.household_id = NULL      nur im eigenen Plan sichtbar
plan_positions.household_id = <id>      erscheint im Haushaltsplan
```

`ON DELETE SET NULL`: Wird der Haushalt gelöscht, bleiben die Posten in den
persönlichen Plänen stehen. Niemand verliert seine Planung, weil ein anderer
den Haushalt auflöst.

## Regeln

- Nur die Rolle `owner` darf den Haushalt ändern, einladen und entfernen
- Ein Posten darf nur in einen Haushalt, in dem sein Besitzer Mitglied ist —
  siehe `can_assign_to_household()` in [rechte.md](rechte.md)

## Offen

- Was passiert beim Austritt eines Mitglieds mit seinen Posten im
  Haushaltsplan? Aktuell bleiben sie sichtbar
- Einladungsverfahren fehlt komplett — es gibt keine Endpunkte
- Darf der letzte `owner` den Haushalt verlassen?
