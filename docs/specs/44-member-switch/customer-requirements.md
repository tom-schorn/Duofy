# Customer requirements: Personen- und Haushaltsumschalter immer sichtbar

Format: 2

## Background

Der Umschalter für die Personen (wessen Zahlen sind offen) und der Haushalt-Eintrag erscheinen heute nur, wenn jemand etwas mit der Person teilt oder der Haushalt mindestens zwei Mitglieder hat. Wer allein ist, sieht beides nicht und erfährt nicht, dass es sie gibt (Tom, 29.10.2026: der Umschalter und der Knopf Haushalt fehlen).

## Goal

Der Umschalter und ein eigener Haushalt-Knopf sind für angemeldete Personen immer sichtbar und erklären, was sie brauchen, wenn sie noch nicht nutzbar sind.

## Users

Alle angemeldeten Personen, vor allem die, die noch allein sind.

## Requirements

R1 (must): Der Umschalter für Personen ist für jede angemeldete Person sichtbar, auch wenn sie allein ist und niemand etwas mit ihr teilt, und zeigt dann nur „Ich“ — because man sehen soll, dass es ihn gibt.
R2 (must): Ein eigener Knopf „Haushalt“ ist immer sichtbar: Hat die Person einen Haushalt mit mindestens zwei Mitgliedern, schaltet er auf die Haushaltssicht; sonst ist er als nicht verfügbar gekennzeichnet und erklärt, wie man einen Haushalt gründet oder beitritt, mit Link zur Haushalt-Seite — because man den Weg dorthin finden soll.
R3 (must): Personen, die ihre Zahlen mit der angemeldeten Person teilen, erscheinen im Umschalter mit Vornamen, wie bisher nach dem Berechtigungssystem — because die Freigaben der Personen gelten.
R4 (must): Die Wahl bleibt beim Wechseln der Seiten erhalten und die aktive Sicht ist auf jeder Seite erkennbar — because man wissen muss, wessen Zahlen gerade offen sind.
R5 (must): Der Umschalter ist ein Profil-Knopf: im Web unten in der Seitenleiste (ab Desktop) und im Layout darunter an der Stelle des Profils, er öffnet ein Menü mit Ich, den Personen mit Freigabe, dem Haushalt (mit Erklärung, wenn nicht verfügbar), dem Konto und Abmelden; dieselbe Komponente wird in der App wiederverwendet — because ein Baustein für Web und App gelten soll (Tom, 29.10.2026, 12:38).
R6 (must): Ohne Freigabe sieht niemand Daten, die nicht freigegeben sind; der Umschalter ändert keine Berechtigungen — because die Rechte unverändert bleiben müssen.

## Constraints

Das Berechtigungssystem (Spec 68) und die Haushaltssicht (Spec 69) bleiben unverändert. Baut auf dem Layout (#9, #10). Die Haushaltsseite selbst bleibt wie sie ist.

## Open questions

None.
