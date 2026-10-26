# Etappe 2 — Der Monatsplan entsteht aus den Verträgen

## Kontext

Etappe 1 ist fertig und im Hauptzweig: Ein Vertrag (`Commitment`) weiß, in
welchem Monat er fällig ist, an welchem Tag, und ob er noch läuft. 20 Tests grün.

Jetzt kommt das Herzstück von Duofy: Aus den Verträgen einer Person entsteht der
Plan für einen Monat. Jeder fällige Vertrag wird zu einer Zeile im Plan, einem
Posten. Der Posten ist eine **Kopie**, kein Verweis: Ändert man den Vertrag später,
bleibt ein bereits angelegter Monat, wie er war (Architekturentscheidung 0002 im
Original).

Vorlage im Original: `create_plan` in `backend/app/api/v1/plans.py`, die Modelle in
`backend/app/models/plan.py`, die Tests in `backend/tests/test_plan_creation.py`.

Diese Etappe bleibt in `Duofy.Domain`, also ohne Datenbank und ohne Oberfläche.

## Arbeitsteilung (wie vereinbart)

- **Ich** schreibe die Tests und die beiden reinen Datenklassen (nur Eigenschaften,
  nichts zu lernen daran) samt ausführlichen deutschen Kommentaren.
- **Tom** schreibt die eine Methode, die den Plan erzeugt, bis die Tests grün sind.

Neu für Tom in C#, jeweils mit Python-Gegenstück erklärt: Liste (`List<T>` wie
`list`), Schleife (`foreach` wie `for x in`), statische Methode (wie
`@staticmethod`), Fehler werfen (`throw` wie `raise`).

## Was entsteht

### Von mir: `Duofy.Domain/PlanPosition.cs`

Eine Zeile im Monatsplan. Vorerst nur die Felder, die sich aus dem heutigen
Vertrag ergeben:

- `Label` (Text, aus `Commitment.Name`)
- `AmountPlanned` (`decimal`, aus `Commitment.Amount`)
- `DueDay` (`int`, aus `Commitment.EffectiveDueDay(year, month)`)
- `Type` (`CommitmentType`, aus `Commitment.Type`)

### Von mir: `Duofy.Domain/Plan.cs` (Gerüst)

Der Plan einer Person für einen Monat:

- `Year`, `Month`
- `Positions` (`List<PlanPosition>`)
- Methodenkopf `public static Plan CreateFrom(IEnumerable<Commitment> commitments, int year, int month)`
  mit einem Rumpf, der nur `throw new NotImplementedException();` enthält, damit
  das Projekt baut und die Tests rot statt gar nicht laufen.

### Von mir: `Duofy.Domain.Tests/PlanCreationTests.cs`

Die Tests sind Toms Aufgabenstellung. Ihre Namen sind die Anforderungen:

1. Ein fälliger Vertrag wird zu einem Posten (Name, Betrag, Art stimmen)
2. Ein Vertrag, der diesen Monat nicht fällig ist, fehlt im Plan
   (vierteljährlich ab Januar ist im September nicht dabei)
3. Ein Vertrag vor seiner ersten Zahlung fehlt
4. Ein Vertrag nach seiner letzten Zahlung fehlt
5. Der Zahltag wird beim Kopieren auf die Monatslänge gekürzt
   (Zahltag 31 wird im September zu 30)
6. Der Posten ist eine Kopie: Ändert man den Vertrag danach, bleibt der Posten
7. Die Posten sind nach Zahltag sortiert
8. Ohne Verträge entsteht ein leerer Plan, kein Fehler
9. Ein Monat außerhalb von 1 bis 12 wird abgelehnt
   (`ArgumentOutOfRangeException`)
10. Jahr und Monat stehen im fertigen Plan

### Von Tom: der Rumpf von `Plan.CreateFrom`

Etwa zehn Zeilen: Monat prüfen, Plan anlegen, über die Verträge laufen, mit dem
vorhandenen `IsDueIn` filtern, für jeden fälligen einen `PlanPosition` bauen und
dabei `EffectiveDueDay` nutzen, am Ende nach Zahltag sortieren.

Danach zeige ich ihm dieselbe Methode noch einmal in der Kurzform mit LINQ
(`Where`, `Select`, `OrderBy`, das Gegenstück zu Pythons List Comprehension) —
nur zum Kennenlernen, welche Fassung bleibt, entscheidet er.

## Bewusst noch nicht drin

Diese Dinge kopiert das Original ebenfalls in den Posten, sie kommen in eigenen
kleinen Schritten, sobald der Vertrag die Felder dafür hat:

- Budget (Grundbedarf, Wünsche, Sparen) und Kategorie
- Limit statt Einzelzahlung, Konto, Zahlungsart, privat, durchlaufender Posten
- Sparziel, das sein Ziel erreicht hat, wird nicht mehr eingeplant (braucht Buchungen)
- „Diesen Monat gibt es schon" (braucht die Datenbank)
- Wem der Plan gehört (braucht Benutzer)

## Ablauf

1. Neuer Arbeitszweig `feat/domain-plan-creation` vom Hauptzweig
2. Ich lege `PlanPosition.cs`, das Gerüst `Plan.cs` und die Tests an; die Tests
   sind rot
3. Tom schreibt `CreateFrom`, ich begleite und schaue drüber
4. Kurzform mit LINQ zeigen
5. Diesen Plan nach `docs/plans/` kopieren, CHANGELOG ergänzen
6. Wenn alles grün ist und Tom sagt, dass es passt: Pull Request und Übernahme in
   den Hauptzweig (stehende Freigabe)

## Prüfung

- `dotnet test Duofy.Domain.Tests`: alle Tests grün, die 20 alten und die neuen
- Jeder neue Test war vorher einmal rot
- `dotnet build Duofy.Web` baut weiterhin
