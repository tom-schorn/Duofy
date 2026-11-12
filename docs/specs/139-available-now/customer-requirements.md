# Customer requirements: "Jetzt verfügbar" next to "Verplanbar"

Format: 2

## Background

"Verplanbar" (the free amount, PlanFigures.Free) counts planned income of the month before it arrives, for example the salary at the end of the month. The human reads it as money that is there and is misled (12.11.2026: "Verplanbar rechnet immer noch Sachen, die noch nicht auf dem Konto sein können", Duofy#139).

## Goal

Next to "Verplanbar im Monat" the person sees "Jetzt verfügbar": what is really on the accounts now, minus what still has to go out this month.

## Users

Every Duofy user looking at the start page or the month plan; household members with grants.

## Requirements

R1 (must): A second number "Jetzt verfügbar" = the balances of all accounts that count as available (the per-account flag, the same set as "Verplanbar") minus the open expenses of the current month — because that is what can really be spent now (human, 12.11.2026: two numbers; accounts by the per-account flag, amended 12.11.2026; open expenses until the end of the month).
R2 (must): "Verplanbar" stays as it is and is labelled "Verplanbar im Monat" with a hint that planned income is included — because both views are useful and the old number must not change meaning silently.
R3 (must): Both numbers show where "Verplanbar" shows today (start page, month plan, household view), with a short breakdown for "Jetzt verfügbar" (Konten, minus offen bis Monatsende) — because the human wants to see how it is made up.
R4 (must): In the household view and with the person switch, "Jetzt verfügbar" follows the same grant rules as the account balances — because balances of others are visible only with their grant.

## Constraints

Repository: Duofy (core feature for everyone). No database change.

## Open questions

None.
