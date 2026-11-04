# Customer requirements: Push reminders for due positions with tick-off from the notification

Format: 2

## Background

Duofy plans positions with a due day and a payment method (Lastschrift, Dauerauftrag, Überweisung, Abhebung, Sonstige). Ticking a position off happens only inside the app; nothing reminds a person that a manual payment is due today. Competitors such as Monarch Money remind of bills, but marking them paid happens only in the app, not from the notification. The Android app and the Windows app (#40) are the clients; the Windows app goes to the Microsoft Store first.

The human, 2026-11-04: "zu fragen wenn heute rechnung x ansteht das eine entsprechende benachrichtigung abfragt". On the scope: "benachrichtigung würde ich aber auf die posten beschränken die ein manuelles dazutun erfordern zb barzahlung der überweisung oder so". On recipients: "einstellbar durch berechtigung würde ich sagen. so kann person b mit handeln wenn person a hilfe braucht". On the text: "wen mit vernünftigen text. wenn es machbar ist mit datenschutz und avv".

## Goal

On the morning of the due day, a person gets a readable push notification for each position they must pay by hand, can tick it off with one tap or be reminded tomorrow, and a household member with the right permission can step in.

## Users

People of the hosted Plagevo with the Android or Windows app, on every day a manual payment is due; household members who help someone else.

## Requirements

R1 (must): A reminder comes as a push notification to the Android app and to the Windows app — because the human chose push to the phone and then "Android und Windows".
R2 (must): It comes on the due day at a time each person sets; each person can switch reminders off — because "uhrzeit einstellbar".
R3 (must): Only positions with payment method Überweisung, Abhebung or Sonstige cause a reminder; Lastschrift and Dauerauftrag never do — because only positions that need a manual action should remind.
R4 (must): The notification shows a readable text such as "GEZ ist heute fällig", while the push provider receives no position name, amount or other financial data — because the human wants a readable text only if it is feasible with data protection and a processing agreement.
R5 (must): The notification has the buttons "Bezahlt" (ticks the position with the planned amount) and "Später" (reminds again tomorrow); tapping the notification opens the tick dialog of that position — because the human chose "Bezahlt und Später".
R6 (must): The owner of the position gets the reminder, and so does every household member to whom the owner granted the reminder permission — because "so kann person b mit handeln wenn person a hilfe braucht".
R7 (must): Once the position is ticked by anyone, no further reminder for it comes, and open notifications for it disappear on the other devices — because the reminder is about one payment.
R8 (must): Reminders exist only on the hosted version; a self-hosted Duofy hides the settings for them — because the human chose "Nur Plagevo".
R9 (should): The data protection notice names the push providers and their processing agreements — because the human made the agreement a condition.

## Constraints

- Push providers: Firebase Cloud Messaging for Android, Windows Push Notification Services for Windows; both free and with a processing agreement (Google's Firebase data processing terms, Microsoft's products and services data protection addendum). Signing those agreements is the human's step.
- New tables or columns need a migration that only the human rolls out.
- Latest patched package versions, pinned; free, usable commercially.

## Open questions

None.
