# Abnahme von Spec 82: Belege

Stand 02.11.2026, Zweig `feat/v2-ui`. Gemessen mit einer lokalen Wegwerf-Datenbank, nichts davon berührt ein echtes System.

## A4: Release-Build und Start

Portal: `dotnet publish -c Release`, gestartet in `Production` gegen die lokale API, geöffnet im Browser. Planübersicht und Monat erscheinen mit den Dateien aus `Duofy.Shared`, der Fehlerbalken bleibt verborgen, die Browser-Konsole ist leer. Das Log enthält eine Warnung: Die Abfrage `GET api/v1/plans/2026/11` wurde abgebrochen, als die Prüfung von der Planübersicht weiter navigierte, während die Karten noch luden. Keine Ausnahme der Stufe Fehler.

Android: `dotnet build Mobile/Duofy -f net10.0-android -c Release`: 0 Fehler, 0 Warnungen, 3 Minuten 32 Sekunden; APK und AAB signiert.

**Offen:** Die Android-App ist als Release gebaut, aber nicht gestartet. Es gibt keinen Emulator, und auf dem angeschlossenen Handy von Tom startet sie nur die Leitung. Der Teil "öffnet die Planübersicht ohne Ausnahme im Log" ist für die App deshalb nicht belegt.

## A5: Seitenbreite

Angemeldet mit Testdaten, jede Route von `main` außer den Entwicklerseiten, dazu Reiter und Dialoge des Monats, hell und dunkel, bei 360, 768 und 1440 Pixeln: 180 Messungen von `scrollWidth` gegen die Fensterbreite, alle ohne Überstand.

Nicht gemessen: `/bausteine`, `/diagramme`, `/schritt-dialog` (Entwicklerseiten), der Monat aus Sicht einer anderen Person, und die Haushaltssicht über den Umschalter.

## A6: Backend und Migrationen

`git diff main -- Backend` ist leer. Die Migrationen liegen unter `Backend`.

## A8: Tests

Zahlen je Projekt stehen im Bericht an die Leitung. Entfernte Tests stehen einzeln in [removed-tests.md](removed-tests.md).

## A9: Nichts nach außen

`feat/v2-ui` hat keinen Upstream und keinen Zweig auf dem Remote; es wurde nicht gepusht, nicht gemerged und nichts auf den Server gebracht.

## A11 und A12 im Browser

Im Monat, Posten Miete, ohne Buchungen. Kästchen geklickt: Fokus im Feld Datum innerhalb des Dialogs, Datum und Betrag vorbelegt (2026-11-02, 900,00). Esc: Dialog zu, Fokus zurück auf dem Kästchen, Kästchen leer. Erneut, Betrag 888,50, Abhaken: Kästchen gesetzt, Anzahl am Reiter Buchungen von 5 auf 6. Dialog Neuer Posten: Fokus im ersten Feld, Esc schließt, Fokus zurück auf dem Knopf.
