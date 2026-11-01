# Customer requirements: Vertrag anlegen in drei Schritten

Format: 2

## Background

Der Dialog zum Anlegen eines Vertrags ist zu lang (Rundgang 29.10.2026). Tom hat drei Schritte beschlossen: Was, Wann, Wer und Wohin. Sparpläne und Kredite werden von den klassischen Verträgen getrennt und gehören nicht mehr in diesen Dialog (Tom, 29.10.2026). Das Dialog-Gerüst baut #11.

## Goal

Ein klassischer Vertrag (Ausgabe oder Einnahme) wird in drei kurzen Schritten angelegt oder bearbeitet.

## Users

Alle Personen, die einen wiederkehrenden Posten anlegen, gelegentlich.

## Requirements

R1 (must): Schritt 1 Was fragt Name, Art (Ausgabe oder Einnahme) und Betrag — because das die Pflichtangaben sind.
R2 (must): Schritt 2 Wann fragt Rhythmus, Start und Ende und zeigt den nächsten Fälligkeitstermin; ein Ende vor dem Start wird abgelehnt — because Termine das Plangerüst bilden.
R3 (must): Schritt 3 Wer und Wohin fragt die Konten (gezahlt von, gezahlt zu) und die Zahlart und zeigt vor dem Speichern eine Zusammenfassung — because man prüfen will, was entsteht.
R4 (must): Der Dialog bietet nur klassische Arten an (Ausgabe, Einnahme), Sparplan und Kredit nicht mehr — because sie in einen eigenen Bereich ausgelagert werden.
R5 (must): Bearbeiten eines Vertrags nutzt denselben Ablauf mit den bestehenden Werten — because ein Weg einfacher ist als zwei.
R6 (must): Der gespeicherte Vertrag entspricht den Eingaben und bestehende Verträge anderer Arten bleiben unverändert erhalten — because nichts verloren gehen darf.

## Constraints

Baut auf dem Dialog-Gerüst (#11). Die Zahlart und die Konten dürfen vorerst Auswahlfelder bleiben; die Symbol-Buttons folgen mit #15 und #16 und tauschen sie nur aus. Bestehende Verträge der Arten Sparziel und Schuld werden nicht gelöscht und nicht migriert.

## Open questions

None.
