# Customer requirements: Bookings

Format: 2

## Background

In the original Duofy a booking (transaction) records what really happened with money: expenses, income and transfers between own accounts, each with a pot of the 50/30/20 rule and a category, shown in the month book of the plan. The .NET rebuild has a domain class `Booking` with name, payment date and a signed amount. On 2026-10-21 the human went through the original's rules one at a time. Together with the accounts (spec 58, built by the human) bookings make the first usable app. The team builds this feature.

## Goal

A person can record expenses, income and transfers on their accounts, see them month by month and see the account balances change accordingly, in the browser and in the app.

## Users

Every Duofy user, several times a day on the phone (quick entry right after paying) and now and then on the computer to look through a month.

## Requirements

R1 (must): The amount of a booking is always positive; whether money comes in or goes out follows from its kind, and Duofy computes the sign — because nobody should have to think of a minus sign while typing.
R2 (must): Every booking except a pure transfer belongs to one of four pots: income, needs (Grundbedarf), wants (Wünsche), savings — because the 50/30/20 evaluation of the plan needs it from the first booking on.
R3 (must): Every booking except a transfer has a category; everybody starts with the original's list, grouped, where each category suggests a pot that the person can change — because the category says what the money was for.
R4 (must): A person can add own categories, each with a pot, as a transition until a fixed schema exists — because the original's list misses entries (issue 40), and the own categories show what the fixed schema needs.
R5 (must): A transfer moves money between two different own accounts; it has no category, normally no pot, optionally the pot savings, which makes it count for the savings target; a cash withdrawal is a transfer to the cash account — because money that stays in the household is no expense, but saving must still be visible.
R6 (must): A booking's date must lie after the opening date of every account it touches and not after today — because bookings on or before the opening date are contained in the opening balance, and what is still to come belongs to the plan.
R7 (must): Bookings can be changed and deleted at any time; deleting asks for confirmation first — because mistakes must be fixable, and a slip on the phone must not delete anything.
R8 (must): A page shows the bookings of one month, newest first, with arrows to the previous and next month, a filter for one or all accounts and the month's sums of income and expenses — because a month is the unit Duofy plans in.
R9 (must): A large plus button (on the phone at the bottom right) opens a quick entry with the default account preselected — because most bookings are entered right after paying.
R10 (must): The balances of the accounts follow the bookings — because the balance is computed from them (spec 58).
R11 (must): A person sees and changes only their own bookings and categories — because money data is private.
R13 (must): An expense or income can be split into several lines, each with its own amount, category and pot; the lines add up to the booking's amount — because one receipt often covers several purposes, for example groceries and drugstore (decided 2026-10-21 instead of separate budgets).
R12 (wont): Plan month, link to plan positions, carry-over, counterparty from statements, import — because they come with the plan and import features.

## Constraints

- Builds on the API foundation (spec 57) and on the human's accounts (spec 58): bookings reference the human's account table and must not change it.
- The original's category list is in `backend/app/models/enums.py` (class `Category`) of github.com/tom-schorn/Duofy; German names come from `frontend/src/locales/de.json`.
- "Today" is the date in Europe/Berlin.

## Open questions

None.

## Amendments

- 2026-10-21: R13 (split bookings) added after the approval, decided by the human in the dialog while discussing budgets: there are no separate budgets; plan positions will get one or more booking lines assigned (plan feature), and bookings can be split.
