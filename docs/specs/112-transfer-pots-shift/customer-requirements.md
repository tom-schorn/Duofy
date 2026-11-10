# Customer requirements: Transfers move money between pots

Format: 2

## Background

Since spec 72 a new transfer carries a source pot and a target pot, both required (needs, wants or savings, the same pot allowed). The pots are only remembered: a transfer changes no booked sum per pot and no quota actual. On 30.10.2026 the human objected to assumption A-008 (transfers without a pot); on 10.11.2026 the human made clear that the pots of a transfer should also change the sums.

## Goal

A transfer from one pot to another moves its amount: the source pot goes down, the target pot goes up, in the booked sum per pot and in the actual of the quota.

## Users

Everyone who moves money between their own accounts, for example 200 € from the current account to the savings account (needs to savings), and members of a household who look at the household month.

## Requirements

R1 (must): A transfer with a source pot and a different target pot lowers the booked sum of the source pot and raises the booked sum of the target pot by its amount in its plan month — because the human wants to see where the money went (10.11.2026).
R2 (must): The same transfer moves the actual of the quotas the same way — because the 50/30/20 comparison should show money put aside for savings (10.11.2026).
R3 (must): A transfer with the same source and target pot, and an older transfer without pots, changes no pot sum and no quota actual — because nothing moved between pots.
R4 (should): The household month adds up the moved amounts of the members the same way it adds up their other bookings, with the same visibility rules — because the household view must match the members' own months.
R5 (must): The free amount (Verplanbar), the fixed sum and the account balances stay as they are — because a transfer between own accounts neither earns nor spends money.
R7 (must): A transfer whose line sits on a plan position (for example the savings rate or a savings goal tick) shifts nothing between pots; only the position counts it — because otherwise the amount is counted twice (human, 10.11.2026, card P-064).
R6 (wont): Changing how ticking a position with a target account books its transfer — because it uses the position's pot on both sides and therefore moves nothing.

## Constraints

- The pots stay required for new transfers as today (spec 72); no change to the dialog.
- Changing or deleting a transfer changes the moved amounts accordingly.
- A source pot may go below zero in the booked sum; that is shown as it is.

## Open questions

None.
