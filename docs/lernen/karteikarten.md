# Karteikarten

Toms Lernkarten zu C#, EF Core und ASP.NET Core. Claude fragt sie im Gespräch ab
und trägt das Ergebnis hier ein.

## So funktioniert es

Jede Karte liegt in einem von fünf Fächern. Neue Karten beginnen in Fach 1.

- Richtig beantwortet: Die Karte rückt ein Fach weiter.
- Falsch oder nur halb: Die Karte fällt zurück in Fach 1.

Je höher das Fach, desto seltener kommt die Karte dran:

| Fach | Wieder dran nach |
|---|---|
| 1 | 1 Tag |
| 2 | 2 Tagen |
| 3 | 4 Tagen |
| 4 | 8 Tagen |
| 5 | 16 Tagen |

Eine Karte in Fach 5, die wieder richtig beantwortet wird, gilt als gelernt.

## Karten

| Nr | Thema | Frage | Kern der Antwort | Fach | Fällig |
|---|---|---|---|---|---|
| 1 | EF Core | Database First oder Code First: Woran entscheidest du, welchen Weg du nimmst? | Wem gehört die Datenbank? Gibt es sie schon und gehört sie jemand anderem: Database First. Baust du etwas Neues: Code First. | 2 | 2026-10-21 |
| 2 | EF Core | Was ist ein POCO, und warum ist `Commitment` einer? | Eine ganz gewöhnliche Klasse, die nichts über die Datenbank weiß, ohne EF-Basisklasse. `Commitment` liegt in `Duofy.Domain`, das EF Core gar nicht kennt. | 1 | 2026-10-20 |
| 3 | EF Core | Was ist die Fluent API, und wo steht sie in Duofy? | Die Angaben zur Datenbank als aneinandergehängte Aufrufe im Builder, in `OnModelCreating` im `DuofyDbContext`. | 1 | 2026-10-20 |
| 4 | EF Core | Nenne zwei Gründe, warum Duofy den Builder statt Attributen wie `[MaxLength(200)]` benutzt. | Die Klasse soll nichts über die Datenbank wissen. Und mit Attributen geht nicht alles (zum Beispiel das Mitlöschen), also steht alles an einer Stelle. | 2 | 2026-10-21 |
| 5 | EF Core | Warum bekommen mit `HasDefaultValue(DateTime.Now)` alle Zeilen dieselbe Zeit, und wie macht man es richtig? | C# rechnet die Zeit einmal beim Erzeugen der Migration aus, ein fester Wert landet in der Datenbank. Richtig: `HasDefaultValueSql("now()")`, dann rechnet Postgres bei jeder Zeile neu. | 2 | 2026-10-21 |
| 6 | C# | Warum steht bei `DateTime.Now` keine Klammer? | Es ist eine Property, keine Methode. Sie sieht aus wie ein Feld, führt aber Code aus. | 2 | 2026-10-21 |
| 7 | EF Core | Woher weiß EF Core, welche Migrationen schon gelaufen sind? | Aus der Tabelle `__EFMigrationsHistory`, dort steht jede gelaufene Migration mit Namen. | 2 | 2026-10-21 |
| 8 | EF Core | Du änderst eine Klasse und startest ohne neue Migration. Was passiert? | Die Datenbank bleibt, wie sie war. `Migrate()` bricht beim Start ab, weil Änderungen ausstehen (seit EF Core 9). Ohne `Migrate()` kommt der Fehler spätestens beim Zugriff auf die fehlende Spalte. | 2 | 2026-10-21 |
| 9 | EF Core | Wofür sind `Up` und `Down` in einer Migration? | `Up` führt die Änderung aus, `Down` macht sie rückgängig. | 2 | 2026-10-21 |
| 10 | EF Core | Darfst du eine Migration löschen, die schon gelaufen ist? Und wann geht es doch? | Nein, sonst hält EF Core die neue für ungelaufen und will die Tabellen doppelt anlegen. Solange sie nirgends gelaufen ist: `dotnet ef migrations remove`. | 2 | 2026-10-21 |
| 11 | EF Core | Was ist eine Konvention, und worauf schaut EF Core dabei? | Eine Standardannahme, wenn du nichts angibst. EF Core liest nur Namen und Typen, nie die Daten. | 1 | 2026-10-20 |
| 12 | EF Core | Welche Eigenschaft wird ohne Angabe zum Primärschlüssel? | `Id` oder Klassenname plus `Id`, bei `Buch` also `BuchId`. Sonst mit `HasKey(...)` festlegen. | 2 | 2026-10-21 |
| 13 | EF Core | Konvention, Attribut, Builder: Wer gewinnt? | Der Builder überstimmt das Attribut, das Attribut überstimmt die Konvention. | 2 | 2026-10-21 |
| 14 | EF Core | Warum `HasPrecision(12, 2)` statt `HasColumnType("numeric(12,2)")`? | Typnamen sind in jeder Datenbank anders. `HasPrecision` übersetzt EF Core passend, `HasColumnType` bindet dich an eine Datenbank. | 2 | 2026-10-21 |
| 15 | EF Core | Die Eigenschaft heißt in C# `OwnerId`, die Spalte in der Datenbank aber `owner_id`. Was sorgt dafür? | `UseSnakeCaseNamingConvention()` in `Program.cs` ist eine nachgerüstete Konvention, die alle Namen auf einmal umschreibt. | 1 | 2026-10-20 |
| 16 | C# | In JavaScript gibt es die Array-Methoden `filter`, `map` und `find`. Wie heißen die passenden LINQ-Methoden in C#? | `Where`, `Select`, `FirstOrDefault`. Dazu `Sum` und `OrderBy`. | 1 | 2026-10-20 |
| 17 | C# | Was bedeutet `List<Booking>`? | Eine Liste, in der Buchungen liegen. Die spitzen Klammern nennen den Typ, der drinsteckt (Generics), wie `list[Booking]` in Python. | 2 | 2026-10-21 |
| 18 | C# | Was entspricht in C# dem `Promise` aus JavaScript? | `Task`. `async` und `await` funktionieren wie in JavaScript. | 1 | 2026-10-20 |
| 19 | C# | Was bedeutet das Fragezeichen in `DateOnly?`, und was macht EF Core daraus? | Der Wert darf fehlen (null). Die Spalte darf in der Datenbank leer sein, ohne Fragezeichen ist sie Pflicht. | 2 | 2026-10-21 |
| 20 | ASP.NET Core | Wer macht bei der Anmeldung was: ASP.NET Core Identity oder EF Core? | Identity prüft Passwörter und setzt Cookies. EF Core speichert die Benutzer in der Tabelle `users`. | 1 | 2026-10-20 |

## Verlauf

| Datum | Abgefragt | Richtig |
|---|---|---|
| 2026-10-19 | 20 | 13 |
