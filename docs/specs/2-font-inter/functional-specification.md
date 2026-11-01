# Functional specification: Schrift Inter lokal einbinden und Geist ablösen

Format: 2

## Summary

Die Schrift der gesamten Anwendung wechselt von Geist auf Inter. Die Dateien liegen lokal, die Lizenz wird mitgeliefert.

## Customer requirements

Siehe [customer-requirements.md](customer-requirements.md), Issue #2 in tom-schorn/duofy-dotnet. Abgedeckt sind alle Anforderungen R1 bis R4.

## Components

None.

## Solution

Inter wird als Variable-Schrift in den statischen Dateien der gemeinsamen Seiten abgelegt und über die vorhandene Schriftangabe der Anwendung eingebunden. Geist und seine Dateien entfallen. Beträge erhalten die Einstellung für tabellarische Ziffern.

## Behaviour

- R1: Jede Seite, auch Fehler- und Anmeldeseiten, zeigt Inter; fehlt die Schriftdatei, fällt der Browser auf die Systemschrift zurück, die Seite bleibt lesbar.
- R2: Beim Laden einer Seite entsteht keine Netzwerkanfrage an einen anderen Host als den eigenen Server.
- R3: Der Lizenztext liegt neben den Schriftdateien, die Doku nennt Quelle und Version.
- R4: Ziffern von Beträgen haben gleiche Breite.

## Data and interfaces

Neue Dateien: Inter als Variable-Schrift (WOFF2), Lizenztext. Entfernt: Geist-Dateien und deren Lizenz. Keine Schnittstellen, keine Daten.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given eine beliebige Seite der Web-Version, when sie geladen ist, then ist die berechnete Schriftfamilie des Textes Inter.
- [ ] A2 (R2): Given die Startseite wird in einem Browser-Test geladen, when die Netzwerkanfragen aufgezeichnet werden, then gehen alle Anfragen an den eigenen Host.
- [ ] A3 (R3): Given das Repository, when nach dem Lizenztext von Inter gesucht wird, then liegt er neben den Schriftdateien und die Doku nennt Quelle und Version.
- [ ] A4 (R4): Given ein Betrag in einer Liste, when er angezeigt wird, then hat sein Text die Eigenschaft tabellarische Ziffern.
- [ ] A5 (R1): Given das Repository, when nach Geist-Schriftdateien gesucht wird, then gibt es keine mehr.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/FontInterTests.cs, Tests/Duofy.Web.Tests/FontServingTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/FontInterTests.cs, Tests/Duofy.Web.Tests/FontServingTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/FontInterTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/FontInterTests.cs |
| R1 | A5 | Tests/Duofy.Shared.Tests/FontInterTests.cs |

## Non-goals

- Neue Farben, Formen oder Layouts (Aufgabe #3 und folgende).
- Eine zweite Schrift für Überschriften.
- Schriftgrößen-Skala (gehört zum Theme in #3).

## Risks

Fehlende Schriftdatei auf dem Gerät: Rückfall auf Systemschrift. Android-WebView lädt WOFF2 lokal: im Test auf einem Gerät prüfen.

## Open questions

None.

## Approval

Approved by the human on 2026-10-29

Freigegeben im Kickoff vom 29.10.2026.
