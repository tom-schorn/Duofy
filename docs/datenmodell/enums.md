# Enums & Block-Ableitung

`backend/app/models/enums.py`

## Category — wofür das Geld sachlich ist

Die **Liste** ist systemweit vorgegeben, nicht pro Haushalt erweiterbar —
sonst legt jeder eigene Kategorien an und keine Auswertung ist vergleichbar.

Die **Zuordnung zu einem Block** ist es ausdrücklich nicht. Der Nutzer wählt.

```
income · housing · insurance · groceries · health · mobility ·
communication · children · subscriptions · leisure · vacation ·
pocket_money · reserves · debt_repayment · investment · legal
```

`legal` deckt Rechts- und Verfahrenskosten ab — Treuhänder-Vergütung,
Gerichtskosten, Anwalt.

## Block — die 50/30/20-Zuordnung

```
income · needs · wants · savings
```

**Investition ist kein eigener Block.** Bewusste Anschaffungen fließen in
`wants`. Die getrennte Darstellung im Investitionsplan läuft über
`Category.INVESTMENT` — eine eigene Liste, aber rechnerisch Teil der Wünsche.

Genau so ist es in der Excel gelöst: ein eigenes Blatt, damit es übersichtlich
bleibt, dessen Summe aber in die 30 % einfließt.

> **Warum das wichtig ist:** `Plan` kennt nur drei Ziele — `target_needs`,
> `target_wants`, `target_savings`. Ein vierter Block hätte auf keine Quote
> eingezahlt und wäre in jeder Auswertung unsichtbar gewesen.

## Wer bestimmt den Block

```python
def resolve_block(chosen: Block, commitment_type: CommitmentType | None = None) -> Block:
    if commitment_type in (CommitmentType.SAVINGS_GOAL, CommitmentType.DEBT):
        return Block.SAVINGS
    return chosen
```

| Fall | Block | wählbar |
|---|---|---|
| `type = debt` | Sparen | **nein** |
| `type = savings_goal` | Sparen | **nein** |
| `type = contract` | Nutzerwahl | ja |
| Einmal-Posten | Nutzerwahl | ja |

Die beiden festen Regeln sind rechnerisch, nicht Geschmackssache: Tilgung und
Sparen sind gebundenes Geld, kein Verbrauch. Inkasso wird als `debt` angelegt
und landet damit automatisch bei Sparen, ohne Extraregel.

### Warum der Rest frei ist

Ob Sprit Bedarf oder Wunsch ist, hängt vom Haushalt ab — der eine fährt zur
Arbeit, der andere zum Vergnügen. Es gibt dazu keine allgemeingültige Wahrheit,
die man ins Modell schreiben könnte.

Ein Beispiel aus der Praxis: eine feste monatliche Mindestvergütung an einen
Insolvenztreuhänder. Seit einem Jahr unverändert, also scheinbar Fixkosten —
aber kein Bedarf und keine Schuld. Der Nutzer setzt sie auf Wunsch, und das ist
richtig so.

## BLOCK_SUGGESTION

Ein Dictionary `Category → Block`. **Nur ein Vorschlag fürs Frontend**, das
damit das Auswahlfeld vorbelegt. Keine Datenlogik — der gespeicherte Wert
kommt immer aus der Nutzerwahl bzw. `resolve_block()`.

| Kategorie | Vorschlag |
|---|---|
| `income` | Einnahme |
| `housing` `insurance` `groceries` `health` `mobility` `communication` `children` `legal` | Bedarf |
| `subscriptions` `leisure` `vacation` `pocket_money` `investment` | Wunsch |
| `reserves` `debt_repayment` | Sparen |

## Zwei Achsen, nicht eine

Der Punkt, an dem die Excel-Tabelle scheiterte:

| | Kategorie | Block |
|---|---|---|
| Was ist es? | Mobilität | — |
| Wie werten wir es? | — | Bedarf oder Wunsch |
| Ändert sich | nie | je nach Haushalt |

**Sprit ist immer Mobilität** — objektiv. Ob er als Bedarf zählt, ist eine
Entscheidung. In der Excel steckten beide Achsen in einer Spalte, deshalb
liefen Toms und Jasmins Blätter auseinander.

## Weitere Enums

**`Role`** — `owner` · `member`

**`CommitmentType`** — `contract` · `savings_goal` · `debt`

**`Rhythm`** — `monthly` · `quarterly` · `biannual` · `annual`
Mit `.interval` als Monatsabstand (1, 3, 6, 12) für die Fälligkeitsrechnung.

**`PlanStatus`** — `draft` · `confirmed`

**`PaymentMethod`** — `withdrawal` · `transfer` · `standing_order` ·
`direct_debit` · `special`
Entspricht dem Farbcode aus der Excel: grün Abhebung, orange Überweisung,
pink Dauerauftrag/Lastschrift, gelb Besonderheit.

## Speicherung — VARCHAR ohne Prüfung

Alle Enum-Spalten nutzen `SAEnum(..., native_enum=False)`. Das ergibt
**VARCHAR(20) ohne Check-Constraint** — SQLAlchemy 2.0 setzt
`create_constraint` standardmäßig auf `False`.

Konsequenzen:

- Ein neuer Enum-Wert braucht **keine Migration**
- Die Prüfung findet nur in Python statt. Direktes SQL kann jeden Text
  schreiben
- Für die Kategorienliste ist das der richtige Kompromiss, weil sie wachsen
  wird

Wer DB-seitige Prüfung will, setzt `create_constraint=True` — dann ist aber
jede Enum-Änderung eine Migration.

## Offen

- Reichen 16 Kategorien? Unterkategorien nötig?
- Sollen Nutzer eigene Kategorien anlegen dürfen? (Gegen: Auswertungen
  werden unvergleichbar)
