# Changelog

All notable changes to this project are documented in this file. The format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- A hosted server no longer starts with an http public address or with mail TLS switched off (spec 79)
- Repository restructured without any change of behaviour: `Duofy.Api`, `Duofy.Data` and `Duofy.Domain` are one project `Backend/Duofy.Backend` (folders `Api`, `Data`, `Domain`), the data formats are called `Duofy.Schemas`, projects lie in the folders `Backend`, `Shared`, `Web`, `Mobile` and `Tests`, and the tests of the API and the domain are one project `Duofy.Backend.Tests` (spec 77)

### Added

- Plagevo runs in its own Docker network `plagevo` (172.30.10.0/24) with fixed addresses (tunnel .2, api .11, web .12, backup .13); the tunnel points at addresses, the API trusts only the tunnel (`172.30.10.2/32`) and exempts only the web container; the cloudflared of a separate compose project joins the network as an external one (spec 79, amendment 2026-10-26)
- External database: `ConnectionStrings__Duofy` is required in `deploy/.env` (Plagevo uses a PostgreSQL server reached over Tailscale), the backup dumps that database and `backup.sh check` tests the connection; `deploy/own-db.yml` adds an own PostgreSQL in the internal network `plagevo-backend` for self-hosters
- `deploy/.env` accepts every setting in .NET's notation (`Duofy__Mail__Host`) as well as the old names (`DUOFY_MAIL_HOST`); the .NET notation wins
- `Duofy:Mail:FromName`: the sender name of mails, default `Plagevo`
- Operations of Plagevo behind a Cloudflare Tunnel (spec 79): container images for the API and the web version (multi-stage, non-root, health check, version and build date labels), `deploy/docker-compose.yml` with PostgreSQL, a daily backup kept 14 days and memory limits for a small server, joining the network of an existing `cloudflared` container, and the guide `docs/betrieb.md`
- `Duofy:TrustedProxies`: behind trusted proxies the API and the web version take the client address from `CF-Connecting-IP` and the scheme from `X-Forwarded-Proto`, so the sign-in limit counts per client
- `RateLimiting:ExemptAddresses`: connection addresses the sign-in limit does not apply to, for the web container
- `Duofy:KeysDirectory`: the keys of tokens and mailed links survive a restart or an update
- `Duofy:Analytics:Token`: Cloudflare Web Analytics on every page of the web version, without cookies
- `/health` on the web version for the container health check
- Blazor Hybrid solution with `Duofy.Shared`, `Duofy.Web` and the MAUI app
- `Duofy.Domain` for the business rules and `Duofy.Domain.Tests` for their tests
- Commitment with its due month, due day and last due date
- Month plan created from the commitments that fall due, as independent copies
- Account with a balance that is computed from its start amount and bookings
