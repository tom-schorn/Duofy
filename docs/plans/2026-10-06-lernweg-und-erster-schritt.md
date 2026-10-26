# Duofy in C# neu bauen — Lernweg und erster Schritt

## Kontext

Duofy existiert als Python/React-Anwendung auf GitHub (tom-schorn/Duofy, v0.2.0).
Tom will es in `D:\Projekte Git\Duofy` komplett in C#/.NET 10 neu bauen und dabei
C# lernen. Er ist Neuling. Vereinbart ist:

- **Lernmodus:** Ein neues Konzept zeige und erkläre ich einmal an einem Beispiel.
  Sobald Tom das Muster verstanden hat, schreibt er das nächste Stück selbst, und
  ich begleite nur noch (Aufgabe stellen, drüberschauen, erklären).
- **Android-App:** komplett meine Sache, Tom muss das nicht lernen.
- **Umfang:** alles neu in C# — Datenbank, Fachregeln, Anmeldung, Oberfläche.

Im Ordner liegt bisher nur die unveränderte Blazor-Hybrid-Vorlage (`Duofy.Shared`,
`Duofy.Web`, `Duofy` als MAUI-App), ohne Git, ohne Fachcode, ohne Tests.

Das Original dient als Fachvorlage: seine Regeln, seine Tests und seine fünf
Architekturentscheidungen (`docs/adr/`) übernehmen wir inhaltlich, den Code
schreiben wir neu.

## Zielbild (wohin es am Ende geht)

```
Duofy.Domain        Fachregeln in reinem C#, ohne Datenbank und ohne Oberfläche   (neu)
Duofy.Domain.Tests  Tests dazu (xUnit)                                            (neu)
Duofy.Api           Server: Datenbank, Anmeldung, Endpunkte                       (neu, später)
Duofy.Shared        Seiten und Komponenten, von Web und App gemeinsam genutzt     (vorhanden)
Duofy.Web           Web-Variante                                                  (vorhanden)
Duofy               MAUI-App für Android — baue ich                               (vorhanden)
```

Warum zuerst `Duofy.Domain`: Dort lernt Tom C# selbst (Klassen, Eigenschaften,
Methoden, Tests), ohne dass gleichzeitig Datenbank, Web und App im Weg stehen.
Und die App braucht später ohnehin einen Server, mit dem sie spricht — die
Fachregeln gehören deshalb nicht in die Oberfläche.

## Lernweg in Etappen

Jede Etappe ist ein kleines, lauffähiges Stück. Reihenfolge nach dem, was
aufeinander aufbaut — fachlich und beim Lernen.

| # | Was entsteht | Was Tom dabei lernt | Vorlage im Original |
|---|---|---|---|
| 0 | Git, Pflichtdateien, Projekte `Duofy.Domain` + Tests | wie eine .NET-Lösung aufgebaut ist, `dotnet build` / `dotnet test` | — |
| 1 | **Vertrag** mit der Regel „in welchem Monat fällig?" | Klasse, Eigenschaft, Enum, `decimal`, `DateOnly`, Methode, erster Test | `backend/app/models/commitment.py`, `tests/test_date_cases.py` |
| 2 | **Monatsplan**: aus Verträgen werden Posten kopiert | Listen, Schleifen, LINQ, Kopie statt Verweis | `models/plan.py`, `tests/test_plan_creation.py`, ADR 0002 |
| 3 | Erste Blazor-Seite: Verträge anlegen, Monat erzeugen (Daten nur im Speicher) | Komponente, Datenbindung, Formular, Dienst einhängen | `frontend/src/pages/CommitmentsPage.tsx` |
| 4 | Datenbank: Verträge und Pläne bleiben erhalten | EF Core, Migrationen, Postgres | `backend/alembic/`, ADR 0004 |
| 5 | Server `Duofy.Api` und Anmeldung; Web und App reden mit ihm | Endpunkte, Anmeldung, Fehler als Code | `backend/app/api/v1/`, ADR 0003 |
| 6 | Konten, Buchungen, Abhaken erzeugt die Buchung | Beziehungen zwischen Tabellen, Kontostand berechnen | `models/account.py`, `transaction.py`, ADR 0001 |
| 7 | Haushalt und Freigaben, danach Import, Verlauf, Hinweise | Rechteprüfung, Dateien einlesen | `services/grants.py`, `statements.py`, `flow.py`, `hints.py` |

Die Android-App ziehe ich ab Etappe 5 nebenher nach und berichte nur, was sich
getan hat.

## Dieser Plan gibt nur Etappe 0 und 1 frei

### Etappe 0 — Grundgerüst (mache ich, erkläre dabei kurz)

1. `git init`, Zweig `main`, sofort Feature-Zweig `feat/domain-commitment`.
   Nur lokal — noch nichts zu GitHub.
2. Pflichtdateien: `.gitignore` (für .NET), `README.md`, `CHANGELOG.md`,
   `.env.example`. Die Lizenzdatei kommt erst, wenn die Lizenzfrage geklärt ist
   (siehe unten).
3. Neue Projekte, in `Duofy.slnx` eingetragen:
   - `Duofy.Domain` (Klassenbibliothek, `net10.0`, keine Pakete)
   - `Duofy.Domain.Tests` (xUnit, Paketversionen fest eingetragen)
4. Diesen Plan nach `docs/plans/` im Projekt kopieren.
5. Commit: `chore: set up repository and domain projects`.

### Etappe 1 — Der Vertrag und seine Fälligkeit

**Teil A — ich zeige es vor (Test zuerst, dann Code):**

- `Duofy.Domain/CommitmentType.cs` — Enum: Contract, SavingsGoal, Debt, Income
- `Duofy.Domain/Commitment.cs` — vorerst nur die Felder, die für die Fälligkeit
  nötig sind: `Name`, `Amount` (`decimal`), `Type`, `IntervalMonths`,
  `FirstDueDate` (`DateOnly`), `EndsOn` (`DateOnly?`)
- Methode `IsDueIn(int year, int month)` mit den drei Regeln des Originals:
  nicht vor dem Start, nicht nach dem Ende, im Abstand ab der ersten Fälligkeit
  (Monate absolut gezählt, damit es über den Jahreswechsel stimmt)
- `Duofy.Domain.Tests/CommitmentDueTests.cs` — Tests mit ausgedachten Zahlen,
  z. B. „alle 3 Monate ab Februar ist im Mai fällig, im April nicht"

Ich erkläre dabei Zeile für Zeile: was eine Klasse ist, warum Geld `decimal`
ist und nie `double`, was das Fragezeichen bei `DateOnly?` bedeutet, wie ein
Test aufgebaut ist.

**Teil B — Tom schreibt selbst, ich begleite:**

- `EffectiveDueDay(year, month)`: Zahltag 31 wird im Februar zum 28. oder 29.
- `RunsInMonthOf(today)`: ist der Vertrag in diesem Monat noch nicht beendet?

Ich gebe jeweils die Aufgabe in einem Satz und zwei, drei Beispiele vor; Tom
schreibt erst den Test, dann die Methode. Danach schaue ich drüber.

Commits einzeln, z. B. `feat: add commitment with due month rule`,
`feat: clamp due day to month length`.

## Regeln aus dem Original, die von Anfang an gelten

- Beträge immer `decimal`, nie `double` oder `float`
- Keine echten Beträge, Namen oder Kontonummern in Code und Tests
- Code, Kommentare und Commits englisch; Gespräch und Doku deutsch
- Ein Test heißt so, wie das Verhalten ist, das er prüft

## Bewusst noch offen (entscheiden wir, wenn es dran ist)

- **Lizenz:** das Original ist AGPL-3.0, Toms Standard ist MIT — vor der ersten
  Veröffentlichung klären
- **GitHub:** neues Repository oder Zweig im alten, öffentlich oder privat
- **Datenbank:** Postgres wie im Original ist die naheliegende Wahl — Etappe 4
- **Wie Web und App mit dem Server reden** — Etappe 5

## Prüfung

- `dotnet build Duofy.slnx` läuft ohne Fehler (die MAUI-App darf dabei
  ausgenommen sein, falls die Android-Werkzeuge noch fehlen — dann nur
  `Duofy.Domain`, `Duofy.Domain.Tests` und `Duofy.Web` bauen)
- `dotnet test Duofy.Domain.Tests` ist grün; jeder Test war vorher einmal rot
- `Duofy.Web` startet weiterhin und zeigt die Vorlagenseite
- Tom kann in eigenen Worten sagen, warum „alle 5 Monate ab November" im April
  fällig ist
