# Enums & Block-Ableitung

`backend/app/models/enums.py`

## Category — wofür das Geld sachlich ist

Systemweit vorgegeben, **nicht** pro Haushalt erweiterbar. Sonst legt jeder
eigene Kategorien an und keine Auswertung ist mehr vergleichbar.

| Kategorie | Standard-Block |
|---|---|
| `income` | Einnahme |
| `housing` | Bedarf 50 % |
| `insurance` | Bedarf |
| `groceries` | Bedarf |
| `health` | Bedarf |
| `mobility` | Bedarf |
| `communication` | Bedarf |
| `children` | Bedarf |
| `subscriptions` | Wunsch 30 % |
| `leisure` | Wunsch |
| `vacation` | Wunsch |
| `pocket_money` | Wunsch |
| `reserves` | Sparen 20 % |
| `debt_repayment` | Sparen |
| `investment` | Investition |

Die Zuordnung steht als `CATEGORY_BLOCK`-Dictionary im Code.

## Block — die 50/30/20-Zuordnung

`income` · `needs` · `wants` · `investment` · `savings`

Richtwert, keine Regel — siehe [plan.md](plan.md).

## Zwei Achsen, nicht eine

Das ist der Punkt, an dem die Excel-Tabelle scheiterte:

| | Kategorie | Block |
|---|---|---|
| Was ist es? | Mobilität | — |
| Wie werten wir es? | — | Bedarf oder Wunsch |
| Ändert sich | nie | je nach Entscheidung |

**Sprit ist immer Mobilität** — objektiv, unstrittig. Ob er als Bedarf oder
Wunsch zählt, ist eine Entscheidung. In der Excel steckten beide Achsen in
einer Spalte, deshalb liefen Toms und Jasmins Blätter auseinander.

## resolve_block()

```python
def resolve_block(category, commitment_type=None) -> Block:
    if commitment_type in (CommitmentType.SAVINGS_GOAL, CommitmentType.DEBT):
        return Block.SAVINGS
    return CATEGORY_BLOCK[category]
```

**Der Typ schlägt die Kategorie.** Sonst wäre „Urlaub sparen" ein Wunsch:

| Vorgang | commitment_type | category | Block |
|---|---|---|---|
| Urlaub sparen | `savings_goal` | `vacation` | **savings** |
| Urlaub buchen | — | `vacation` | **wants** |
| Kredit tilgen | `debt` | `mobility` | **savings** |
| Miete | `contract` | `housing` | **needs** |
| Netflix | `contract` | `subscriptions` | **wants** |

Ohne diese Regel müsste man Kunstkategorien wie „Urlaubsrücklage" erfinden und
hätte die Kategorien nach Verwendungszweck statt nach Sache geschnitten.

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

## Warum VARCHAR statt nativer Postgres-Enums

Alle Enum-Spalten nutzen `SAEnum(..., native_enum=False)` — also VARCHAR mit
Check-Constraint.

Bei nativen Enums wäre jede neue Kategorie ein `ALTER TYPE`, das sich in
Transaktionen sperrig verhält und aus dem sich Werte nicht wieder entfernen
lassen. Mit VARCHAR ist es eine normale Migration.

Und Kategorien **werden** wachsen — sie sind systemweit vorgegeben, also
kommen Wünsche dafür bei jedem neuen Nutzer.

## Offen

- Reichen 15 Kategorien? Unterkategorien nötig?
- Welche Kategorien dürfen ihren Block nie ändern (Miete als Wunsch ist
  Unsinn), und wer entscheidet Grenzfälle?
- Sprit: Bedarf oder Wunsch? — die Entscheidung steht noch aus
