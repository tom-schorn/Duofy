# Die App selbst installieren: Windows und Android

Diese Anleitung bringt Plagevo (Duofy) als Testpaket auf deinen Windows-PC und als Release-App auf dein Android-Telefon.
Beide Apps sprechen mit dem echten Server (`https://api.plagevo.de/`, steht in `Mobile/Duofy/appsettings.json`).
Es ist kein Store-Weg: Du installierst die Pakete selbst, das nennt man Querladen.

Stand der ersten Installation: gebaut aus dem Stand `c57676a` von `main` (4. November 2026). Bei jedem neuen Bau
trägst du hier den neuen Stand ein (`git rev-parse --short main`).

Alle Befehle laufen im Hauptordner des Repositorys. Windows-Bau und Android-Bau immer nacheinander, nie gleichzeitig
(sonst geht dem PC mit 16 GB der Speicher aus).

## 1. Windows bauen

Voraussetzung: .NET-SDK 10 und der MAUI-Arbeitsbereich (`dotnet workload install maui`).

```powershell
powershell -ExecutionPolicy Bypass -File Mobile/Windows-Testpaket.ps1
```

Das Skript legt bei Bedarf das Testzertifikat an (Abschnitt 2), baut das Paket und signiert es. Das Ergebnis:

- `Mobile/Duofy/AppPackages/Duofy_<Version>_x64_Test/Duofy_<Version>_x64.msix` ist das Paket. `<Version>` ist die
  Paketversion, beim ersten Bau `1.0.0.0`. Das Skript zeigt am Ende die genauen Pfade an (`Paket: ...`).
- `Mobile/Duofy-Test.cer` ist das öffentliche Zertifikat.

Für ein neues Paket mit höherer Version erhöhst du die Version `Version="..."` im Element `Identity` von
`Mobile/Duofy/Platforms/Windows/Package.appxmanifest` (vier Zahlen, zum Beispiel `1.0.1.0`) und zusammen damit
`ApplicationDisplayVersion` in `Mobile/Duofy/Duofy.csproj`. Welcher der beiden Werte den Ausschlag gibt, ist hier nicht
geprüft; der Ordnername des Pakets nach dem Bau zeigt es. Das Manifest wird beim Bau vom Skript nur an der Identität
verändert, die Version darin bleibt.

Die Warnung zu `mspdbcmf.exe` betrifft nach bisheriger Erfahrung nur ein Symbolpaket und blieb folgenlos.

## 2. Das Testzertifikat

Das Paket trägt die Test-Identität `CN=Duofy Test`. Dazu erzeugt das Skript ein selbst gemachtes Zertifikat. Sein
privater Schlüssel bleibt im Zertifikatspeicher deines Benutzers und wird nie exportiert; im Repository liegt keine
Datei mit dem privaten Schlüssel und kein Passwort. Nur das öffentliche `Mobile/Duofy-Test.cer` wird mitgegeben.

Neu angelegte Zertifikate sind nicht exportierbar (`-KeyExportPolicy NonExportable`), der Schlüssel lässt sich also auch
aus Versehen nicht herausziehen. Ein schon vorhandenes Zertifikat bleibt exportierbar, bis du es beim nächsten `-Renew`
(siehe unten) ersetzt.

Windows installiert nur Pakete, deren Zertifikat es kennt. Dafür vertraust du dem `.cer` einmal. Das braucht
Administratorrechte, deshalb machst du es selbst. PowerShell als Administrator öffnen und:

```powershell
Import-Certificate -FilePath "Mobile\Duofy-Test.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Wichtig ist der Speicher: Das Zertifikat gehört in "Lokaler Computer", "Vertrauenswürdige Personen" (englisch
`LocalMachine\TrustedPeople`). Über die grafische Oberfläche (Doppelklick auf die `.cer`, "Zertifikat installieren")
wählst du "Lokaler Computer" und dann "Alle Zertifikate in folgendem Speicher speichern" mit "Vertrauenswürdige
Personen". Landet es stattdessen im Benutzerspeicher oder unter "Vertrauenswürdige Stammzertifizierungsstellen", zeigt
`Get-AuthenticodeSignature` zwar `Valid`, die Installation scheitert aber mit dem Fehler `0x800B0109`
("Das Stammzertifikat der Signatur ... muss vertrauenswürdig sein"). Entferne dann die falschen Kopien (siehe unten).

Prüfen, ob das Zertifikat am richtigen Ort liegt:

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq "CN=Duofy Test"
```

Signatur prüfen (Status `Valid`; die Version im Pfad ersetzt der Platzhalter `*`):

```powershell
Get-AuthenticodeSignature (Get-ChildItem "Mobile\Duofy\AppPackages\Duofy_*_x64_Test\Duofy_*_x64.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
```

### Zertifikat erneuern

Das Zertifikat gilt ein Jahr. Danach (oder wenn du es verlierst) erneuerst du es: Das Skript mit `-Renew` aufrufen, es
erzeugt ein neues Zertifikat und baut das Paket damit neu. Dann das neue `.cer` wie oben vertrauen. Die erneuerte
`Mobile/Duofy-Test.cer` gehört danach in einen Commit, damit das Repository das gültige öffentliche Zertifikat trägt.
Ein Paket, das mit einem neuen Zertifikat signiert ist, lässt sich vermutlich über das alte nur installieren, wenn du
das alte zuerst deinstallierst.

```powershell
powershell -ExecutionPolicy Bypass -File Mobile/Windows-Testpaket.ps1 -Renew
```

### Zertifikat wieder entfernen

Wenn du die Test-App nicht mehr brauchst, nimmst du dem Zertifikat das Vertrauen wieder. Zuerst die App deinstallieren
(Abschnitt 4), dann in einer Administrator-PowerShell:

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq "CN=Duofy Test" | Remove-Item
```

Falls das Zertifikat versehentlich auch an anderen Orten liegt, entfernst du dort ebenfalls die Kopien mit dem Namen
`CN=Duofy Test`: im Benutzerspeicher `Cert:\CurrentUser\Root`, `Cert:\CurrentUser\CA` und
`Cert:\CurrentUser\TrustedPeople` (normale PowerShell) und im
Speicher `Cert:\LocalMachine\Root` (Administrator). Das Zertifikat mit dem privaten Schlüssel in `Cert:\CurrentUser\My`
löschst du nur, wenn du das Testpaket nie wieder bauen willst.

## 3. Windows installieren

Nach dem Vertrauensschritt, in einer normalen PowerShell:

```powershell
Add-AppxPackage -Path (Get-ChildItem "Mobile\Duofy\AppPackages\Duofy_*_x64_Test\Duofy_*_x64.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
Get-AppxPackage Duofy.Test
```

Die App erscheint im Startmenü. Beim ersten Start meldest du dich mit deinem Konto an. Ohne Netz zeigt sie ihre
Offline-Meldung.

Eine neue Version mit höherer Versionsnummer installierst du mit demselben Befehl über die alte.

## 4. Windows deinstallieren

```powershell
Get-AppxPackage Duofy.Test | Remove-AppxPackage
```

App und Startmenü-Eintrag sind danach weg. Deine Daten liegen auf dem Server und bleiben.

## 5. Android bauen

Voraussetzung: .NET-SDK 10, der Arbeitsbereich `android` und das Android-SDK.

```powershell
dotnet publish Mobile/Duofy -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

Das Ergebnis ist `Mobile/Duofy/bin/Release/net10.0-android/de.tomschorn.duofy-Signed.apk`. Der Schalter
`AndroidPackageFormat=apk` ist nötig, weil ein Release sonst ein App-Bundle für Google Play baut, das sich nicht
direkt installieren lässt.

Signiert wird mit dem Schlüssel aus den Umgebungsvariablen `DUOFY_KEYSTORE_PATH`, `DUOFY_KEY_ALIAS`,
`DUOFY_KEYSTORE_PASSWORD` und `DUOFY_KEY_PASSWORD`. Sind sie nicht gesetzt, signiert das SDK mit dem Debug-Schlüssel;
das genügt für dein eigenes Telefon, nicht für Google Play.

Prüfen (der Prüfer braucht Java, zum Beispiel `JAVA_HOME` auf ein JDK setzen):

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\build-tools\36.0.0\apksigner.bat" verify --verbose --print-certs Mobile\Duofy\bin\Release\net10.0-android\de.tomschorn.duofy-Signed.apk
```

## 6. Android installieren

Auf dem Telefon: Einstellungen, Über das Telefon, siebenmal auf die Build-Nummer tippen, dann in den
Entwickleroptionen USB-Debugging einschalten. Telefon per Kabel anschließen und die Debug-Anfrage bestätigen.

Alle `adb`-Befehle hier brauchen den Pfad zum Programm. Setze ihn zuerst, in derselben PowerShell:

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
```

Statt Kabel geht auch drahtloses Debugging (Telefon und PC im selben WLAN): in den Entwickleroptionen "Drahtloses
Debugging" einschalten, "Mit Kopplungscode koppeln" wählen und dann am PC erst koppeln, dann verbinden. Adresse und
Anschluss (Port) zeigt das Telefon an:

```powershell
& $adb pair <Adresse>:<Kopplungsport>
& $adb connect <Adresse>:<Port>
```

Bei mehreren Geräten gibst du bei jedem `adb`-Befehl `-s <Adresse>:<Port>` an.

Installieren und prüfen:

```powershell
& $adb devices
& $adb install -r Mobile\Duofy\bin\Release\net10.0-android\de.tomschorn.duofy-Signed.apk
& $adb shell pm list packages de.tomschorn.duofy
```

Ganz ohne `adb`: die `.apk` aufs Telefon kopieren, in der Dateien-App antippen und das Installieren aus dieser Quelle erlauben.

Eine frühere Installation wird überschrieben, wenn sie mit demselben Schlüssel signiert war. Meldet Android einen
Signaturkonflikt (zum Beispiel weil die alte App mit einem anderen Schlüssel kam), entfernst du die alte App zuerst, in
den Einstellungen oder mit `& $adb uninstall de.tomschorn.duofy`. Deine Daten liegen auf dem Server und bleiben.
