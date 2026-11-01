# Microsoft Store: Paket bauen und einreichen

Diese Anleitung beschreibt, wie aus der App ein Paket für den Microsoft Store wird und was du im Partner Center einträgst.
Gebaut wird auf einem Windows-Rechner. Die Einreichung machst du selbst; im Repository liegen weder Zugangsdaten noch
Schlüssel. Die Signatur übernimmt der Store.

Was im Repository schon da ist:

| Datei | Wofür |
| --- | --- |
| `Mobile/Duofy/Platforms/Windows/Package.appxmanifest` | Das Paket-Manifest mit den Platzhaltern, die mit `ERSETZEN` beginnen |
| `Mobile/Duofy/Duofy.csproj` | Der Schalter `DuofyStorePackage`, der das Paket baut; ohne ihn bleibt der Testweg am PC unverpackt |
| `Mobile/Store/store-tile-512.png` und `.svg` | Die Store-Kachel (512 mal 512 Pixel) |
| `docs/store/beschreibung.md` | Der Beschreibungstext für die Store-Seite |
| `docs/store/datenschutz.md` | Der Datenschutzhinweis mit Verweis auf die Datenschutzerklärung |

## 1. Was du brauchst

- Ein Entwicklerkonto im Partner Center (https://partner.microsoft.com/dashboard). Einmalige Gebühr laut Microsoft; die
  Anmeldung bestätigt Microsoft mit einer Prüfung.
- Einen Windows-Rechner mit dem .NET-SDK 10 und dem MAUI-Arbeitsbereich (`dotnet workload install maui`).
- Eine Datenschutzerklärung im Internet. Beim gehosteten Plagevo ist das der Rechtstext des Servers (siehe
  `docs/store/datenschutz.md`).

## 2. Die Platzhalter ersetzen

Drei Werte vergibt dein Partner Center. Sie stehen im Manifest als `ERSETZEN`, damit niemand versehentlich ein Paket mit
erfundenen Werten einreicht. Das Paket lässt sich mit den Platzhaltern bauen (zum Testen), der Store nimmt es aber erst
mit den echten Werten an.

1. Im Partner Center unter Apps und Spiele eine neue App anlegen und den Namen reservieren. Der Name muss zu
   `ApplicationTitle` in `Mobile/Duofy/Duofy.csproj` passen (heute `Duofy`); wähle im Partner Center den Namen, den du
   zeigen willst, und stelle `ApplicationTitle` danach gleich.
2. Im Partner Center zur App gehen, links Produktverwaltung, Produktidentität. Dort stehen drei Werte:
   - `Package/Identity/Name`: kommt nach `Identity Name` im Manifest.
   - `Package/Identity/Publisher`: kommt nach `Identity Publisher` (beginnt mit `CN=`).
   - `Package/Properties/PublisherDisplayName`: kommt nach `PublisherDisplayName`.
3. Diese drei Werte in `Mobile/Duofy/Platforms/Windows/Package.appxmanifest` eintragen und die Datei speichern.
   Die Version (`Version="1.0.0.0"`) erhöhst du bei jeder neuen Einreichung; die letzte Stelle bleibt 0.

Die Werte sind keine Geheimnisse (sie stehen später im Paket), müssen aber genau stimmen. Nichts anderes aus dem Partner
Center gehört ins Repository.

## 3. Das Paket bauen

Im Hauptordner des Repositorys:

```powershell
dotnet publish Mobile/Duofy -f net10.0-windows10.0.19041.0 -c Release -p:DuofyStorePackage=true -p:RuntimeIdentifierOverride=win-x64
```

Das Ergebnis liegt in `Mobile/Duofy/AppPackages/`:

- `Duofy_1.0.0.0_x64.msixupload` ist die Datei für den Store (Modus `StoreUpload`).
- Der Ordner `Duofy_1.0.0.0_x64_Test` enthält eine Testversion desselben Pakets; sie ist nicht signiert und lässt sich
  ohne Signatur nicht einfach installieren. Zum Ausprobieren am PC nimmst du den unverpackten Weg (Abschnitt 6).

Es wird bewusst nur für x64 gebaut. Für ARM64 wiederholst du den Befehl mit `win-arm64`; das ist freiwillig.

## 4. Einreichen

1. Im Partner Center bei der App eine neue Einreichung beginnen.
2. Preise und Verfügbarkeit: Preis und Märkte wählen (laut Entscheidung kostenlos, sofern du nichts anderes willst).
3. Altersfreigabe: den Fragebogen beantworten (die App hat keine Inhalte für Erwachsene, keine Käufe und kein Chat).
4. Pakete: die Datei `Duofy_1.0.0.0_x64.msixupload` hochladen. Der Store prüft Identität und Herausgeber; stimmen die
   Werte aus Abschnitt 2 nicht, lehnt er das Paket hier mit einer Meldung ab.
5. Store-Einträge: für Deutsch den Text aus `docs/store/beschreibung.md` eintragen. Als App-Kachel (Store-Logo,
   1:1) `Mobile/Store/store-tile-512.png` hochladen. Screenshots musst du selbst machen: mindestens einer, die Seiten der
   App im Fenster (zum Beispiel Start, Plan und Buchungen). Zeige darin nur Beispielzahlen, keine echten Daten.
6. Datenschutzerklärung: die Adresse aus `docs/store/datenschutz.md` eintragen.
7. Hinweise für die Zertifizierung: schreibe kurz, wie die Prüferin die App erreicht (zum Beispiel ein Testkonto auf dem
   Server, auf den die App zeigt). Ohne Zugang kann die Prüfung scheitern.
8. Einreichen. Die Prüfung dauert meist wenige Tage. Der Store signiert das Paket selbst.

## 5. Was die App im Paket darf

Das Manifest verlangt nur die Fähigkeit `runFullTrust`, die jede verpackte MAUI-Desktop-App braucht. Es gibt keine
Zugriffe auf Kamera, Mikrofon, Standort oder Dateien. Die App spricht mit dem Server, dessen Adresse die Person in der App
wählt, und sonst mit niemandem.

## 6. Der Testweg am PC (unverändert)

Ohne den Schalter `DuofyStorePackage` bleibt alles wie bisher: unverpackt, ohne Installation, direkt aus dem Build-Ordner.

```powershell
dotnet build Mobile/Duofy -f net10.0-windows10.0.19041.0 -t:Run
```

## 7. Bekannte Grenzen

- Das Symbolpaket (Fehlersuche für Abstürze) wird nicht erzeugt, weil es Visual-Studio-Werkzeuge braucht und der Store es
  nicht verlangt (`AppxSymbolPackageEnabled=false`).
- Das App-Symbol und die Kachelbilder im Paket kommen aus `Mobile/Duofy/Resources/AppIcon/`. Stehen dort noch die
  Bilder der Vorlage, sehen die Kacheln im Paket nach der Vorlage aus; das neue Plagevo-Symbol kommt mit dem Zweig des
  App-Symbols (Issue #8).
- Ob die App aus dem Store-Paket auf einem Rechner startet, ist hier nicht geprüft worden (siehe Bericht zu Issue #40).
