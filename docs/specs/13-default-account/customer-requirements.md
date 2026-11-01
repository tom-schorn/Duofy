# Customer requirements: Standardkonto beim Registrieren, sprachabhängig

Format: 2

## Background

Wer sich registriert, hat heute kein Konto und muss eines anlegen, bevor etwas gebucht werden kann. Tom wünscht ein automatisches erstes Konto namens Standardkonto, sprachabhängig übersetzt (Rundgang 29.10.2026).

## Goal

Eine neue Person bekommt bei der Registrierung ein erstes Konto, dessen Name zur Sprache der Person passt.

## Users

Neue Personen bei der Registrierung, einmalig.

## Requirements

R1 (must): Bei erfolgreicher Registrierung entsteht für die Person genau ein erstes Konto — because sie sofort buchen können soll.
R2 (must): Der Name wird über die Sprache nachgeschlagen: Deutsch „Standardkonto“, Englisch „Default account“. Die Registrierung nutzt vorerst immer Deutsch, weil die App weder eine Sprachauswahl noch englische Texte hat — because Englisch folgen soll, sobald die App eine Sprachauswahl bekommt, ohne die Registrierung erneut umzubauen.
R3 (must): Bestehende Personen erhalten kein zusätzliches Konto — because bestehende Daten unverändert bleiben müssen.
R4 (should): Ist die Sprache unbekannt, gilt Deutsch — because Deutsch die Standardsprache ist.

## Constraints

Das Konto folgt den vorhandenen Regeln für Konten. Der Name lässt sich später ändern wie bei jedem Konto.

## Open questions

None.
