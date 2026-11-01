# Plan of Release 1.0

<!-- Quelle: Rundgang von Tom am 29.10.2026, Liste im Merkzettel project-release-1-0-scope-1029. Reihenfolge Design zuerst. -->

Ein Eintrag je Slice. Ziel sind Teile unter etwa 300 geänderten Zeilen. Wer die Aufgabe baut, ist ein Vorschlag und gehört dem Lead.

## Tasks

| id | what | depends on | who |
|---|---|---|---|
| F1 | Schrift Inter lokal einbinden und Geist ablösen | - | mia |
| F2 | Theme mit Palette Formensprache und Linien aus dem Design-System | F1 | kai |
| F3 | Dunkelmodus als eigenes Theme | F2 | kai |
| F4 | Umschalter Hell Dunkel Gerät und gespeicherte Wahl | F3 | mia |
| F5 | Bausteine Farbblock Linientabelle eckige Knöpfe | F2 | mia |
| F6 | Tabler-Symbole als Baustein mit Lizenzdatei | F2 | mia |
| F7 | Logo und App-Symbole einbauen Favicon Android Store | F2 | mia |
| F8 | Responsives Gerüst mit Breakpoints für Handy Tablet Desktop | F5 | kai |
| F9 | Seitennavigation für den Desktop | F8 | mia |
| F10 | Dialog-Gerüst mit Schritten Fortschritt und fester Weiter-Position | F5 | kai |
| F11 | Diagramm-Baustein mit Farben und Leerzustand | F5 | mia |
| B1 | Standardkonto beim Registrieren sprachabhängig | - | mia |
| B2 | Symbole für Kontoarten Standard und anpassbar | F6 | kai |
| B3 | Zahlart als Symbol-Buttons | F5, F6 | mia |
| B4 | Konten gezahlt von und zu mit Symbolen | B2 | mia |
| B5 | Vertrag anlegen Schritt 1 Was | F10 | kai |
| B6 | Vertrag anlegen Schritt 2 Wann | B5 | kai |
| B7 | Vertrag anlegen Schritt 3 Wer und Wohin | B6, B3, B4 | kai |
| B8 | Einnahmen als eigene Art im Vertragsdialog auffindbar | B5 | mia |
| B9 | Buchung anlegen im neuen Dialog-Layout | F10, B3, B4 | mia |
| B10 | Dialoge für Konto Haushalt und Plan-Posten neu gestalten | F10 | mia |
| P1 | Plan-Übersicht mit Monatsauswahl | F5, F8 | kai |
| P2 | Monatsplan neu mit Blöcken Einnahmen Fest und Frei | F5, F8 | kai |
| P3 | Soll-Ist-Balken mit Prozent im Monatsplan | P2 | mia |
| P4 | Monatsende-Auswertung mit Diagrammen | P2, F11 | mia |
| P5 | Richtwert-Prüfung je Person im Backend | - | kai |
| P6 | Gemeinsame und eigene Sicht nebeneinander mit Richtwerten | P2, P5 | kai |
| S1 | Menüpunkt Sparen mit Gerüst und Rechten | F8 | mia |
| S2 | Sparziele mit Stand je Ziel | S1 | kai |
| S3 | Schulden und Kredite als eigener Bereich | S1 | kai |
| S4 | Sparen im Vertragsdialog mit Sparziel verknüpfen | S2, B7 | mia |
| H1 | Startseite mit Blöcken und Begrüßung | P2 | mia |
| H2 | Startseite mit schnellen Infos zu Anstehendem und Soll-Ist | H1, P3 | mia |
| C1 | Einwilligungs-Baustein in der App | F5 | mia |
| C2 | Crisp-Chat einbinden nach Einwilligung und per Einstellung | C1, F7 | mia |
| C3 | Datenschutzerklärung und Verträge zu Crisp anpassen | C2 | kai |
| O1 | Ursache der Meldung Rejoining Server klären | - | kai |
| W1 | Microsoft-Store-App Machbarkeit und Entscheidung | F8 | kai |
| W2 | Microsoft-Store-App bauen und einreichen | W1, F7 | kai |
| A1 | Barrierefreiheit prüfen Kontrast Fokus Screenreader | P2, B7, H1 | rea |

## Chart

```mermaid
gantt
  title Plan of Release 1.0
  dateFormat YYYY-MM-DD
  section Fundament Design
  Schrift Inter lokal einbinden und Geist ablösen :f1, 2026-11-02, 1d
  Theme mit Palette Formensprache und Linien aus dem Design-System :f2, after f1, 1d
  Dunkelmodus als eigenes Theme :f3, after f2, 1d
  Umschalter Hell Dunkel Gerät und gespeicherte Wahl :f4, after f3, 1d
  Bausteine Farbblock Linientabelle eckige Knöpfe :f5, after f2, 1d
  Tabler-Symbole als Baustein mit Lizenzdatei :f6, after f2, 1d
  Logo und App-Symbole einbauen Favicon Android Store :f7, after f2, 1d
  Responsives Gerüst mit Breakpoints für Handy Tablet Desktop :f8, after f5, 1d
  Seitennavigation für den Desktop :f9, after f8, 1d
  Dialog-Gerüst mit Schritten Fortschritt und fester Weiter-Position :f10, after f5, 1d
  Diagramm-Baustein mit Farben und Leerzustand :f11, after f5, 1d
  section Bedienung
  Standardkonto beim Registrieren sprachabhängig :b1, 2026-11-02, 1d
  Symbole für Kontoarten Standard und anpassbar :b2, after f6, 1d
  Zahlart als Symbol-Buttons :b3, after f5 f6, 1d
  Konten gezahlt von und zu mit Symbolen :b4, after b2, 1d
  Vertrag anlegen Schritt 1 Was :b5, after f10, 1d
  Vertrag anlegen Schritt 2 Wann :b6, after b5, 1d
  Vertrag anlegen Schritt 3 Wer und Wohin :b7, after b6 b3 b4, 1d
  Einnahmen als eigene Art im Vertragsdialog auffindbar :b8, after b5, 1d
  Buchung anlegen im neuen Dialog-Layout :b9, after f10 b3 b4, 1d
  Dialoge für Konto Haushalt und Plan-Posten neu gestalten :b10, after f10, 1d
  section Plan und Auswertung
  Plan-Übersicht mit Monatsauswahl :p1, after f5 f8, 1d
  Monatsplan neu mit Blöcken Einnahmen Fest und Frei :p2, after f5 f8, 1d
  Soll-Ist-Balken mit Prozent im Monatsplan :p3, after p2, 1d
  Monatsende-Auswertung mit Diagrammen :p4, after p2 f11, 1d
  Richtwert-Prüfung je Person im Backend :p5, 2026-11-02, 1d
  Gemeinsame und eigene Sicht nebeneinander mit Richtwerten :p6, after p2 p5, 1d
  section Sparen
  Menüpunkt Sparen mit Gerüst und Rechten :s1, after f8, 1d
  Sparziele mit Stand je Ziel :s2, after s1, 1d
  Schulden und Kredite als eigener Bereich :s3, after s1, 1d
  Sparen im Vertragsdialog mit Sparziel verknüpfen :s4, after s2 b7, 1d
  section Startseite
  Startseite mit Blöcken und Begrüßung :h1, after p2, 1d
  Startseite mit schnellen Infos zu Anstehendem und Soll-Ist :h2, after h1 p3, 1d
  section Support und Datenschutz
  Einwilligungs-Baustein in der App :c1, after f5, 1d
  Crisp-Chat einbinden nach Einwilligung und per Einstellung :c2, after c1 f7, 1d
  Datenschutzerklärung und Verträge zu Crisp anpassen :c3, after c2, 1d
  section Betrieb
  Ursache der Meldung Rejoining Server klären :o1, 2026-11-02, 1d
  section Plattform
  Microsoft-Store-App Machbarkeit und Entscheidung :w1, after f8, 1d
  Microsoft-Store-App bauen und einreichen :w2, after w1 f7, 1d
  section Prüfung
  Barrierefreiheit prüfen Kontrast Fokus Screenreader :a1, after p2 b7 h1, 1d
```
