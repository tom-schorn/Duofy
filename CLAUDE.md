# CLAUDE.md

Kontext für KI-Sitzungen (Claude Code, Claude im Chat mit GitHub-Zugriff). Menschen
lesen besser das [Wiki](https://github.com/tom-schorn/Duofy/wiki). Diese Datei ist
kurz und verweist; wenn sie dem Code widerspricht, gilt der Code — und die Datei
wird korrigiert.

## Was Duofy ist

Open-Source-Haushaltsbuch (AGPL-3.0) für Haushalte in Deutschland. Grundprinzip:
**planen statt tracken.** Für den Folgemonat wird ein Plan aus Verträgen erzeugt;
Abhaken eines Postens erzeugt die Buchung. Tracking ist Nebenprodukt, nicht Zweck.

Duofy gibt **Richtlinien** vor (50-30-20 als Orientierung, nicht als Zwang), statt
Nutzer mit Konfiguration allein zu lassen. Zielgruppe schließt Menschen ein, die
klare Struktur brauchen (u. a. ADHS, Autismus) → **klare Definitionen statt
Einstellungen.** Im Zweifel eine feste Regel, keine Option.

Hauptfall für V1: **Paar oder Familie**, Erwachsene mit eigenem Einkommen. Jede
Person hat ihren eigenen Plan; der Haushaltsplan ist eine **Brille** darüber,
keine eigene Tabelle. Der Haushalt besitzt nichts — kein Konto, keinen Plan.

## Sprache

- **Code englisch:** Bezeichner, Kommentare, Commit- und PR-Titel
- **Deutsch:** Issues, Wiki, README, diese Datei
- **Oberfläche:** vorerst nur Deutsch. Weitere Sprachen sollen andere per Pull
  Request beisteuern können, ohne Code anzufassen — Texte gehören deshalb nicht
  fest in Komponenten. Die Übersetzungseinheit baut #98 (`react-i18next`,
  `src/locales/de.json`); bis dahin stehen Oberflächentexte direkt in den
  Komponenten und Fehlercodes in `src/lib/api.ts`. Nach #98 kommt jeder neue
  Text als Schlüssel in den Katalog
- Das Backend liefert **Codes, nie Sätze**

## Repo

Monorepo, Arbeitsbranch `develop`, Default `main`.

```
backend/   FastAPI · SQLAlchemy 2 (async) · Alembic · Postgres · uv
frontend/  Vite · React · TypeScript · shadcn/ui · TanStack Query · Recharts
docker/    Images; docker-compose.yml zum Selbsthosten, compose.dev.yaml zur Entwicklung
legal/     Rechtstexte
```

Backend: `app/api/v1` (Endpunkte), `app/services` (Regeln), `app/models`,
`app/schemas`, `app/core/permissions.py`. Frontend: `src/pages`,
`src/components`, `src/lib/domain.ts` (Typen und Domänenlogik, Spiegel der
Backend-Enums), `src/lib/help.tsx` (Hilfespalte).

## Vor jedem Pull Request

```bash
cd backend && uv run ruff check app/ tests/ && uv run pytest -q
cd frontend && npx tsc --noEmit -p tsconfig.app.json && npm run lint && npm run build
```

Genau das läuft in der CI. Backend-Tests brauchen ein echtes Postgres
(`POSTGRES_DB` muss „test" enthalten, die Fixtures löschen alle Tabellen).

## Arbeitsweise

- **Jede Arbeit hat ein Issue.** Neue Issues folgen der Vorlage
  `.github/ISSUE_TEMPLATE/feature_spec.yml`: Zweck · Datenmodell ·
  Automatisierungen · Entscheidungen und warum · Akzeptanzkriterien · Offene
  Fragen · Nicht im Umfang
- **Branch:** `typ/issue-nummer-kurzbeschreibung` (`feat`, `fix`, `docs`,
  `refactor`, `test`, `chore`)
- **PR-Titel** nach Conventional Commits — daraus entstehen Version und Changelog.
  Squash nach `develop`, Merge-Commit nach `main`
- **Jeder Commit signiert** (`git commit -s`, DCO). Commits über die API brauchen
  die Zeile `Signed-off-by:` in der Nachricht, sonst bleibt der PR rot
- **Version** steht nur in `version.txt`
- **Immer Backend und Frontend zusammen ansehen.** Ein Modell ändert sich nie nur
  auf einer Seite
- **Im Code nachsehen, nicht raten.** Aussagen über das Verhalten der App erst nach
  Blick in den Quelltext
- **Wer ein Feature ändert, zieht das Wiki nach** (siehe unten)
- **Jedes Stück wird unabhängig geprüft.** Eine Session, die nicht gebaut hat,
  reviewt vor dem Merge (erst Abnahmekriterien, dann Qualität und Sicherheit);
  danach bespricht Tom das Ergebnis. Migrationen gibt Tom frei
- **Langlebige Entscheidungen** stehen als ADR unter `docs/adr/` (Format und wann
  eine nötig ist: `docs/adr/README.md`)
- **PR-Größe:** Richtwert unter 300 geänderte Zeilen ohne Tests und Migrationen;
  ab etwa 1000 vorher teilen

## Wiki

Das Wiki ist ein eigenes Git-Repo, nicht Teil dieses Repos:

```bash
git clone https://github.com/tom-schorn/Duofy.wiki.git
```

Schreiben geht nur per Git mit Push-Rechten, nicht über die GitHub-API oder den
MCP-Server. Eine Sitzung ohne Push-Rechte schreibt die Änderung als Patch oder
Befehlsfolge auf und übergibt sie.

**Offener Umbau (#96), einmalig:** Das Wiki wird nur noch deutsch.

1. Alle englischen Seiten löschen (die ohne `-de`-Endung, außer `_Sidebar.md`
   und `_Footer.md`, falls vorhanden)
2. Jede `*-de.md` auf den Namen ohne Endung umbenennen (`git mv
   Installation-de.md Installation.md`, `Home-de.md` → `Home.md` usw.)
3. Alle Links `](Seite-de)` → `](Seite)` in allen Seiten und in `_Sidebar.md`
4. Sprachumschalter und Hinweise auf die englische Fassung entfernen
5. Programmierrichtlinien, Abschnitt „Sprache": Code englisch, Issues und Doku
   deutsch, Oberfläche vorerst deutsch mit Übersetzungseinheit (#98)
6. Neue Seite „Neue Sprache beitragen" (#98)
7. Prüfen, dass jeder Link aus dem README ins Wiki noch trifft

**Danach laufend:** Die Seiten werden Stück für Stück überarbeitet, jeweils wenn
ihr Thema in einem Issue dran ist. Stand nach #83 bis #95 prüfen: kein `Block`,
kein `Rhythm`, kein `remaining_debt`, Verlauf und Monatsübertrag wie in #93/#94.

## Regeln, die überall gelten

- **Keine echten Daten** — keine echten Beträge, Namen, Kontonummern, IBANs in
  Code, Tests, Issues, Commits, Wiki oder Screenshots. Das Repo ist öffentlich
- **Beträge** immer `Numeric` / `Decimal`, nie `Float`
- **Enums** über `enum_column()` aus `app/db/types.py` (speichert den Wert, nicht
  den Namen)
- **Fehler** als Code: `require(bedingung, "code")` → `{"code": "..."}`; das
  Frontend übersetzt
- **Kein Endpunkt ohne Rechteprüfung**, sobald fremde Daten im Spiel sind.
  `AccessLevel` (plan < view < edit < delete) immer über `rank` vergleichen
- **Jede Person gibt ihre eigenen Daten frei.** Niemand stellt Rechte für andere ein
- **Kontostand wird nicht gespeichert** — `opening_balance` plus Buchungen
- **Snapshot-Muster:** Vertrag → Planposten wird beim Anlegen des Monats kopiert;
  spätere Vertragsänderungen ändern alte Monate nicht
- **Hinweise im Plan** berechnet das Backend (`code`, `severity`, `position_id`,
  `params`), abgeleitet, nie gespeichert (#88)
- **Migrationen:** eine Änderung, eine Migration. CHECK-Constraints von Hand
  nachtragen, `server_default` bei NOT NULL auf gefüllten Tabellen, benannte
  Fremdschlüssel. Downgrade vor dem PR ausprobieren
- **Tests:** Name beschreibt das Verhalten. Neue Logik bringt einen Test mit
- **Logging** über `logging` nach stdout, Stufe per `LOG_LEVEL`. Nie Beträge,
  Namen, E-Mails, IBANs, Tokens oder Anfrageinhalte loggen; Fehler nur mit Klasse
  und Stack, nicht mit Meldung. Neue Routen mit freiem Pfadteil ≤ 16 Zeichen im
  Log ausdrücklich maskieren (`app/core/logging.py`)

## Begriffe

| Begriff | Bedeutung |
|---|---|
| Vertrag (`Commitment`) | wiederkehrende Zahlung oder Einnahme; **eine Abbuchung = ein Vertrag** |
| Posten (`PlanPosition`) | Zeile im Monatsplan, Kopie eines Vertrags oder einmalig |
| Budget | 50-30-20-Topf: `income`, `needs`, `wants`, `savings` (bis #83 im Code `Block`) |
| Abstand (`interval_months`) | alle wie viele Monate ein Vertrag fällig ist, 1–120 (bis #107 `Rhythm`) |
| Erste Fälligkeit (`first_due_date`) | Pflicht; ihr Tag ist der Zahltag (bis #108 zusätzlich `due_day`) |
| Laufzeitende (`ends_on`) | letzter Monat, in dem ein Vertrag fällig ist; leer = unbefristet (bis #109 `active`) |
| Verpflichtung | Posten mit Häkchen (Miete) |
| Limit (`is_limit`) | Posten ohne Häkchen, Ist nur aus Buchungen (Lebensmittel); bis #83 `is_budget` |
| Umbuchung | Buchung mit `counter_account_id` zwischen eigenen Konten |
| Monatsübertrag | Buchungsart, die den Startstand eines Kontos im Monat festlegt, bewegt keinen Kontostand (#94) |
| Verlauf | Kurve durch den Monat, Standardkonto, Plan bis gebucht (#93) |

## Stand und Reihenfolge V1

V1 = erste Version für andere; danach nur Bugfix-Releases (1.0.x). Reihenfolge
nach Abhängigkeiten:

1. ~~#13 Test-Fundament, #98 Übersetzungseinheit, #83 Modell aufräumen (#106–#110)~~
   — erledigt
2. #95 (erledigt), #87, #10
3. #94 → #4
4. #88 → #93
5. #91, #84
6. Release: #15, #64, #65, #66, zuletzt #58 (Hilfespalte) und #96 (README, Wiki)

Querschnitt, erledigt: #117 Logging, #118 Dependabot, #119 ADR-Ordner. Offen:
#116 Trunk statt develop (Zeitpunkt klären).

V2: Verträge und Forderungen (#89), Gemeinschaftskonten (#92), Sparziele mit
eigenem Stand (#86). V3: Anträge (#90).
