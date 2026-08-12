# Duofy Backend

FastAPI + SQLAlchemy (async) + Alembic + PostgreSQL.

## Start

Die Datenbank läuft im Container, die App nativ.

```bash
# einmalig
cp .env.local.example .env.local
uv sync

# Datenbank (aus dem Projekt-Root)
docker compose up -d

# App
uv run uvicorn app.main:app --reload
```

→ API auf http://localhost:8000
→ Docs auf http://localhost:8000/docs

## Befehle

```bash
uv run pytest                                   # Tests
uv run ruff check . && uv run ruff format .     # Lint + Format

uv run alembic revision --autogenerate -m "..."  # Migration erzeugen
uv run alembic upgrade head                      # Migration anwenden
uv run alembic downgrade -1                      # eine zurück
```

## Aufbau

```
app/
├── main.py           FastAPI-App, CORS, Health-Endpunkte
├── core/config.py    Settings aus .env.local
├── db/
│   ├── base.py       DeclarativeBase — Basis aller Modelle
│   └── session.py    Engine + get_session-Dependency
└── models/           ← hier kommen die Modelle rein
```

## Wichtig

**Neue Modelle müssen in `app/models/__init__.py` importiert werden**, sonst
sieht Alembic sie beim Autogenerate nicht und erzeugt eine leere Migration.

**Beträge immer als `Numeric(12, 2)`** — nie `Float`. Bei Geld führt
Fließkomma zu Rundungsfehlern, die erst nach Monaten auffallen.

```python
from sqlalchemy import Numeric
from decimal import Decimal

betrag: Mapped[Decimal] = mapped_column(Numeric(12, 2))
```

## Postgres

Läuft auf Port **5433** (nicht 5432), damit es sich nicht mit anderen lokalen
Datenbanken beißt.

```bash
docker compose logs -f db     # Logs
docker compose down           # stoppen
docker compose down -v        # stoppen + Daten löschen
```
