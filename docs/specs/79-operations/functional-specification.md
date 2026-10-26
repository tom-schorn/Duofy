# Functional specification: Operations of Plagevo

Format: 2

## Summary

A Docker Compose setup in `deploy/` with images for the API and the web version, PostgreSQL, cloudflared and a daily backup job; the API and web trust forwarded client addresses from the tunnel; data protection keys persist in a volume; hosted servers require https and mail TLS; an optional Cloudflare Web Analytics beacon; an operations guide.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R10 without R7. R7 and R11 are out of scope.

## Components

- [Instance](docs/components/instance.md)
- [User account](docs/components/user-account.md)

## Solution

- **Images**: `Backend/Duofy.Backend/Dockerfile` and `Web/Duofy.Web/Dockerfile`, multi-stage (SDK build, ASP.NET runtime), non-root user, `HEALTHCHECK` on the health endpoints, labels `org.opencontainers.image.version` and `org.opencontainers.image.created` from build arguments.
- **Compose** (`deploy/docker-compose.yml`): services `api`, `web`, `db` (PostgreSQL 18 with a named volume), `tunnel` (cloudflared with `TUNNEL_TOKEN`), `backup` (pg_dump daily into `./backups`, 14 days kept); only internal networks, no published ports; `deploy/.env.example` lists every setting (connection, Duofy:Hosted, PublicUrl https://app.plagevo.de, RegistrationMode, SetupCode, mail settings for noreply@example.org, analytics token, tunnel token) without values for secrets.
- **Tunnel ingress** (configured in the Cloudflare dashboard, documented): app.plagevo.de to http://web:8080, api.plagevo.de to http://api:8080.
- **Client address**: API and web use forwarded headers and take the client address from `CF-Connecting-IP` only when the request comes from the tunnel container's network; the API's rate limit partitions by that address.
- **Keys**: ASP.NET data protection persists to a volume (`/keys`); the application name is fixed so restarts keep tokens valid.
- **Hosted checks**: with `Duofy:Hosted` true the API stops at start when PublicUrl is not https or Mail:UseTls is false, naming the setting.
- **Analytics**: setting `Duofy:Analytics:Token`; when set, the web host adds Cloudflare's Web Analytics beacon to every page.
- **Guide**: `docs/betrieb.md` in German, step by step, plus a README section "Plagevo betreiben".

## Behaviour

- R1: `docker compose --env-file .env up -d` in `deploy/` starts all services; `docker compose ps` shows them healthy.
- R4: two clients with different `CF-Connecting-IP` through the tunnel get separate rate limit budgets; a forged header from outside the tunnel network is ignored.
- R5: after recreating the api container, an existing refresh token still works.
- R6: Hosted with an http PublicUrl or Mail:UseTls false: the API exits with one log line naming the setting.
- R9: the backup service writes one dump per day named by date; files older than 14 days are deleted; the guide shows the restore with psql.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| No new endpoints | – | All endpoints answer as before |

External input: yes

## Acceptance criteria

- [ ] A1 (R1, R2): Given the repository, when `docker compose config` runs in deploy/ with .env.example copied to .env, then it is valid, no service publishes a port, and both images build with a non-root user, a HEALTHCHECK and the two labels.
- [ ] A2 (R4): Given the API behind a trusted proxy network, when two requests carry different CF-Connecting-IP values, then they count in separate rate limit partitions; when the header comes from an untrusted address, it is ignored.
- [ ] A3 (R5): Given a data protection key ring in a directory, when the API is started twice with the same directory, then a refresh token issued by the first run is accepted by the second.
- [ ] A4 (R6): Given Hosted true, when PublicUrl is http://app.plagevo.de or Mail:UseTls is false, then the API stops at start and the log names the setting.
- [ ] A7 (R8): Given an analytics token, when a page of the web version is rendered, then it contains the beacon with that token; without a token it does not.
- [ ] A8 (R9): Given the backup service, when it runs, then a dump file with the date appears and files older than 14 days are removed; the guide's restore command restores it into an empty database, tested once by hand and noted in the guide.
- [ ] A9 (R3, R10): Given docs/betrieb.md, when it is read, then it covers the Cloudflare steps (domain, tunnel with app and api, one rate limit rule, Turnstile, analytics), the environment file, mail through noreply@example.org, first start with the setup code, update, backup and restore.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Web.Tests/DeployFilesTests.cs, plus `docker compose --env-file .env.example config` and both image builds by hand |
| R2 | A1 | Tests/Duofy.Web.Tests/DeployFilesTests.cs, plus `docker image inspect` of both images (user 1654, HEALTHCHECK, labels) |
| R3 | A9 | docs/betrieb.md: steps 2.2 (tunnel, app and api) and 3 |
| R4 | A2 | Tests/Duofy.Backend.Tests/ForwardedClientTests.cs and Tests/Duofy.Web.Tests/ForwardedClientTests.cs |
| R5 | A3 | Tests/Duofy.Backend.Tests/KeyRingTests.cs, plus a refresh after recreating the api container by hand |
| R6 | A4 | Tests/Duofy.Backend.Tests/HostedStartTests.cs |
| R8 | A7 | Tests/Duofy.Web.Tests/AnalyticsTests.cs |
| R9 | A8 | deploy/backup.sh, run and restored by hand on 2026-10-26 as noted in docs/betrieb.md step 10 |
| R10 | A9 | docs/betrieb.md |

## Threats

- Can someone pretend to be someone else? Forwarded client addresses are trusted only from the tunnel network; registration runs in invite mode.
- Can someone change data? Nothing new; the home network publishes no port, only the tunnel reaches the containers.
- Can someone deny an action? Logs keep the real client address behind the tunnel.
- Can someone read what they should not? Secrets live only in the server's environment file; keys and backups stay on the home server; hosted mode refuses http and mail without TLS.
- Can someone bring it to a halt? Cloudflare's WAF and one rate limit rule in front, the API's own rate limit per real client behind it.
- Can someone gain rights they should not have? The first administrator still needs the setup code of at least 16 characters.

## Non-goals

- The landing page, R2, Email Routing, other mail providers, chat support, a rented server, Kubernetes.

## Risks

- Availability depends on the home server, its power and internet; acceptable for the first release with testers.
- The sending limits of the mail provider provider; checked when mail goes live.

## Open questions

None.

## Approval

Approved by the human on 2026-10-26

Decided in the check-in of 2026-10-26: home server behind a Cloudflare Tunnel, app.plagevo.de and api.plagevo.de, Cloudflare free plan (Tunnel, DNS, Pages, WAF with one rate limit rule, Turnstile, Web Analytics), mail through noreply@example.org, daily local backups instead of R2.

Amendment 2026-10-26 (decided by the human in the check-in): Turnstile moves to V2 (R7 wont, A5 and A6 removed), because the first release runs in invite mode and the app would need a separate widget page.

Amendment 2026-10-26: the web container is exempt from the API's auth rate limit (human's choice); Cloudflare's rule limits web users per client.
