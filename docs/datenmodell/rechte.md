# Rechte

`backend/app/core/permissions.py`

Kein Rollen-Framework, sondern vier Regeln als Funktionen. Die Prüfungen
liefern `bool`, `require()` macht daraus einen HTTP-Fehler.

## Die vier Regeln

| Objekt | sehen | ändern |
|---|---|---|
| `Commitment` | nur der Eigentümer | nur der Eigentümer |
| `Plan` | nur der Eigentümer | nur der Eigentümer |
| `PlanPosition` | Planbesitzer **+** Mitglieder des gesetzten Haushalts | dieselben, mit Protokoll |
| `Household` | Mitglieder | nur Rolle `owner` |

## Verpflichtungen bleiben privat

Jasmin sieht Toms O2-Vertrag **nicht**. Sie sieht nur den **Posten**, den er
erzeugt — und auch nur, wenn Tom ihn in den gemeinsamen Haushalt gehängt hat.

Das trennt sauber:

```
meine Verträge        privat, immer
was wir zusammen planen   sichtbar, wenn household_id gesetzt
```

## Fehler-Codes statt Texten

```python
require(owns_plan(user, plan), "not_the_owner")
```

Wirft `403` mit `{"code": "not_the_owner"}`. Der Text kommt aus dem Frontend —
so steht es in den Projektregeln: *Backend liefert Fehler-Codes, das Frontend
übersetzt sie.*

## Funktionen

```python
require(allowed: bool, code: str) -> None

async is_member(session, user_id, household_id) -> bool
async is_household_owner(session, user_id, household_id) -> bool

owns_commitment(user, commitment) -> bool
owns_plan(user, plan) -> bool

async can_access_position(session, user, position, plan) -> bool
async can_assign_to_household(session, plan_owner_id, household_id) -> bool
```

## can_assign_to_household — die wichtigste Prüfung

Ein Posten darf nur in einen Haushalt, in dem sein **Besitzer** Mitglied ist.
Ohne diese Prüfung könnte man Posten in fremde Haushalte schieben und wäre
plötzlich in einer fremden Planung sichtbar.

Das ist die Stelle, an der Recht und Validierung dasselbe sind.

## Stand

Das Modul ist geschrieben, wird aber **von keinem Endpunkt aufgerufen** — es
gibt noch keine. Die Regeln greifen erst, wenn die Routen dazukommen.

## Offen

- Kein Endpunkt nutzt die Prüfungen bisher
- Keine Tests
- Rate Limiting, Brute-Force-Schutz beim Login — fastapi-users bringt das
  nicht mit
- Bei einer Finanz-App mit Insolvenzdaten steht eine ernsthafte
  DSGVO-Betrachtung an, sobald echte Daten fließen
