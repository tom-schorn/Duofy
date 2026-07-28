# Duofy

Finanz-App mit Fokus auf **Planung statt Tracking**. Eigener Account pro Nutzer,
optional ein gemeinsam verwalteter Haushalt (Paare, WGs). Budget nach der
50/30/20-Regel.

## Stack

- **Backend:** Python 3.12 + FastAPI, SQLAlchemy, Alembic, PostgreSQL
- **Auth:** fastapi-users (JWT)
- **Frontend:** React + Vite, Tailwind + shadcn/ui
- **State:** TanStack Query (Server) + Zustand (UI)
- **i18n:** react-i18next — `de`, `en`
- **Mobile:** Capacitor (nach dem MVP, gleiche React-Codebase)
- **Tests:** pytest (ab Tag 1), Vitest (nach MVP)
- **Deploy:** Frontend → Cloudflare Pages, Backend → Docker auf vServer

## Layout

```
/backend    FastAPI-App
/frontend   React-App
```

## Regeln

- Beträge immer als `DECIMAL`/`NUMERIC`, **nie** als Float
- Konten von Anfang an als eigene Entität — echte Bankkonten kommen in V2 dazu
- Backend liefert Fehler-**Codes**, das Frontend übersetzt sie
- Frontend und Backend laufen auf getrennten Domains → CORS explizit halten
- Sprache: Gespräche Deutsch, Code / Commits / Issues Englisch

## Bankanbindung

- **MVP:** manuelle Eingabe + CSV-Import
- **V2:** GoCardless Bank Account Data (PSD2/AIS)
- PSD2 deckt nur **Zahlungskonten** ab — Depots (z. B. Scalable Capital) bleiben
  dauerhaft manuell. Re-Consent alle 90–180 Tage einplanen.
