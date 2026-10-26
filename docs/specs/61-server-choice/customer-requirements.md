# Customer requirements: Server choice

Format: 2

## Background

Duofy comes as a hosted service (SaaS) and for self-hosting. On 2026-10-21 (card P-003) the human decided that the app lets the person choose the server the way Bitwarden does: the server is shown at the bottom of the sign-in page and can be changed there. Google sign-in comes later and only for the hosted service (issue 62); the app must therefore be able to ask a server which sign-in ways it offers. On 2026-10-23 the human decided that the hosted address comes from the app configuration and is set when hosting exists.

## Goal

A person can point the app at the hosted service or at their own Duofy server before signing in, and the app remembers the choice.

## Users

Every app user once at the first start; self-hosters in particular, and the developer while testing against a PC on the home network.

## Requirements

R1 (must): The sign-in and register pages show the current server at the bottom; by default it is the hosted service from the app configuration — because most people use the hosted service and should not have to type anything.
R2 (must): Tapping the server opens a dialog where the person chooses the hosted service or enters their own address — because self-hosters need their own server.
R3 (must): Before an own address is taken, the app checks that a Duofy server answers there; if not, it says so and keeps the old server — because a typo must not lock the person out.
R4 (must): Only https addresses are accepted, except in debug builds where http is allowed — because tokens and passwords must not travel unencrypted.
R5 (must): The app remembers the chosen server across restarts and uses it for every request — because the choice is made once.
R6 (must): The API answers which sign-in ways it offers (for now only e-mail and password), and the app asks it after choosing a server — because Google sign-in will only exist on the hosted service.
R7 (should): Changing the server while tokens of another server are stored removes those tokens — because tokens of one server mean nothing on another.
R8 (wont): Google sign-in itself, several servers at once, server choice in the web version — because they are issue 62, not needed, or the human's part.

## Constraints

- The app currently reads `Api:BaseAddress` once at start (`Duofy/MauiProgram.cs`) and the API client takes it as a fixed base address; this has to become changeable at run time.
- The API already maps `/api/v1/health`.

## Open questions

None.
