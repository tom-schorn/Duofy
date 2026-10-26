# Duofy (.NET)

**Eine Finanz-App, die plant statt trackt** — neu gebaut in C# und .NET 10.

Dies ist der Neubau von [Duofy](https://github.com/tom-schorn/Duofy), das bisher
aus einem Python-Backend und einer React-Oberfläche besteht. Fachlich bleibt es
dasselbe: Aus Verträgen entsteht der Plan für den Folgemonat, und das Abhaken
eines Postens erzeugt die Buchung.

> **Stand:** ganz am Anfang. Es gibt die Fachregeln für Verträge und ihre Tests,
> noch keine Datenbank und keine eigene Oberfläche. Der Lernweg steht unter
> [docs/plans](docs/plans).

## Aufbau

| Projekt | Inhalt |
|---|---|
| `Backend/Duofy.Backend` | Das Backend in einem Projekt: `Api/` (die Duofy-API unter `/api/v1`), `Data/` (Datenbank mit Entity Framework Core, PostgreSQL, und Migrationen) und `Domain/` (Fachregeln in reinem C#); als einziges Projekt mit Datenbank |
| `Backend/Duofy.Schemas` | Anfragen und Antworten der API, gemeinsam für API, Web, App und Tests |
| `Shared/Duofy.ApiClient` | Clients für die API mit Token-Ablage und automatischem Token-Refresh |
| `Shared/Duofy.Shared` | Seiten und Komponenten, die Web und App gemeinsam nutzen |
| `Web/Duofy.Web` | Web-Variante (Blazor), ohne Datenbank; holt alles von der API |
| `Mobile/Duofy` | App für Android, iOS, Mac und Windows (.NET MAUI) |
| `Tests/Duofy.Backend.Tests` | Tests des Backends: die API gegen eine echte PostgreSQL in Docker (Testcontainers) und die Fachregeln unter `Domain/` (xUnit) |
| `Tests/Duofy.ApiClient.Tests` | Tests der Clients |
| `Tests/Duofy.Shared.Tests` | Tests der gemeinsamen Seiten und Komponenten |
| `Tests/Duofy.Web.Tests` | Tests der Web-Variante und der Projektgrenzen |
| `Tests/Duofy.App.Tests` | Tests der App-Projektdateien |

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- für die App zusätzlich die MAUI-Workload: `dotnet workload install maui`
- für die API eine PostgreSQL-Datenbank, für die Tests der API Docker

## Bauen, testen, starten

Alle Tests (die API-Tests starten dafür selbst eine PostgreSQL in Docker):

```bash
dotnet test Duofy.slnx
```

**API starten.** Die API braucht die Verbindungszeichenfolge zur Datenbank. Am
einfachsten liegt sie lokal in den User-Secrets (nie in einer eingecheckten Datei):

```bash
dotnet user-secrets set "ConnectionStrings:Duofy" "Host=localhost;Database=duofy;Username=duofy;Password=..." --project Backend/Duofy.Backend
dotnet run --project Backend/Duofy.Backend
```

Die API läuft dann unter `http://localhost:5080`; `http://localhost:5080/api/v1/health`
antwortet mit 200. Neue Migrationen spielt sie beim Start selbst ein. Das Profil
`lan` (`dotnet run --project Backend/Duofy.Backend --launch-profile lan`) lauscht im ganzen
lokalen Netz, zum Beispiel für das Telefon.

**Web-Variante starten** (in einem zweiten Terminal, die API muss laufen):

```bash
dotnet run --project Web/Duofy.Web
```

Die Adresse der API steht in `Web/Duofy.Web/appsettings.json` unter `Duofy:ApiBaseAddress`
und lässt sich mit der Umgebungsvariable `Duofy__ApiBaseAddress` ändern. Alle
Umgebungsvariablen stehen in `.env.example`.

## Plagevo betreiben

Plagevo ist die gehostete Version von Duofy ("Plagevo by Duofy"). Sie läuft auf einem eigenen Server hinter einem
Cloudflare Tunnel, ohne offenen Port: `app.plagevo.de` ist die Web-Variante, `api.plagevo.de` die API. Alles dafür liegt
in `deploy/`: Die Compose-Datei startet API, Web-Variante, PostgreSQL und eine tägliche Sicherung; die Images bauen aus
`Backend/Duofy.Backend/Dockerfile` und `Web/Duofy.Web/Dockerfile`.

```bash
cd deploy
cp .env.example .env      # ausfüllen, nie einchecken
docker compose --env-file .env up -d --build
```

Die ganze Einrichtung Schritt für Schritt (Cloudflare, Umgebungsdatei, Mail über `noreply@example.org`, erster Start mit
dem Einrichtungscode, Update, Sicherung und Wiederherstellung) steht in [docs/betrieb.md](docs/betrieb.md).

## Regeln

- Beträge sind immer `decimal`, nie `double` oder `float`
- Keine echten Beträge, Namen oder Kontonummern in Code und Tests
- Bezeichner und Commits englisch; Kommentare im Code und Doku deutsch
- Commits nach [Conventional Commits](https://www.conventionalcommits.org/)

## Testing on an Android phone over Wi-Fi

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

## Lizenz

GNU Affero General Public License v3.0, siehe [LICENSE](LICENSE). Wer Duofy als Dienst für andere betreibt, stellt seine Änderungen ebenfalls unter diese Lizenz.
