# Duofy

**A finance app that plans instead of tracking.**

[![CI](https://github.com/tom-schorn/Duofy/actions/workflows/ci.yml/badge.svg)](https://github.com/tom-schorn/Duofy/actions/workflows/ci.yml)
[![License: AGPL v3](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)

Most household ledgers ask *where did the money go?* Duofy asks first: *where
should it go?* You sit down once a month, hand the money out to items, and tick
them off afterwards. Tracking falls out as a by-product.

Built for households where several people plan together but each has their own
account — couples, families, shared flats. Everyone keeps their own monthly plan;
whatever is carried together also shows up in a household view. It works for one
person alone just as well, and the household can join later without anything being
rebuilt.

> **Early days.** Duofy is below 1.0. Things change, and an update may need manual
> steps. The [roadmap](https://github.com/tom-schorn/Duofy/wiki/Roadmap) says what
> already works and what does not.

## Run it

```bash
curl -O https://raw.githubusercontent.com/tom-schorn/Duofy/main/docker-compose.yml
curl -O https://raw.githubusercontent.com/tom-schorn/Duofy/main/.env.example
cp .env.example .env
```

Two values are mandatory — a database password, and `JWT_SECRET` from
`openssl rand -hex 32`. Everything else has a default that suits most people.

```bash
docker compose up -d
```

Duofy answers on `http://localhost:8080`. Register there; the first account is an
account like any other, there is no administrator role. Three containers, together
under 256 MB.

The [installation page](https://github.com/tom-schorn/Duofy/wiki/Installation) has
the rest — reverse proxy, backups, updating.

## Documentation

Everything lives in the [wiki](https://github.com/tom-schorn/Duofy/wiki):

| | |
|---|---|
| [Installation](https://github.com/tom-schorn/Duofy/wiki/Installation) | getting Duofy running |
| [First steps](https://github.com/tom-schorn/Duofy/wiki/First-Steps) | from an empty account to your first monthly plan |
| [Concept](https://github.com/tom-schorn/Duofy/wiki/Concept) | why Duofy is built the way it is |
| [Roadmap](https://github.com/tom-schorn/Duofy/wiki/Roadmap) | what works, and what comes next |
| [Contributing](https://github.com/tom-schorn/Duofy/wiki/Contributing) | rules, branches, pull requests |
| [Coding guidelines](https://github.com/tom-schorn/Duofy/wiki/Coding-Guidelines) | what code looks like here |

Every page exists in German as well — the link sits at the top of each one.

## Where to say something

| You have | It goes to |
|---|---|
| a bug | an [issue](https://github.com/tom-schorn/Duofy/issues/new/choose) |
| an idea, or a case Duofy cannot handle | [Discussions › Ideas](https://github.com/tom-schorn/Duofy/discussions/new?category=ideas) |
| a question | [Discussions › Q&A](https://github.com/tom-schorn/Duofy/discussions/new?category=q-a) |
| a security vulnerability | a [private report](https://github.com/tom-schorn/Duofy/security/advisories/new), never a public issue |

The split is deliberate: the issue list is the roadmap, so only agreed work sits in
it. An idea starts as a discussion, and once we agree on it, an issue follows.

**Cases Duofy cannot handle are the most valuable thing you can send.** It grew out
of one household and carries its point of view — a payment that fits no field, an
income that will not go in, a way of splitting costs it does not know. Say what
does not work for you; the solution comes later.

One rule everywhere, because this is a finance app: **no real amounts, names or
account numbers.** Made-up figures describe a problem just as well, and whatever
lands in a public issue stays there.

## Licence

[GNU AGPL-3.0](LICENSE). Use it, run it, change it, pass it on. Whoever offers a
modified version as a network service publishes their source as well — that is the
only condition.
