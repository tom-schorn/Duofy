# User

`users` · `backend/app/models/user.py`

Ein Nutzer mit eigenem Account. Duofy funktioniert **ohne Haushalt** — eine
Einzelperson legt Verpflichtungen an und plant. Kommt später jemand dazu, muss
nichts umgebaut werden.

## Felder

| Feld | Typ | Herkunft | |
|---|---|---|---|
| `id` | UUID | fastapi-users | Primärschlüssel |
| `email` | String | fastapi-users | eindeutig, indiziert |
| `hashed_password` | String | fastapi-users | **nur der Hash** |
| `is_active` | bool | fastapi-users | Konto gesperrt oder nicht |
| `is_superuser` | bool | fastapi-users | |
| `is_verified` | bool | fastapi-users | E-Mail bestätigt |
| `first_name` | String(100) | eigen | |
| `last_name` | String(100) | eigen | |

Die ersten sechs Felder kommen von `SQLAlchemyBaseUserTableUUID`. Nur Vor- und
Nachname sind selbst ergänzt.

## Warum es kein Passwort-Feld gibt

Ein Klartext-Passwort steht **nie** in der Datenbank. fastapi-users nimmt es
beim Registrieren entgegen, hasht es und legt nur `hashed_password` ab. Wer
ein Feld `password` im Modell anlegt, hat einen Sicherheitsfehler gebaut.

## Warum die Tabelle `users` heißt

`user` ist in PostgreSQL ein **reserviertes Wort**. Ohne Anführungszeichen
führt jede Abfrage darauf zu Fehlern.

## Beziehungen

```
User ──< HouseholdMember      in welchen Haushalten
User ──< Commitment           seine Verträge, Sparziele, Schulden
User ──< Plan                 seine Monatspläne
User ──< PlanPositionChange   was er geändert hat
```

## Endpunkte

Von fastapi-users bereitgestellt, registriert in `app/api/v1/auth.py`:

```
POST  /api/v1/auth/register           Registrierung
POST  /api/v1/auth/jwt/login          Login, liefert JWT
POST  /api/v1/auth/jwt/logout
POST  /api/v1/auth/forgot-password
POST  /api/v1/auth/reset-password
GET   /api/v1/users/me                eigenes Profil
PATCH /api/v1/users/me                Profil ändern
```

## Offen

- E-Mail-Verifizierung ist vorbereitet (`is_verified`), aber nicht aktiviert —
  es fehlt der Mailversand
- Konto löschen: was passiert mit Posten, die in einem Haushaltsplan hängen?
  Aktuell `CASCADE`, also weg. Für die Mitplaner wäre das ein Loch
