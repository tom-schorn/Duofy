# Customer requirements: Verplanbar counts money taken back from savings

Format: 2

## Background

Spec 93 (live since 04.11.2026) computes Verplanbar from income, fixed positions, carry-over and booked lines without a position; transfers are ignored. When the human takes money back from a savings account (Depot-Verrechnung, Tagesgeld-Verrechnung: accounts that do not count as available) to the checking account and spends it, the expense lowers Verplanbar but the withdrawal does not raise it. October 2026 therefore shows -409.97 although the real rest was -66.49.

The human, 05.11.2026 (P-099): "das ist physikalisch nicht möglich". In the meeting 05.11.2026 the human chose: a withdrawal from savings raises Verplanbar by its amount, like an extra income; a deposit into savings without a position lowers it; transfers between two available accounts still do not count.

## Goal

Verplanbar matches what is really left in the available accounts of the month.

## Users

Everyone who plans a month, alone or in a household.

## Requirements

R1 (must): A transfer of the plan month from an account that does not count as available into one that does raises Verplanbar by its amount — because the human chose "erhöht Verplanbar".
R2 (must): A transfer of the plan month from an available account into one that does not count as available lowers Verplanbar, unless its line is on a position — because a deposit on a savings position already counts as a fixed position.
R3 (must): A transfer between two available accounts, or between two accounts that are both not available, does not change Verplanbar — because no money leaves or enters the available money.
R4 (must): The month list, the month and the household plan show the same Verplanbar — because spec 93 R2 stays.

## Constraints

- No database change.

## Open questions

None.
