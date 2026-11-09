# Duofy

**Eine Finanz-App, die plant statt trackt.** Aus Verträgen und festen Zahlungen entsteht der Plan für den Folgemonat;
das Abhaken eines Postens erzeugt die Buchung. Duofy zeigt für jeden Monat, was hereinkommt, was schon vergeben ist und was
frei bleibt, allein oder gemeinsam im Haushalt.

Duofy ist in C# und .NET 10 geschrieben. Es läuft als Web-Variante im Browser, als App für Android und als App für Windows,
alle drei sprechen mit derselben API. Die alte Version mit Python-Backend und React-Oberfläche ist archiviert:
[Duofy-python](https://github.com/tom-schorn/Duofy-python).

## Was heute funktioniert

- **Monatsplan:** Aus Verträgen und regelmäßigen Zahlungen entsteht der Plan eines Monats mit Einnahmen, Festem und dem
  frei verplanbaren Betrag. Posten lassen sich anlegen, ändern und abhaken; was im Vormonat übrig war, lässt sich als Übertrag
  in den neuen Monat nehmen. Dazu gibt es eine Planübersicht über mehrere Monate und Diagramme.
- **Konten und Buchungen:** Konten mit Symbol, Buchungen (Einnahme, Ausgabe, Umbuchung, Übertrag) mit Kategorien, mehrere Buchungen auf einen Posten, Buchungsliste je Konto.
- **Verträge, Sparen und Schulden:** Verträge und regelmäßige Zahlungen (mit Konto und Zahlungsart), Sparziele, Schulden mit Zahlungen. Beträge sind
  immer `decimal`.
- **Haushalt:** Ein Haushalt mit mehreren Personen, Einladung über einen Link, eigene Freigaben (jede Person bestimmt,
  was sie teilt), ein gemeinsamer Haushaltsplan und Richtwerte.
- **Erinnerungen:** Push-Erinnerungen an fällige Posten auf Android und Windows, mit "später erinnern". Sie gibt es nur in
  der gehosteten Version und sind standardmäßig aus.
- **Konto und Server:** Registrierung (offen, nur mit Einladung oder geschlossen), Anmeldung mit automatischer
  Token-Erneuerung, Passwort ändern und zurücksetzen, E-Mail-Bestätigung in der gehosteten Version, Konto löschen, eine
  Verwaltungsseite für die Administration (Einladungen, Rücksetz-Links).
- **Rechtliches:** Impressum, Datenschutz und Nutzungsbedingungen als Textdateien des Servers, ohne Anmeldung unter
  `/api/v1/legal` abrufbar und in jeder Seite verlinkt, dazu die Lizenzen der mitgelieferten Bausteine.
- **API:** Die Duofy-API unter `/api/v1` (Konto, Konten, Buchungen, Kategorien, Pläne, Verträge, Schulden, Sparziele,
  Haushalte, Erinnerungen, Rechtstexte, Verwaltung); `/api/v1/health` prüft auch die Datenbank.

## Aufbau

| Projekt | Inhalt |
|---|---|
| `Backend/Duofy.Backend` | Das Backend in einem Projekt: `Api/` (die Duofy-API unter `/api/v1`), `Data/` (Datenbank mit Entity Framework Core, PostgreSQL, und Migrationen) und `Domain/` (Fachregeln in reinem C#); als einziges Projekt mit Datenbank |
| `Backend/Duofy.Schemas` | Anfragen und Antworten der API, gemeinsam für API, Web, App und Tests |
| `Shared/Duofy.ApiClient` | Clients für die API mit Token-Ablage und automatischem Token-Refresh |
| `Shared/Duofy.Shared` | Seiten und Komponenten, die Web und App gemeinsam nutzen |
| `Web/Duofy.Web` | Web-Variante (Blazor mit Radzen), ohne Datenbank; holt alles von der API |
| `Mobile/Duofy` | App für Android und Windows (.NET MAUI) |
| `Mobile/Duofy.Push` | Empfang der Erinnerungen in der App |
| `Tests/` | xUnit-Tests je Projekt (`Duofy.Backend.Tests` gegen eine echte PostgreSQL in Docker mit Testcontainers) und Browserprüfungen (`*/check.mjs`) |
| `deploy/` | Compose-Dateien, Sicherung und Prüfskript für den Betrieb auf einem Server |
| `docs/` | Betrieb, Store-Einreichung, App-Installation, Spezifikationen und Pläne |

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker, für die Datenbank beim lokalen Start und für die Tests der API
- für die App zusätzlich die MAUI-Workload: `dotnet workload install maui`

## Lokal starten

Duofy braucht eine PostgreSQL-Datenbank. Am einfachsten startet sie in Docker (dieselbe Version wie in `deploy/own-db.yml`):

```bash
docker run -d --name duofy-db -e POSTGRES_DB=duofy -e POSTGRES_USER=duofy -e POSTGRES_PASSWORD=change-me \
  -p 127.0.0.1:5432:5432 -v duofy-db:/var/lib/postgresql postgres:18.6-alpine
```

**Einstellungen.** `.env.example` ist die Vorlage mit allen Umgebungsvariablen und ihrer Erklärung; .NET liest eine `.env`
nicht von selbst. Setze die Werte in der Shell, in der IDE oder als User-Secrets (nie in einer eingecheckten Datei). Lokal
genügen zwei:

```bash
export ConnectionStrings__Duofy="Host=localhost;Port=5432;Database=duofy;Username=duofy;Password=change-me"
export Duofy__RegistrationMode=open   # sonst gilt "invite" und nur ein Einrichtungscode (Duofy__SetupCode) lässt jemanden hinein
```

**API starten** (neue Migrationen spielt sie beim Start selbst ein):

```bash
dotnet run --project Backend/Duofy.Backend
```

Die API läuft dann unter `http://localhost:5080`; `http://localhost:5080/api/v1/health` antwortet mit 200. Das Profil
`lan` (`dotnet run --project Backend/Duofy.Backend --launch-profile lan`) lauscht im ganzen lokalen Netz, zum Beispiel für
das Telefon.

**Web-Variante starten** (in einem zweiten Terminal, die API muss laufen):

```bash
dotnet run --project Web/Duofy.Web
```

Sie öffnet sich unter `http://localhost:5195`. Die Adresse der API steht in `Web/Duofy.Web/appsettings.json` unter
`Duofy:ApiBaseAddress` und lässt sich mit der Umgebungsvariable `Duofy__ApiBaseAddress` ändern.

**Alle Tests** (die API-Tests starten dafür selbst eine PostgreSQL in Docker):

```bash
dotnet test Duofy.slnx
```

## Auf einem Server betreiben

Für den Betrieb gibt es fertige Docker-Images (`Backend/Duofy.Backend/Dockerfile`, `Web/Duofy.Web/Dockerfile`, beide ohne
Root-Rechte und mit Gesundheitsprüfung) und eine Compose-Datei in `deploy/`. Sie startet API, Web-Variante und eine
tägliche Datenbanksicherung; ein Port wird nicht veröffentlicht, die Dienste erreicht man über einen Tunnel oder einen
eigenen Proxy. Die Datenbank ist ein PostgreSQL-Server, den `ConnectionStrings__Duofy` nennt, oder die eigene aus
`deploy/own-db.yml`. Die gehostete Version heißt Plagevo ("Plagevo by Duofy"): `portal.plagevo.de` ist die Web-Variante,
`api.plagevo.de` die API.

```bash
cd deploy
cp .env.example .env      # ausfüllen, nie einchecken
docker compose --env-file .env up -d --build
```

Die ganze Einrichtung Schritt für Schritt (Cloudflare und Tunnel, Datenbank, Umgebungsdatei, Mail, erster Start mit dem
Einrichtungscode, Update, Sicherung und Wiederherstellung) steht in [docs/betrieb.md](docs/betrieb.md).

## Apps

Die Apps für Android und Windows sind .NET MAUI und zeigen dieselben Seiten wie die Web-Variante. Ohne weitere Einstellung
sprechen sie mit dem gehosteten Dienst `https://api.plagevo.de/` (`Mobile/Duofy/appsettings.json`, Schlüssel
`Api:BaseAddress`). Wie man sie selbst installiert, steht in [docs/app-installieren.md](docs/app-installieren.md); das Paket
für den Microsoft Store beschreibt [docs/store/README.md](docs/store/README.md).

## Einbetten

Eine selbst betriebene Web-Variante lässt sich in einer eigenen Seite im iframe nutzen, zum Beispiel in Home Assistant.
Dafür nennt `Duofy__FrameAncestors` die erlaubten Seiten, mehrere durch Leerzeichen getrennt (mit `deploy/docker-compose.yml`
geht auch `DUOFY_FRAME_ANCESTORS`):

```bash
Duofy__FrameAncestors=https://homeassistant.example.org:8123
```

Jede Adresse ist genau `https://rechner[:port]`, ohne Pfad, ohne Schrägstrich am Ende und ohne `*`; `http://` gilt nur in
der Entwicklung. Ein ungültiger Wert bricht den Start ab. Ohne die Variable bleibt alles gesperrt wie bisher; Plagevo
setzt sie nicht.

Die Web-Variante selbst muss über https erreichbar sein, sonst nimmt der Browser ihre Cookies nicht an. Safari und damit
die Home-Assistant-App auf dem iPhone sperren Cookies fremder Seiten grundsätzlich; dort klappt die Anmeldung im iframe
nur, wenn beide Seiten unter derselben Domain liegen (`ha.example.org` und `duofy.example.org`). Wer die Web-Variante
vorher schon direkt genutzt hat, meldet sich im iframe einmal neu an.

**Sicherheit:** Jede eingetragene Seite kann Duofy in ihrem Rahmen zeigen und darüberlegen, was sie will; wird sie
übernommen (zum Beispiel per XSS), gilt das auch für den Angreifer. Deshalb nur eigene, vertrauenswürdige Seiten eintragen.
Mit der Liste gelten die Cookies außerdem für die ganze Instanz als `SameSite=None`, immer mit `Secure`: Der Browser
schickt sie dann auch bei Anfragen von fremden Seiten mit, und der Schutz gegen untergeschobene Anfragen hängt allein am
Antiforgery-Token und an der Verbindung, die fremde Seiten nicht aufbauen können.

## Schrift

Die Anwendung setzt die Schrift Inter 5.3.0 (Variable, Latin) in der Version aus dem npm-Paket `@fontsource-variable/inter`. Quelle: https://github.com/rsms/inter, Lizenz SIL Open Font License 1.1. Die Datei liegt in `Shared/Duofy.Shared/wwwroot/fonts/` und wird mit der Anwendung ausgeliefert, zusammen mit dem Lizenztext `LICENSE-Inter-OFL.txt`. Es gibt keinen Aufruf an einen Fremdserver.

## Symbole

Die Symbole sind Material Symbols (Apache-2.0), die im Paket Radzen.Blazor mitkommen; es gibt keine Anfrage an fremde Server. Die Oberfläche baut auf Radzen Blazor 12.1.8 (MIT). Welches Symbol zu einem Konto oder einer Zahlungsart gehört, steht in `Shared/Duofy.Shared/Services/AccountIconSet.cs` und `PaymentMethodIconSet.cs`.

## Regeln

- Beträge sind immer `decimal`, nie `double` oder `float`
- Keine echten Beträge, Namen oder Kontonummern in Code und Tests
- Bezeichner und Commits englisch; Kommentare im Code und Doku deutsch
- Commits nach [Conventional Commits](https://www.conventionalcommits.org/)

## Die Android-App auf dem eigenen Telefon testen

Die App läuft auf dem eigenen Telefon und spricht über das WLAN mit der API auf dem
Entwicklungsrechner. Telefon und Rechner müssen im selben WLAN sein.

Einmalig: **Entwickleroptionen** auf dem Telefon einschalten (Einstellungen, Über das
Telefon, siebenmal auf die Build-Nummer tippen) und dort **Drahtloses Debugging** aktivieren.
Android-SDK und JDK liegen unter `%LOCALAPPDATA%\Android\Sdk` und `%LOCALAPPDATA%\Android\openjdk`;
`Directory.Build.props` findet sie von selbst. `adb` liegt in `%LOCALAPPDATA%\Android\Sdk\platform-tools`.

1. **Pair** – Telefon mit dem Rechner koppeln. Auf dem Telefon: Drahtloses Debugging, "Gerät mit
   Kopplungscode koppeln". Dann auf dem Rechner mit der dort gezeigten Adresse und dem Code:

   ```bash
   adb pair 192.168.0.23:37099
   adb connect 192.168.0.23:41234
   ```

   (`pair` benutzt die Kopplungs-Adresse, `connect` die Adresse vom Hauptbildschirm des Drahtlosen Debuggings.)
   Danach zeigt `adb devices` das Telefon.

2. **Start the API** – die API so starten, dass sie im lokalen Netz erreichbar ist und nicht nur auf
   `localhost` (die genaue Adresse nennt die Dokumentation der API):

   ```bash
   dotnet run --project Backend/Duofy.Backend --urls http://0.0.0.0:5080
   ```

   Die Windows-Firewall muss eingehende Verbindungen auf diesem Port erlauben. Die IP-Adresse des Rechners
   zeigt `ipconfig` (zum Beispiel `192.168.0.10`).

3. **Install the app** – die Debug-App mit der Adresse des Rechners bauen und auf das Telefon installieren.
   Die Adresse steht in `Mobile/Duofy/appsettings.Debug.json` unter `Api:BaseAddress`; für den eigenen Rechner
   lässt sie sich mit einer Datei `Mobile/Duofy/appsettings.local.json` überschreiben (nicht einchecken):

   ```bash
   dotnet build Mobile/Duofy -f net10.0-android -t:Run
   ```

   Debug-Builds dürfen unverschlüsseltes HTTP benutzen, Release-Builds nicht.

Ein signiertes Release-Bundle für Google Play entsteht mit den Umgebungsvariablen `DUOFY_KEYSTORE_PATH`,
`DUOFY_KEYSTORE_PASSWORD`, `DUOFY_KEY_ALIAS` und `DUOFY_KEY_PASSWORD`:

```bash
dotnet publish Mobile/Duofy -f net10.0-android -c Release
```

Ohne `DUOFY_KEYSTORE_PATH` signiert das SDK mit dem Debug-Schlüssel. Das genügt, um einen Release-Build auf dem eigenen
Telefon zu testen (als APK, nicht für Google Play):

```bash
dotnet publish Mobile/Duofy -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

Die Datei liegt danach unter `Mobile/Duofy/bin/Release/net10.0-android/publish/` (`de.tomschorn.duofy-Signed.apk`). Die
App heißt auf dem Telefon Plagevo und nutzt ohne weitere Einstellung den gehosteten Dienst `https://api.plagevo.de/`.

## Sicherheit

Sicherheitsprobleme bitte nicht öffentlich melden, sondern privat über GitHub; wie, steht in [SECURITY.md](SECURITY.md).

## Lizenz

Duofy steht unter der GNU Affero General Public License v3.0, siehe [LICENSE](LICENSE).
