# Functional specification: Oberfläche von Grund auf neu mit Blazor Blueprint

Format: 2

## Summary

Die gesamte Oberfläche wird in einem Zug neu gebaut, mit Blazor Blueprint und dem Aussehen des alten Duofy. Radzen fliegt ganz raus. Backend, API und Datenbank bleiben unverändert. Online geht erst das Ganze.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 bis R10.

## Components

None.

## Solution

Referenz ist das alte Duofy, lokal unter `http://localhost:8088`, Code in `D:\Projekte Git\Duofy-alt\frontend`, Testzugang in `lokal-zugang.txt` dort. Der Versuch in `D:\Projekte Git\Duofy-spike-blueprint` und `Duofy-spike-app` zeigt die Technik.

`BlazorBlueprint.Components` und `BlazorBlueprint.Primitives` kommen in fester Version nach `Duofy.Shared`. Eine Theme-Datei hält alle Werte aus `frontend/src/index.css` als CSS-Variablen, hell und dunkel, dazu Schriften und Rundung. Gemeinsame Bausteine in `Duofy.Shared` bauen die Seiten auf; `Radzen.Blazor`, `RadzenVariables`, `ThemeBoot` und die alte `app.css` werden entfernt. Reihenfolge: Theme, Gerüst und Bausteine, dann alle Seiten, zuletzt das Entfernen. Die Arbeit läuft auf einem einzigen Zweig und wird erst nach Toms Ja nach `main` geführt.

Ergebnisse der Vorprüfung vom 02.11.2026: Die Meldung „An unhandled error has occurred“ im Versuch war kein Fehler, die App-Vorlage zeigt sie, wenn das CSS sie nicht ausblendet. Haken, Monat, Scrollen und Dialog mit Tastatur liefen auf dem Handy. Ein Klick ohne Daten brauchte im Portal 51 bis 364 ms, meist um 60 ms. Der Server braucht zur Datenbank zu Hause etwa 68 ms je Abfrage; das ist eine eigene Aufgabe.

## Behaviour

- Layout: Am PC feste Seitenleiste wie im Original mit Personen-Umschalter. Am Handy die beschlossene untere Leiste Start, Plan, Haushalt, Mehr. Buchungen nur im Monat, Bewegungen am Konto.
- Planübersicht: Monatskarten mit Verplanbar, Anstehend und drei Topf-Balken. „Verplanbar“ ersetzt das Wort „Frei“ für denselben Wert der API. Anstehend ergibt sich aus den nicht abgehakten Posten.
- Monat: drei schlichte Kennzahlen, Reiter wie im Original, Sankey aus den Posten des Monats, Posten nach Töpfen mit Balken und Kästchen, Zeile Ungeplant aus Buchungen ohne Posten.
- Haken: Ein Dialog zeigt Datum und Betrag vorbelegt und bucht erst nach Bestätigung; die API kann das bereits. Hat ein Posten schon Buchungen, entsteht keine weitere.
- Knöpfe in normaler Schreibweise, keine Farbblöcke, Linien statt Schatten, Serifen-Überschriften und Zahlen in ausgerichteter Spalte wie im Original.
- Alle übrigen Seiten übernehmen ihre heutigen Funktionen in diesem Aussehen: Anmeldung, Registrierung, Passwort, E-Mail-Bestätigung, Einladung, Start, Konten, Verträge, Sparen, Haushalt, Freigaben, Haushaltsplan, Konto, Verwaltung, Lizenzen, Rechtliches, Mehr, Fehlerseiten.
- App: Statusleiste überdeckt nie Inhalt, auch nicht beim Scrollen. `#blazor-error-ui` ist nur im echten Fehlerfall sichtbar.
- Seiten, die wegen `HttpContext` statisch rendern müssen, bleiben statisch und nutzen dann Bausteine ohne Interaktivität.

### Änderung nach Toms Durchsicht vom 03.11.2026

Toms Liste steht wörtlich in `.workday/kai-82-hilfen/tom-befunde-2026-11-03.md`, die Befunde des Leads in `.workday/kai-82-hilfen/lead-befunde.md`. Wo diese Änderung den Punkten oben widerspricht, gilt sie.

- Titel: Jede Seite nennt ihren Titel einmal. Die Kopfzeile zeigt keinen zweiten Titel derselben Seite.
- Person: Die Auswahl der Person (Ich, Partner, Haushalt) sitzt unten im Profil-Menü der Seitenleiste, am Handy im Profil unter Mehr. Oben gibt es keinen Umschalter mehr.
- Menü am PC: Start, Meine Planung, Unser Haushalt, Verträge, Konten, Sparen. „Unser Haushalt“ trägt den Namen des Haushalts und enthält Haushaltsplan, Mitglieder, Freigaben und Richtwerte. Der Eintrag „Mitglieder“ entfällt.
- Handy: untere Leiste Start, Plan, Haushalt, Sparen, Mehr. Mehr enthält keinen Haushalt-Eintrag. Jede Seite bekommt eine eigene Handy-Gestaltung: Kennzahlen kompakt in einer Zeile oder als waagerecht wischbare Reihe, Aktionen als ein Hauptknopf plus Menü, Inhalt beginnt im ersten Bildschirm.
- Hell und Dunkel: ein einziger Knopf, der zwischen hell und dunkel wechselt; ohne Wahl gilt die Einstellung des Geräts.
- Erklärspalte: Am PC ab 1280 Pixel Breite rechts eine Spalte mit kurzen Erklärungen zur Seite wie im alten Duofy. Die Texte beschreiben nur Regeln, die es bei uns gibt, und werden Tom vor dem Einbau vorgelegt. Darunter wird sie ausgeblendet.
- Monat, Reiter Plan: vier gleich große Kennzahl-Kästen in einer Reihe, kein Monatsumschalter oben rechts, kein Sankey. Die Buchungsanzahl eines Postens steht in seiner Zeile; aufgeklappt zeigen sich die Buchungen in voller Breite.
- Monat, Reiter Buchungen: Liste in voller Breite mit Datum, Name, Posten, Konto, Topf und Betrag; Ausgaben rot mit Minus, Einnahmen ohne Vorzeichen.
- Monat, Reiter Auswertung: kein Sankey (Tom 03.11.2026: "eigentlich kanns weg"); je ein rundes Diagramm Soll gegen Ist für die drei Töpfe und für die Kategorien; eine Tageskurve des Monats, bis heute aus den echten Buchungen, ab morgen aus den offenen Posten an ihrem Fälligkeitstag, mit markierter Stelle, an der sie unter null fällt, und Datum und Betrag der Lücke.
- Haushaltsplan: eine Übersicht der Haushaltsmonate wie bei den eigenen Plänen; im Monat sind die Posten nach Haushaltsmitglied gruppiert.
- Beträge: Ein Topf zeigt überall denselben Betrag. Ungeplant ist eine eigene Zeile außerhalb der Töpfe. „Ist“ in Soll und Ist bezieht sich auf die geplanten Einnahmen des Monats, solange keine gebucht sind.
- Schrift: Beträge und Texte in derselben Schrift mit gleich breiten Ziffern; Serifen nur für Überschriften.
- Tablet (768 bis 1279 Pixel, hochkant und quer): Die Seitenleiste ist eingeklappt auf Symbole mit Beschriftung beim Darüberfahren und lässt sich ausklappen; keine Erklärspalte; Inhalte stehen zweispaltig, wo es passt, zum Beispiel Diagramme der Auswertung und Monatskarten.
- Druck: Ein eigenes Druckbild für A4 hochkant, schwarz-weiß lesbar. Kopf mit Person oder Haushalt und Monat, eine Zeile Kennzahlen, dann je Topf eine Tabelle mit Posten, Fälligkeit, geplantem und gebuchtem Betrag und Status, am Ende Ungeplant und Summen. Ohne Navigation, Knöpfe, Sankey und Erklärspalte; Tabellen brechen nicht mitten in einer Zeile um, Seitenzahlen im Fuß.
- Rückmeldungen wie „Der Posten ist abgehakt.“ erscheinen als kurze Meldung, die sich selbst schließt und nichts verschiebt.

## Data and interfaces

Keine Änderung an API und Datenbank. `Duofy.Schemas` und das Backend bleiben unberührt.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given der fertige Zweig, when alle Projektdateien und Paketlisten durchsucht werden, then gibt es keinen Treffer für `Radzen` außer in Doku und Historie.
- [ ] A2 (R2): Given die Theme-Datei, when ein Test sie liest, then entsprechen `--background`, `--primary`, `--border`, `--radius` und die Topf-Farben hell und dunkel den Werten des alten Duofy.
- [ ] A3 (R2): Given Razor- und CSS-Dateien der Seiten, when ein Test sie durchsucht, then stehen feste Farbwerte nur in der Theme-Datei.
- [ ] A4 (R3): Given Portal und Android-App, when beide als Release gebaut und gestartet werden, then öffnet die App die Planübersicht aus denselben Dateien in `Duofy.Shared` ohne Ausnahme im Log.
- [ ] A5 (R4): Given jede Seite und Breiten von 360, 768 und 1440 Pixeln, when sie angemeldet mit Testdaten geöffnet wird, then ist die Seitenbreite nicht größer als das Fenster.
- [ ] A6 (R5): Given der fertige Zweig, when er mit `main` verglichen wird, then sind `Backend` und die Migrationen unverändert.
- [ ] A7 (R5): Given die Liste der Routen auf `main`, when der fertige Zweig geprüft wird, then ist jede Route erreichbar oder führt wie beschlossen weiter.
- [ ] A8 (R6): Given der fertige Zweig, when `dotnet test Duofy.slnx` läuft, then sind alle Tests grün und entfernte Tests sind im Bericht einzeln mit ihrem Ersatz genannt.
- [ ] A9 (R7): Given der Abschluss, when berichtet wird, then ist nichts nach `main`, GitHub oder auf den Server gelangt.
- [ ] A10 (R8): Given Testdaten, when ein Monat geöffnet wird, then zeigt er Kennzahlen, Zeile Ungeplant und nach Töpfen geordnete Posten mit den Beträgen der API.
- [ ] A11 (R8): Given ein offener Posten, when sein Kästchen gewählt wird, then erscheint ein Dialog mit Datum und Betrag, und nach Bestätigung entsteht genau eine Buchung mit diesen Werten.
- [ ] A12 (R9): Given ein Dialog, when er geöffnet und Esc gedrückt wird, then liegt der Fokus zuerst im Dialog, der Dialog schließt und der Fokus kehrt zum Auslöser zurück.
- [ ] A13 (R11): Given jede angemeldete Seite bei 1440 Pixeln, when ihr Text gelesen wird, then kommt ihr Titel genau einmal vor, es gibt keinen Personen-Umschalter in der Kopfzeile, keinen Menüeintrag „Mitglieder“ und genau einen Knopf für Hell und Dunkel.
- [ ] A14 (R12): Given jede angemeldete Seite bei 390 mal 844 Pixeln, when sie geöffnet wird, then hat die untere Leiste genau Start, Plan, Haushalt, Sparen, Mehr, Mehr enthält keinen Haushalt-Eintrag, und im Monat ist der erste Posten ohne Scrollen sichtbar.
- [ ] A15 (R13): Given eine angemeldete Seite bei 1440 Pixeln, when sie geöffnet wird, then ist die Erklärspalte sichtbar, und bei 1024 Pixeln ist sie es nicht.
- [ ] A16 (R14): Given Testdaten mit einer offenen Ausgabe, die den Stand unter null drückt, when der Reiter Auswertung geöffnet wird, then zeigt er keinen Sankey, aber die runden Diagramme für Töpfe und Kategorien und die Tageskurve mit Datum und Betrag der Lücke.
- [ ] A17 (R15): Given der Reiter Buchungen mit Testdaten, when er geöffnet wird, then steht jede Ausgabe mit Minus und in der Farbe für Ausgaben, und die Kennzahl-Kästen im Reiter Plan sind gleich hoch und gleich breit.
- [ ] A18 (R15): Given die CSS- und Razor-Dateien, when ein Test sie durchsucht, then gibt es außer der Serifen-Schrift für Überschriften genau eine Schriftfamilie.
- [ ] A19 (R16): Given ein Haushalt mit zwei Mitgliedern, when der Haushaltsplan geöffnet wird, then führt er zu einer Liste der Haushaltsmonate, und im Monat stehen die Posten unter dem Namen ihres Mitglieds.
- [ ] A20 (R17): Given die Testdaten vom 03.11.2026, when Übersicht, Monat und Auswertung gelesen werden, then zeigt jeder Topf überall denselben Betrag, und Ist ist bei ungebuchten Einnahmen nicht überall 0 %.
- [ ] A21 (R19): Given jede angemeldete Seite bei 768 mal 1024 und 1024 mal 768 Pixeln, when sie geöffnet wird, then gibt es keinen Überstand, die Seitenleiste ist eingeklappt und die Erklärspalte ist nicht sichtbar.
- [ ] A22 (R20): Given ein Monat mit Testdaten, when er im Druckmodus als A4 gerendert wird, then enthält das Druckbild Kopf, Kennzahlen und je Topf eine Tabelle mit allen Posten, aber keine Navigation, keinen Knopf und keinen Sankey.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/InterfaceRulesTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/ThemeFileTests.cs |
| R2 | A3 | Tests/Duofy.Shared.Tests/InterfaceRulesTests.cs |
| R3 | A4 | docs/specs/82-blueprint-foundation/acceptance-evidence.md |
| R4 | A5 | docs/specs/82-blueprint-foundation/acceptance-evidence.md |
| R5 | A6 | docs/specs/82-blueprint-foundation/acceptance-evidence.md |
| R5 | A7 | Tests/Duofy.Shared.Tests/InterfaceRulesTests.cs |
| R6 | A8 | docs/specs/82-blueprint-foundation/removed-tests.md |
| R7 | A9 | docs/specs/82-blueprint-foundation/acceptance-evidence.md |
| R8 | A10 | Tests/Duofy.Shared.Tests/MonthPageTests.cs |
| R8 | A11 | Tests/Duofy.Shared.Tests/MonthPageTests.cs |
| R9 | A12 | Tests/Duofy.Shared.Tests/InterfaceBehaviourTests.cs |
| R11 | A13 | Tests/Duofy.Shared.Tests/AppLayoutTests.cs |
| R12 | A14 | Tests/Duofy.Shared.Tests/SideNavigationTests.cs |
| R13 | A15 | Tests/Duofy.Shared.Tests/HelpColumnTests.cs |
| R14 | A16 | Tests/Duofy.Shared.Tests/MonthReviewTests.cs |
| R15 | A17 | Tests/Duofy.Shared.Tests/MonthPageTests.cs |
| R15 | A18 | Tests/Duofy.Shared.Tests/InterfaceRulesTests.cs |
| R16 | A19 | Tests/Duofy.Shared.Tests/HouseholdPlanPageTests.cs |
| R17 | A20 | Tests/Duofy.Shared.Tests/MonthViewTests.cs |
| R19 | A21 | Tests/Duofy.Shared.Tests/InterfaceRulesTests.cs |
| R20 | A22 | Tests/Duofy.Shared.Tests/MonthPrintTests.cs |

## Non-goals

- Backend-Änderungen, also auch Limit-Posten, Verlauf mit Tiefpunkt, private Posten, Monate nur auf Wunsch und der CAMT-Import.
- Name, Logo und weitere Einstellungen der Instanz über eine Einstellungsseite; das wird eine eigene Aufgabe mit Backend-Änderung direkt nach diesem Umbau (R18).
- Datenbankabfragen senken oder die Datenbank umziehen.
- Windows-App und Store.
- Datenkorrekturen im Echtsystem.

## Risks

Ein langer Zweig mit vielen Dateien: Es läuft nichts anderes parallel, und jeder Abschnitt wird committet. Rund 1.600 Tests prüfen die Oberfläche und müssen mit; `data-testid` bleibt erhalten, wo es geht. Blueprint hat im Wesentlichen einen Entwickler, die Apache-Lizenz erlaubt notfalls eigene Pflege. Seiten mit Daten bleiben langsam, solange jede Abfrage 68 ms braucht. Blueprint verlangt für Overlays interaktive Bereiche; statische Seiten brauchen eigene Lösungen. Speicher: nur eine Arbeitssitzung und Android-Builds nicht parallel zu Gesamttests.

## Open questions

None.

## Approval

Approved by the human on 2026-11-02

Tom im Dialog: "Alles auf einmal und das ui Thema von 0 anfangen. Du weißt wie es aussehen soll und gut ist."

Änderung nach Toms Durchsicht (R11 bis R20, A13 bis A22) freigegeben am 2026-11-03; Tom hat dazu Tablet und Druck ergänzt.

Zweite Änderung nach Toms Durchsicht am 2026-11-03: Tom wörtlich "sankey optimieren. bzw eigentlich kanns weg. die gruppierung zu namen muss irgendwie präsenter werden das geht unter. und das tagesdiagram is auch bissle zu groß. versuch alles bissle zu standartisieren. aber dann können wir es ausliefern." Der Sankey entfällt überall; A10 und A16 sind entsprechend angepasst.
