# Changelog

## [0.2.0](https://github.com/tom-schorn/Duofy/compare/v0.1.0...v0.2.0) (2026-08-14)


### ⚠ BREAKING CHANGES

**Sessions now last a month instead of an hour** ([#38](https://github.com/tom-schorn/Duofy/issues/38)).
The access token is still a JWT but only lives fifteen minutes; beside it sits a
refresh token in an `HttpOnly` cookie that is renewed on every use. What you have
to do when updating:

* The migration runs on start and creates the `refresh_tokens` table. Nothing to do
  by hand.
* Two new values in your `.env`, both with defaults that suit most people:
  `REFRESH_LIFETIME_SECONDS` (30 days) and `COOKIE_SECURE` (`true`). **Set
  `COOKIE_SECURE=false` if you reach Duofy over plain http**, for instance on your
  home network only — browsers throw away secure cookies on http addresses, and
  without the cookie nobody stays signed in.
* Everyone signs in once more. Sessions from before this release do not carry over.

**The plan status and the confirmation are gone**
([#40](https://github.com/tom-schorn/Duofy/issues/40),
[#41](https://github.com/tom-schorn/Duofy/issues/41)).
A month is never "finished": planning happens on the last Saturday and things still
get added during the week after. The state also only existed in your own plan — the
shared household plan is composed on the fly and never had an object to confirm.

* `POST /plans/{plan_id}/confirm` no longer exists.
* Plan responses no longer carry `status` or `confirmedAt`.
* The migration drops `plans.status` and `plans.confirmed_at`. **Which months were
  confirmed cannot be recovered afterwards.** Nothing in Duofy depended on it, so
  nothing else has to change — but a backup before updating is the usual advice and
  applies here.

### Features

* keep sessions alive with refresh tokens ([#38](https://github.com/tom-schorn/Duofy/issues/38)) ([f9181c7](https://github.com/tom-schorn/Duofy/commit/f9181c777217d7ce2151365f47e9b2955ac7a938))


### Bug Fixes

* remove plan status and confirmation ([#40](https://github.com/tom-schorn/Duofy/issues/40)) ([e95bf3b](https://github.com/tom-schorn/Duofy/commit/e95bf3b6f2fecf17d61762356e5d992b223c9847))
* remove the last plan status references from the frontend types ([#41](https://github.com/tom-schorn/Duofy/issues/41)) ([424f458](https://github.com/tom-schorn/Duofy/commit/424f45866ef827b9d47b7db31ad61944691ffdf9))
