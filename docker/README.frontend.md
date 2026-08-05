# Duofy — Web interface

The web interface of [Duofy](https://github.com/tom-schorn/Duofy), a household
finance app that **plans instead of tracks**.

Most household ledgers ask *where did the money go?* Duofy asks first *where
should it go?* — you sit down once a month, hand out the money to items, and tick
them off later. Tracking falls out as a by-product.

Built for households where several people plan together but each has their own
account: couples, families and shared flats. Everyone has their own monthly plan;
whatever is carried together also appears in a shared household view.

## This image

A React application served by nginx. It also forwards everything under `/api` to
the backend container, so the browser only ever talks to **one** address — no
CORS to configure, and nothing to set up when you put it behind your own domain.

That is also why this image takes **no configuration at all**. The port is the
only thing you choose, and you choose it in the compose file.

It is **not meant to be run on its own** — it expects a container named `backend`
next to it.

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

For HTTPS and your own domain, put the reverse proxy you already run in front of
that port — Caddy, nginx proxy manager, Traefik or a Cloudflare tunnel. Duofy
does not ship one, because everyone already has their own.

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

[PolyForm Noncommercial 1.0.0](https://github.com/tom-schorn/Duofy/blob/main/LICENSE) —
free for private use and for non-profits, schools and public institutions.
Commercial operation is reserved to the copyright holder.
