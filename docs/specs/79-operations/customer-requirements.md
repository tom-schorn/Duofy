# Customer requirements: Operations of Plagevo

Format: 2

## Background

Plagevo is the hosted SaaS offering of Duofy ("Plagevo by Duofy", decided 2026-10-26). In the check-in of 2026-10-26 the human decided: it runs on the human's home server behind a Cloudflare Tunnel; plagevo.de is the landing page (built separately), app.plagevo.de the web version and api.plagevo.de the backend; only Cloudflare's free plan is used (Tunnel, DNS, Pages, WAF with one rate limit rule, Turnstile, Web Analytics); mails are sent through the SMTP access of noreply@example.org, which the imprint also names. Review round 5 (P-019) named three points that must be fixed before going public: the rate limit behind a proxy, persistent data protection keys, and https and TLS on hosted servers.

## Goal

The human starts Plagevo on the home server with one command, it is reachable under app.plagevo.de and api.plagevo.de through the tunnel, protected and backed up, without opening a port at home.

## Users

The human as operator; testers and later outside users of Plagevo.

## Requirements

R1 (must): Web, API, PostgreSQL and cloudflared run as containers started together with one command; settings and secrets come only from an environment file, documented in an example without secrets — because the operator needs one simple way to start, update and stop.
R2 (must): The containers follow the human's Docker rules: multi-stage builds, not running as root, health checks, labels for version and build date — because they are the human's standing rules for every image.
R3 (must): The tunnel serves app.plagevo.de to the web version and api.plagevo.de to the API; no port is opened at home; invitation links use app.plagevo.de — because the addresses were decided and the home network stays closed.
R4 (must): Behind Cloudflare, the API and the web version see the real client address, so the API's own rate limit works per client and not for everybody at once — because review round 5 found that all users would share one budget (P-019).
R5 (must): The keys that protect sign-in cookies, refresh tokens and mailed links survive a restart or an update of the containers — because otherwise every update signs everybody out and breaks every mailed link (P-019).
R6 (must): A hosted server refuses to start without an https public address and with mail TLS switched off — because tokens must never go out in clear text (P-019).
R7 (wont): Cloudflare Turnstile on registration, forgot password and sign-in — because the first release runs in invite mode for the human and a few testers, where nobody registers without a link, and the API checks would need a widget page that also serves the Android app; it comes with opening the registration (V2).
R8 (should): Web Analytics can be switched on by a token in the environment and then counts page views of the web version, but only after the person has agreed in a consent banner (amended 2026-11-01, #35, #43: the landing page asks for consent as well) — because the human wants to know the usage and the privacy policy must be true.
R9 (must): The database is backed up every day into a folder on the home server, the last 14 days are kept, and restoring is described and tested once — because the records loss of 2026-10-26 showed what an unsaved state costs.
R10 (must): An operations guide describes the setup step by step: Cloudflare (domain, tunnel, DNS, the one rate limit rule, Turnstile, analytics), the environment file, mail through noreply@example.org, first start with the setup code, update, backup and restore — because the human does it rarely and must be able to repeat it.
R11 (wont): The landing page, R2 storage, Email Routing, another mail provider, chat support, a rented server — because the human decided against them for now or they come separately.

## Constraints

- Builds on the project structure of #77 (Backend, Shared, Web, Mobile, Tests).
- Only Cloudflare's free plan; secrets never in the repository; the SMTP password only in the server's environment file.
- Hosted mode (Duofy:Hosted) with mail on; registration mode as the human chooses at start.

## Open questions

None.
