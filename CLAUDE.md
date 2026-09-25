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
- Die Programmierrichtlinien im Wiki sagen noch „alles auf Englisch" — veraltet,
  wird überarbeitet
- Der Endnutzer sieht Deutsch; das Backend liefert **Codes, nie Sätze**

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

## Regeln, die überall gelten

- **Keine echten Daten** — keine echten Beträge, Namen, Kontonummern, IBANs in
  Code, Tests, Issues, Commits oder Screenshots. Das Repo ist öffentlich
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

## Begriffe

| Begriff | Bedeutung |
|---|---|
| Vertrag (`Commitment`) | wiederkehrende Zahlung oder Einnahme; **eine Abbuchung = ein Vertrag** |
| Posten (`PlanPosition`) | Zeile im Monatsplan, Kopie eines Vertrags oder einmalig |
| Budget | 50-30-20-Topf: `income`, `needs`, `wants`, `savings` (bis #83 im Code `Block`) |
| Verpflichtung | Posten mit Häkchen (Miete) |
| Limit (`is_limit`) | Posten ohne Häkchen, Ist nur aus Buchungen (Lebensmittel); bis #83 `is_budget` |
| Umbuchung | Buchung mit `counter_account_id` zwischen eigenen Konten |
| Monatsübertrag | Buchungsart, die den Startstand eines Kontos im Monat festlegt, bewegt keinen Kontostand (#94) |
| Verlauf | Kurve durch den Monat, Standardkonto, Plan bis gebucht (#93) |

## Stand und Reihenfolge V1

V1 = erste Version für andere; danach nur Bugfix-Releases (1.0.x). Reihenfolge
nach Abhängigkeiten:

1. #13 Test-Fundament → #83 Modell aufräumen
2. #95, #87, #10
3. #94 → #4
4. #88 → #93
5. #91, #84
6. Release: #15, #64, #65, #66, zuletzt #58 (Hilfespalte) und #96 (README, Wiki)

V2: Verträge und Forderungen (#89), Gemeinschaftskonten (#92), Sparziele mit
eigenem Stand (#86). V3: Anträge (#90).
