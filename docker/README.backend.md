# Duofy — Backend

The API of [Duofy](https://github.com/tom-schorn/Duofy), a household finance app
that **plans instead of tracks**.

Most household ledgers ask *where did the money go?* Duofy asks first *where
should it go?* — you sit down once a month, hand out the money to items, and tick
them off later. Tracking falls out as a by-product.

Built for households where several people plan together but each has their own
account: couples, families and shared flats. Everyone has their own monthly plan;
whatever is carried together also appears in a shared household view.

## This image

FastAPI on Python 3.12, runs as a non-root user, and applies its database
migrations on start. It expects PostgreSQL 14 or newer.

It is **not meant to be run on its own** — use the compose file, which brings the
database and the web interface with it.

## Run it

Download [`docker-compose.yml`](https://github.com/tom-schorn/Duofy/blob/main/docker-compose.yml)
and [`.env.example`](https://github.com/tom-schorn/Duofy/blob/main/.env.example),
then:

```bash
cp .env.example .env      # fill in a database password and a JWT secret
docker compose up -d
```

Duofy is then on <http://localhost:8080>. Everything together stays under 256 MB
of memory.

## Environment

| Variable | Meaning |
|---|---|
| `POSTGRES_HOST` | database host — `db` inside the compose network |
| `POSTGRES_PORT` | default `5432` |
| `POSTGRES_DB` · `POSTGRES_USER` · `POSTGRES_PASSWORD` | must match what the database was created with |
| `JWT_SECRET` | signs the login tokens — generate with `openssl rand -hex 32` |
| `ENVIRONMENT` | `production` or `development` |

## Tags

| Tag | What it is |
|---|---|
| `latest` | the newest release |
| `1.2.0` | a specific release — pin this if you self-host |
| `dev` | current state of the `develop` branch, may break |

## Documentation

Concept, roadmap and contribution rules are in the
[wiki](https://github.com/tom-schorn/Duofy/wiki).

## Licence

[GNU AGPL-3.0](https://github.com/tom-schorn/Duofy/blob/main/LICENSE) — free to
use, run, study and change. If you offer a modified version as a network service,
you have to publish your source as well.
