# Plagevo betreiben

Diese Anleitung beschreibt Schritt für Schritt, wie Plagevo auf dem eigenen Server läuft: hinter einem Cloudflare
Tunnel, ohne einen offenen Port außer SSH, mit der Datenbank bei dir zu Hause, erreichbar über Tailscale. Plagevo ist
die gehostete Version von Duofy ("Plagevo by Duofy"); der Code heißt weiter Duofy. Die Anforderungen stehen in
[docs/specs/79-operations](specs/79-operations).

Du brauchst diese Anleitung selten, deshalb steht jeder Schritt ausgeschrieben da, auch die Befehle. Vier Platzhalter
setzt du selbst ein: `<installationsordner>` ist der Ordner dieses Repositorys auf dem Server, `<tunnel-ordner>` der Ordner
des Compose-Projekts für den Tunnel, `<server-tailnet-ip>` die Tailscale-Adresse des Servers (dort `tailscale ip -4`) und
`<benutzer>` dein SSH-Benutzer auf dem Server.

## Überblick

Auf dem Server laufen diese Container:

| Dienst | Aufgabe | Speichergrenze |
| --- | --- | --- |
| `api` | Die Duofy-API, erreichbar als `api.plagevo.de` | 384 MB |
| `web` | Die Web-Variante, erreichbar als `portal.plagevo.de` | 512 MB |
| `backup` | Sichert die Datenbank jeden Tag nach `deploy/backups` | 128 MB |
| `cloudflared` | Der Tunnel zu Cloudflare; ein eigenes Compose-Projekt in `<tunnel-ordner>` | – |

`api`, `web` und `backup` kommen aus `deploy/docker-compose.yml`. Die Datenbank gehört nicht dazu: Es ist dein
PostgreSQL-Server zu Hause, den der Server über Tailscale erreicht (Schritt 3). Wer keinen eigenen PostgreSQL-Server
hat, startet die Datenbank mit der zweiten Datei `deploy/own-db.yml` dazu (Schritt 12).

Alle Container hängen im eigenen Docker-Netz `plagevo`, das `deploy/docker-compose.yml` anlegt, mit festen Adressen:

| Adresse | Wer |
| --- | --- |
| `172.30.10.1` | das Gateway des Netzes (Docker) |
| `172.30.10.2` | der Tunnel (`cloudflared`) |
| `172.30.10.11` | `api` |
| `172.30.10.12` | `web` |
| `172.30.10.13` | `backup` |

Der Weg einer Anfrage: Der Browser oder die App spricht https mit Cloudflare. Cloudflare schickt die Anfrage durch den
Tunnel zum Container `cloudflared`. Der leitet `portal.plagevo.de` an `http://172.30.10.12:8080` (web) und
`api.plagevo.de` an `http://172.30.10.11:8080` (api) weiter. Die Web-Variante fragt die API unter
`http://172.30.10.11:8080/`. Kein Container veröffentlicht einen Port; von außen kommt man nur über den Tunnel herein.

Die API glaubt nur dem Tunnel an `172.30.10.2`, von welcher Adresse eine Person kommt (`CF-Connecting-IP`). Die
Web-Variante an `172.30.10.12` nimmt sie von ihrer eigenen Bremse für Anmeldungen aus, denn sonst teilten sich alle
Web-Nutzer ein Budget. Vor der Web-Variante bremst deshalb die Regel bei Cloudflare (Schritt 2.3).

## 1. Was du brauchst

- den Server mit Docker und dem Compose-Plugin (`docker compose version` zeigt eine Version 2.24 oder neuer)
- Tailscale auf dem Server und auf deinem PostgreSQL-Server zu Hause, beide im selben Tailnet
- die Domain `plagevo.de` in Cloudflare (kostenloser Plan reicht)
- das Postfach der Absender-Adresse (`<absender-adresse>`) mit seinen SMTP-Zugangsdaten
- dieses Repository auf dem Server unter `<installationsordner>`:

  ```bash
  git clone https://github.com/tom-schorn/duofy-dotnet.git <installationsordner>
  cd <installationsordner>/deploy
  ```

Alle weiteren Befehle auf dem Server laufen im Ordner `deploy`, außer denen für den Tunnel in `<tunnel-ordner>`.

## 2. Cloudflare einrichten

Alles hier geht im Dashboard von Cloudflare unter [dash.cloudflare.com](https://dash.cloudflare.com), mit dem
kostenlosen Plan.

### 2.1 Domain

`plagevo.de` muss in Cloudflare als Domain eingetragen sein, und beim Registrar müssen die Nameserver von Cloudflare
stehen. Das Dashboard zeigt dann bei der Domain den Status "Active". Die Einträge für `portal` und `api` legt der Tunnel
im nächsten Schritt selbst an.

### 2.2 Tunnel

Im Dashboard unter Zero Trust, Networks, Tunnels den Tunnel öffnen (Fall B: erst anlegen, siehe unten), dann den Reiter
"Public Hostname" (je nach Version "Published application routes"). Zwei Einträge anlegen:

| Subdomain | Domain | Typ | URL |
| --- | --- | --- | --- |
| `portal` | `plagevo.de` | HTTP | `172.30.10.12:8080` |
| `api` | `plagevo.de` | HTTP | `172.30.10.11:8080` |

Cloudflare legt dazu die DNS-Einträge `portal.plagevo.de` und `api.plagevo.de` an. Der Typ ist HTTP, nicht HTTPS: Die
Verschlüsselung macht Cloudflare, im Tunnel ist die Verbindung ohnehin geschützt. `app` statt `portal` geht genauso;
dann überall `app.plagevo.de` statt `portal.plagevo.de` einsetzen, auch in `DUOFY_PUBLIC_URL`.

**Fall A: Der Tunnel läuft in seinem eigenen Compose-Projekt (so ist es auf dem Server).** Der Container `cloudflared`
gehört zum Projekt in `<tunnel-ordner>`. Er tritt dem Netz `plagevo` als externem Netz bei und bekommt dort die
feste Adresse `172.30.10.2`. In `<tunnel-ordner>/docker-compose.yml` sieht das so aus:

```yaml
services:
  cloudflared:
    image: cloudflare/cloudflared:2026.10.0
    container_name: cloudflared
    restart: unless-stopped
    command: ["tunnel", "--no-autoupdate", "run"]
    # Die .env neben dieser Datei enthält nur TUNNEL_TOKEN=<Token des Tunnels>.
    env_file: .env
    networks:
      plagevo:
        ipv4_address: 172.30.10.2

networks:
  # Legt Plagevo an (deploy/docker-compose.yml); hier nur benutzt.
  plagevo:
    external: true
```

Hängt `cloudflared` noch in weiteren Netzen für andere Anwendungen, bleiben die dort einfach stehen; nur `plagevo` kommt
dazu. Die Reihenfolge beim Start: erst Plagevo (das legt das Netz an, Schritt 7), dann das Projekt des Tunnels:

```bash
cd <tunnel-ordner>
docker compose up -d
```

Fehlt das Netz, weil Plagevo noch nicht lief, bricht dieser Befehl mit "network plagevo declared as external, but could
not be found" ab. Dann erst Plagevo starten. Wird `cloudflared` neu erstellt, bekommt er wieder dieselbe Adresse, denn
sie steht in seiner Compose-Datei.

**Fall B: Ein frischer Server ohne Tunnel.** Im Dashboard unter Zero Trust, Networks, Tunnels einen neuen Tunnel vom Typ
"Cloudflared" anlegen. Bei "Install connector" steht ein Befehl mit einem langen Token nach `--token`; nur dieses Token
kommt als `TUNNEL_TOKEN` in `deploy/.env`. Die beiden Einträge wie oben anlegen. Beim Start (Schritt 7) kommt dann
`--profile own-tunnel` dazu; das startet den Dienst `tunnel` aus `deploy/docker-compose.yml` mit der Adresse
`172.30.10.2` im selben Netz. Ein eigenes Projekt in `<tunnel-ordner>` gibt es dann nicht.

### 2.3 Die eine Regel gegen zu viele Anfragen

Der kostenlose Plan erlaubt genau eine Regel. Unter der Domain `plagevo.de`, Security, WAF, Reiter "Rate limiting rules",
eine Regel anlegen:

- Name: `Plagevo Anmeldung`
- Wenn eingehende Anfragen passen, als Ausdruck ("Edit expression"):

  ```text
  (starts_with(http.request.uri.path, "/api/v1/auth/")) or (http.host eq "portal.plagevo.de" and starts_with(http.request.uri.path, "/_blazor"))
  ```

- Mit denselben Eigenschaften: IP (beim kostenlosen Plan fest)
- Wenn die Rate übersteigt: 10 Anfragen in 10 Sekunden
- Dann: Block, für 10 Sekunden

Der erste Teil bremst die App, die direkt mit `api.plagevo.de` spricht. Der zweite Teil bremst, wie oft jemand eine
Verbindung zur Web-Variante aufbaut. Wichtig: Die Web-Variante ist von der Bremse der API ausgenommen, diese Regel ist für
sie also die einzige Grenze vor dem Server, und sie muss `portal.plagevo.de` abdecken. Was innerhalb einer schon offenen
Verbindung der Web-Variante passiert, zum Beispiel mehrere Anmeldeversuche nacheinander, sieht Cloudflare nicht. Dann
greift nur noch die Sperre eines Kontos nach fünf falschen Passwörtern für fünf Minuten.

### 2.4 Turnstile

Turnstile (der Schutz der Formulare vor Bots) ist auf Version 2 verschoben: Die erste Version läuft im Modus
`invite`, ohne Einladung registriert sich niemand. Jetzt nichts einrichten.

### 2.5 Web Analytics

Web Analytics zählt Seitenaufrufe. Plagevo lädt das Skript von Cloudflare (den "Beacon") erst, wenn die Person im
Hinweis "Einwilligung" zugestimmt hat; ohne Zustimmung und ohne Token lädt nichts. Deshalb darf Cloudflare das Skript
nicht selbst in die Seiten einfügen, sonst liefe es auch bei denen, die abgelehnt haben.

So stellst du es ein:

1. Im Dashboard unter Analytics & Logs, Web Analytics, "Add a site": als Hostname `portal.plagevo.de` eintragen.
2. Die automatische Einrichtung (Cloudflare fügt das Skript selbst ein, "Enable automatic setup") ausschalten, also die
   manuelle Einrichtung wählen. Gibt es für den Hostnamen schon eine Site mit automatischer Einrichtung, dort die
   automatische Einrichtung abschalten oder die Site löschen und neu anlegen.
3. Cloudflare zeigt jetzt einen Schnipsel mit `data-cf-beacon='{"token": "..."}'`. Nur den Wert hinter `token` trägst du
   in der `.env` als `CLOUDFLARE_ANALYTICS_TOKEN` ein (Schritt 4), dann `docker compose up -d`.

Ohne Token fragt Plagevo gar nicht erst nach der Einwilligung und lädt nichts. Was die Datenschutzerklärung dazu sagen
muss, steht als Vorlage in `docs/rechtstexte/datenschutz-cloudflare.txt` (Schritt 6).

### 2.6 Chat (Crisp)

Der Chat kommt von Crisp. Plagevo lädt das Skript von Crisp erst, wenn die Person im Hinweis "Einwilligung" dem Chat
zugestimmt hat (eigene Wahl, getrennt von der Statistik). Ohne Website-ID gibt es keinen Chat und keine Frage.

1. Im Crisp-Konto die Website anlegen und die Website-ID kopieren (Settings, Website Settings, Setup instructions).
2. Die ID in der `.env` als `CRISP_WEBSITE_ID` eintragen, dann `docker compose up -d`.
3. Das Bild des Chats (das Logo) stellst du im Crisp-Konto ein (Chatbox-Einstellungen, Aussehen); das Skript von
   Plagevo setzt es nicht.

Plagevo übergibt keine Nutzerdaten an Crisp: keine E-Mail, keinen Namen, keine Kennung. Was die Datenschutzerklärung
dazu sagen muss, steht als Vorlage in `docs/rechtstexte/datenschutz-crisp.txt` (Schritt 6).

## 3. Datenbank bei dir zu Hause über Tailscale

Die Daten von Plagevo liegen in deinem PostgreSQL-Server zu Hause. Der Server erreicht ihn über Tailscale, also durch
ein verschlüsseltes WireGuard-Netz; im Internet ist der Port der Datenbank nie offen. Die Adresse des Servers im
Tailnet ist `<server-tailnet-ip>`, die deines PostgreSQL-Servers siehst du dort mit `tailscale ip -4` (unten `100.x.y.z`).

Alles in diesem Schritt machst du auf deinem PostgreSQL-Server zu Hause, nicht auf dem Server.

1. Benutzer und Datenbank anlegen. Das Passwort ist ein langes Zufallspasswort, zum Beispiel aus
   `openssl rand -base64 32`; es kommt später in die Verbindung in `deploy/.env`:

   ```bash
   sudo -u postgres psql
   ```

   ```sql
   CREATE ROLE plagevo LOGIN PASSWORD '<das Passwort>';
   CREATE DATABASE plagevo OWNER plagevo;
   \q
   ```

   Die Tabellen legt die API beim ersten Start selbst an (Migrationen); dafür reicht es, dass ihr die Datenbank gehört.

2. PostgreSQL auch an der Tailscale-Adresse lauschen lassen. In `postgresql.conf` (meist
   `/etc/postgresql/<Version>/main/postgresql.conf`):

   ```text
   listen_addresses = 'localhost,100.x.y.z'
   ```

   Startet PostgreSQL beim Hochfahren vor Tailscale, gibt es die Adresse noch nicht, und PostgreSQL lauscht dort nicht.
   Dann stattdessen `listen_addresses = '*'` setzen; die Firewall aus Punkt 4 hält den Port trotzdem für alle anderen
   zu.

3. Nur den Server hereinlassen. In `pg_hba.conf` (im selben Ordner) eine Zeile anfügen:

   ```text
   host    plagevo    plagevo    <server-tailnet-ip>/32    scram-sha-256
   ```

   Danach PostgreSQL neu starten, denn `listen_addresses` gilt erst dann: `sudo systemctl restart postgresql`.

4. Die Firewall: Port 5432 nur über Tailscale und nur für den Server öffnen, zum Beispiel mit ufw:

   ```bash
   sudo ufw allow in on tailscale0 from <server-tailnet-ip> to any port 5432 proto tcp
   ```

   Aus dem Heimnetz und aus dem Internet bleibt der Port zu. Wer Tailscale-ACLs benutzt, erlaubt dort ebenso nur den
   Server auf `100.x.y.z:5432`.

5. Die Verbindung kommt als `ConnectionStrings__Duofy` in `deploy/.env` auf dem Server (Schritt 4):

   ```text
   ConnectionStrings__Duofy=Host=100.x.y.z;Port=5432;Database=plagevo;Username=plagevo;Password=<das Passwort>
   ```

   Kein Wert darf ein Semikolon enthalten, auch das Passwort nicht. Die API und die Sicherung nutzen dieselbe
   Verbindung.

Zur Verschlüsselung: Die Verbindung läuft ganz innerhalb von Tailscale, also schon verschlüsselt. Npgsql (der Treiber der
API) nimmt TLS, wenn PostgreSQL es anbietet, sonst nicht (`SSL Mode=Prefer`, der Standard). Bietet dein Server TLS an,
kannst du es mit `;SSL Mode=Require` am Ende der Verbindung erzwingen; dann lehnt die API eine unverschlüsselte Verbindung
ab. Die Sicherung übernimmt diese Einstellung.

Ob der Server die Datenbank erreicht, prüfst du auf dem Server, sobald `deploy/.env` ausgefüllt ist:

```bash
docker compose --env-file .env run --rm backup check
```

Die Ausgabe nennt Host, Port, Datenbank und Benutzer, nie das Passwort, und dann `accepting connections`. Steht dort
`no response`, kommt der Container nicht durch. Dann der Reihe nach prüfen: Antwortet die Datenbank vom Server selbst
aus (`nc -zv 100.x.y.z 5432`)? Wenn ja, läuft Tailscale auf dem Server vielleicht ohne eigenes Netzgerät
(`tailscale status` und `ip addr show tailscale0`; mit `--tun=userspace-networking` erreichen Container das Tailnet
nicht). Wenn nein, liegt es an Punkt 2 bis 4 auf deinem PostgreSQL-Server.

Die Sicherung braucht ein `pg_dump`, das mindestens so neu ist wie dein PostgreSQL-Server; im Container ist es Version
18. Ist dein Server älter, ist das kein Problem.

## 4. Die Umgebungsdatei

Alle Einstellungen und Geheimnisse stehen in `deploy/.env` auf dem Server, und nur dort. Die Datei kommt nie ins
Repository (`deploy/.gitignore` schließt sie aus).

```bash
cp .env.example .env
chmod 600 .env
nano .env
```

Jede Einstellung geht in zwei Schreibweisen: in der von .NET, wie sie auch in `appsettings` steht
(`Duofy__Mail__Host`, `Duofy__PublicUrl`, `Duofy__SetupCode`, `Duofy__Mail__Password`, `Duofy__TrustedProxies` und so
weiter), oder unter dem alten Namen aus `.env.example` (`DUOFY_MAIL_HOST`, `DUOFY_PUBLIC_URL`, ...). Die Reihenfolge ist
fest: Steht die Schreibweise von .NET in der `.env`, gilt sie; sonst der alte Name; sonst der Standard aus
`deploy/docker-compose.yml`. Den Absendernamen nimmt Compose auch als `Duofy__Mail__From__Name`. Jeder Container bekommt
nur seine eigenen Einstellungen, nicht die ganze `.env`.

Was hinein muss:

| Einstellung | Wert |
| --- | --- |
| `PLAGEVO_VERSION`, `PLAGEVO_BUILD_DATE` | die Version, die du baust, und die Zeit des Baus; landen als Labels im Image |
| `ConnectionStrings__Duofy` | die Verbindung zur Datenbank aus Schritt 3; ohne sie bricht Compose mit einer Meldung ab |
| `DUOFY_HOSTED` | `true`; dann gibt es Mails, und die API verlangt https und Mails mit TLS |
| `DUOFY_PUBLIC_URL` | `https://portal.plagevo.de`; dahin zeigen die Links in Mails und Einladungen |
| `DUOFY_REGISTRATION_MODE` | `invite` für die erste Version mit Testern |
| `DUOFY_SETUP_CODE` | nur für den ersten Start, siehe Schritt 7 |
| `DUOFY_IMPRINT_FILE` und so weiter | die Rechtstexte, siehe Schritt 6 |
| `DUOFY_MAIL_*` | die Mail, siehe Schritt 5 |
| `PLAGEVO_NETWORK` | der Name des eigenen Netzes, `plagevo`; nur ändern, wenn der Name schon vergeben ist, dann auch beim Tunnel |
| `TRUSTED_PROXIES` | `172.30.10.2/32`, die Adresse des Tunnels; so steht es schon im Beispiel |
| `TUNNEL_TOKEN` | nur in Fall B; in Fall A weglassen oder leer lassen |
| `CRISP_WEBSITE_ID` | Website-ID des Crisp-Chats (Schritt 2.6); leer heißt: kein Chat, keine Frage nach der Einwilligung |
| `CLOUDFLARE_ANALYTICS_TOKEN` | Token der Web Analytics aus der manuellen Einrichtung (Schritt 2.5); leer heißt: keine Statistik, keine Frage nach der Einwilligung |
| `BACKUP_UID`, `BACKUP_GID` | dein Benutzer auf dem Server (`id -u`, `id -g`); ihm gehört der Ordner der Sicherungen |

`TRUSTED_PROXIES` ist wichtig: Nur von dieser Adresse darf eine Anfrage sagen, von welcher Adresse die Person kommt
(`CF-Connecting-IP`). Passt sie nicht zum Tunnel, sieht die API alle App-Nutzer als eine Adresse, und zehn Anmeldungen
pro Minute gelten für alle zusammen.

`docker compose config` zeigt die ganze Einstellung mit allen Passwörtern im Klartext; die Ausgabe nie weitergeben.
`docker compose config --quiet` prüft nur und zeigt nichts.

## 5. Mails über das Absender-Postfach

Plagevo verschickt Mails (Adresse bestätigen, Passwort vergessen, Einladungen) über das Postfach `<absender-adresse>`.
Das Postfach liegt bei netcup; so sieht es dann in der `.env` aus:

```text
DUOFY_MAIL_HOST=mx2f38.netcup.net
DUOFY_MAIL_PORT=465
DUOFY_MAIL_USER=<absender-adresse>
DUOFY_MAIL_PASSWORD=<das Passwort des Postfachs>
DUOFY_MAIL_FROM=<absender-adresse>
DUOFY_MAIL_FROM_NAME=Plagevo
DUOFY_MAIL_USE_TLS=true
```

Oder in der Schreibweise von .NET: `Duofy__Mail__Host=mx2f38.netcup.net`, `Duofy__Mail__Port=465` und so weiter.

Port 465 verschlüsselt von Anfang an (SSL/TLS), jeder andere Port (meist 587) mit STARTTLS. `DUOFY_MAIL_USE_TLS` bleibt
`true`: Mit `false` startet die gehostete Version nicht, und das Log nennt die Einstellung. `DUOFY_MAIL_FROM_NAME` ist
der Name, den Mailprogramme neben der Adresse zeigen; ohne ihn steht dort "Plagevo". Das Passwort steht nur in der `.env`
auf dem Server.

### 5.1 Erinnerungen auf dem Handy und am PC

Plagevo kann an Posten erinnern, die man von Hand bezahlt (Überweisung, Abhebung, Sonstige): am Morgen am Tag der
Fälligkeit, einige Tage vorher (Vorwarnung, 1 bis 7 Tage, voreingestellt 2), jeden Morgen, solange ein Posten überfällig und
nicht abgehakt ist, und am Abend noch einmal für die Posten von heute, die noch offen sind. Dazu kommt abends eine
Zusammenfassung dessen, was im Haushalt geschah. Jede Person stellt das selbst auf der Seite "Benachrichtigungen" ein: vier
Schalter (Heute fällig, Überfällig, Vorwarnung, Haushalt), die Uhrzeit am Morgen (voreingestellt 09:00) und am Abend (19:00,
abschaltbar). "Später" geht höchstens dreimal je Posten und Fälligkeitstag; danach bleibt nur "Bezahlt" und der Plan zeigt
den Posten als überfällig. Der Server prüft das jede Minute und rechnet in Berliner Zeit. Dafür
schickt der Server ein stilles Signal an Android über Firebase Cloud Messaging (FCM) und an Windows über die Windows Push
Notification Services (WNS). Im Signal stehen nur eine zufällige Kennung und die Art (fällig, Haushalt, zurückziehen), nie ein Name oder
ein Betrag; den Text mit Name und Betrag ("Miete 850 € ist heute fällig") baut die App danach mit Anmeldung aus dem, was der
Server liefert, und er steht auf dem Gerät, auch auf dem Sperrbildschirm, solange Android es nicht verbirgt (Einstellung
"Vertrauliche Inhalte"). Die Erinnerungen gibt es nur auf der gehosteten Version und sind aus, bis du sie einschaltest; wer sie
einschaltet, hat für jede Person zunächst alle vier Schalter an. Die Zugangsdaten stehen nur auf dem Server, nie im Repository.

Die Einstellungen (in der `.env`, Schreibweise von .NET; hier ohne Werte):

| Einstellung | Bedeutung |
| --- | --- |
| `Duofy__Reminders__Enabled` | `true` schaltet die Erinnerungen ein; sonst antworten ihre Wege mit 404, der Job sendet nichts, und die Einstellungen zeigen sie nicht |
| `Duofy__Reminders__Fcm__ServiceAccountFile` | Pfad der Dienstkonto-Datei von Firebase im Container, zum Beispiel `/push/firebase.json` |
| `Duofy__Reminders__Wns__ClientId` | die Paket-SID der Store-App (`ms-app://s-1-15-2-...`) aus dem Partner Center |
| `Duofy__Reminders__Wns__ClientSecret` | das Anwendungsgeheimnis dazu aus dem Partner Center |

**Firebase (Android).** Lege im Firebase-Konsolenprojekt eine Android-App mit dem Paketnamen `de.tomschorn.duofy` an. Zwei
Dateien entstehen, beide sind geheim und bleiben außerhalb des Repositorys:

1. Das Dienstkonto für den Server (Projekteinstellungen, Dienstkonten, neuen privaten Schlüssel erzeugen): Lege die Datei in
   `deploy/push/firebase.json` auf den Server, mache den Benutzer des Containers (`APP_UID`, 1654) zum Besitzer und setze
   die Rechte auf 600 (`sudo chown 1654 deploy/push/firebase.json && sudo chmod 600 deploy/push/firebase.json`), und setze
   `Duofy__Reminders__Fcm__ServiceAccountFile=/push/firebase.json`. Gehört die Datei root mit 600, kann der Server sie nicht
   lesen, und es kommt kein Signal.
   Der Ordner `deploy/push` ist von Git ausgeschlossen und wird nur lesbar als `/push` eingehängt.
2. Die `google-services.json` für die App: Lege sie vor dem Bauen in `Mobile/Duofy/`. Nur wenn die Datei dort liegt, baut das
   Projekt Firebase ein; ohne sie läuft die App, nur ohne Erinnerungen. Sie wird nie eingecheckt (`.gitignore`).

**WNS (Windows).** Die App braucht die Identität aus dem Microsoft Store (siehe `docs/store/README.md`): Paket-SID und
Geheimnis stehen im Partner Center unter der App, Produktidentität und "Windows Push Notification Services". Die unverpackte
Testversion am PC hat keine Identität und bekommt keine Erinnerungen. Ob ein Signal die ganz geschlossene Windows-App
erreicht, hängt am Store-Build mit Hintergrundaufgabe und ist vor der Veröffentlichung auf Staging zu prüfen.

Ein Druck auf "Bezahlt" oder "Später" in der Benachrichtigung bei ganz geschlossener App geht verloren: Die App holt die
Anmeldung erst beim Start. Das ist vorerst so hingenommen.

**Datenbank (Spec 118).** Die Migration `NotificationSettings` ersetzt die Einstellung "Erinnerungen an/aus" durch die vier
Schalter, zwei Uhrzeiten und die Vorwarnung und legt Tabellen für das Zählen von "Später" (`postpone_counts`) und für das
Protokoll der Haushaltsereignisse (`household_events`) an. Sie übernimmt die alten Einstellungen: wer die Erinnerungen an
hatte, behält seine Uhrzeit als Uhrzeit am Morgen und hat alle Schalter an, wer sie aus hatte, hat alle aus. Sie wird nur vom
Betreiber eingespielt (`dotnet ef database update` oder der Migrationsdienst), nie von der Entwicklung gegen die echte
Datenbank; Zurücknehmen geht und löscht die neuen Erinnerungsarten und die Zusammenfassungen.

**Prüfen.** Nach dem Einschalten: `docker compose up -d api`, dann `docker compose logs api | grep Reminders`. Prüfe auch die
Dienstkonto-Datei: `ls -ln deploy/push/firebase.json` muss den Besitzer 1654 und die Rechte `-rw-------` zeigen, sonst
kann der Server sie nicht lesen. Fehlt ein
Zugang, steht dort `nicht verschickt: FCM ist nicht eingerichtet` (oder WNS) mit der Kennung der Erinnerung, aber nie mit einem
Token. Der Datenschutztext (Teil von Issue 88) nennt Firebase Cloud Messaging und die Windows Push Notification Services als
Auftragsverarbeiter, was sie bekommen (das Push-Token des Geräts und die zufällige Kennung) und die Vereinbarungen.

## 6. Rechtstexte

Impressum, Datenschutzerklärung und Nutzungsbedingungen sind einfache Textdateien im Ordner `deploy/legal` auf dem
Server; die API sieht ihn als `/legal`, nur lesend. Der Ordner kommt nicht ins Repository.

```bash
mkdir -p legal
nano legal/impressum.txt
nano legal/datenschutz.txt
nano legal/nutzungsbedingungen.txt
```

In die Datenschutzerklärung gehören die Abschnitte zu Cloudflare aus `docs/rechtstexte/datenschutz-cloudflare.txt`.
Die mit "OFFEN" markierten Stellen musst du vorher ausfüllen oder streichen.

Die Namen müssen zu `DUOFY_IMPRINT_FILE`, `DUOFY_PRIVACY_FILE` und `DUOFY_TERMS_FILE` passen. Ein Text, der fehlt, leer
oder größer als 200 KB ist, gilt als nicht vorhanden; das Log nennt dann seinen Namen.

## 7. Erster Start mit dem Einrichtungscode

Beim ersten Start gibt es noch keinen Administrator. Den macht der Einrichtungscode.

1. Einen Code erzeugen und als `DUOFY_SETUP_CODE` in die `.env` schreiben (mindestens 16 Zeichen), dazu den Ordner der
   Sicherungen anlegen:

   ```bash
   openssl rand -base64 24
   mkdir -p backups && chmod 700 backups
   ```

2. Prüfen, ob die Datenbank erreichbar ist (Schritt 3):

   ```bash
   docker compose --env-file .env run --rm backup check
   ```

3. Bauen und starten. Das erste Bauen dauert einige Minuten. Das legt auch das Netz `plagevo` an:

   ```bash
   docker compose --env-file .env up -d --build
   ```

   In Fall B (eigener Tunnel) zusätzlich mit `--profile own-tunnel`, hier und bei jedem späteren Befehl, der alle
   Dienste anfasst.

4. In Fall A danach den Tunnel starten, damit er dem Netz beitritt (Schritt 2.2):

   ```bash
   cd <tunnel-ordner> && docker compose up -d && cd <installationsordner>/deploy
   ```

5. Warten, bis alles gesund ist; `docker compose ps` zeigt bei `api` und `web` "healthy":

   ```bash
   docker compose --env-file .env ps
   ```

6. Im Browser `https://portal.plagevo.de/registrieren` öffnen. Solange es keinen Administrator gibt, zeigt die Seite das
   Feld "Einrichtungscode". Mit dem Code registrieren; dann kommt eine Mail an die eigene Adresse, und der Link darin
   bestätigt sie. Danach anmelden: Das Konto ist Administrator.

7. Den Code aus der `.env` löschen (`DUOFY_SETUP_CODE=`) und die API neu starten, damit er nicht mehr gilt:

   ```bash
   docker compose --env-file .env up -d api
   ```

Testerinnen und Tester lädt der Administrator danach auf der Seite "Administration" ein; ohne Einladung kann sich im
Modus `invite` niemand registrieren.

## 8. Prüfen, ob alles läuft

```bash
docker compose --env-file .env ps
docker compose --env-file .env logs --tail=50 api
docker network inspect plagevo --format '{{range .Containers}}{{.Name}} {{.IPv4Address}}{{"\n"}}{{end}}'
curl -s https://api.plagevo.de/api/v1/health
```

Der dritte Befehl listet die Container im Netz `plagevo` mit ihren Adressen; `cloudflared` muss mit `172.30.10.2/24`
dabei sein. Der letzte Befehl antwortet mit `Healthy`. Bricht die API beim Start ab, steht im Log, welche Einstellung
fehlt oder unsicher ist, zum Beispiel "Gehosteter Server ohne Einstellung Duofy:Mail:Host" oder "Gehosteter Server mit
unverschlüsselter Einstellung Duofy:PublicUrl".

Ob die Web-Variante auch wirklich benutzbar ist, prüft danach ein eigenes Skript, nach jedem Start und jedem Update:

```bash
sh check-web.sh
```

Es fragt `https://portal.plagevo.de/health` und `https://portal.plagevo.de/_framework/blazor.web.js` ab (eine andere
Adresse als erstes Argument, zum Beispiel `sh check-web.sh http://172.30.10.12:8080` auf dem Server selbst) und endet
mit "ist benutzbar". Fehlt das Skript `blazor.web.js`, lädt jede Seite, aber kein Knopf reagiert; der HEALTHCHECK des
Containers merkt das nicht, darum diese zweite Prüfung.

Der Speicher: `docker stats --no-stream` zeigt, was jeder Container braucht.

## 9. Update

1. Vorher sichern (siehe Schritt 10):

   ```bash
   docker compose --env-file .env run --rm backup once
   ```

2. Den neuen Stand holen und in der `.env` `PLAGEVO_VERSION` und `PLAGEVO_BUILD_DATE` anpassen:

   ```bash
   git pull
   nano .env
   ```

3. Neu bauen und starten. Neue Migrationen der Datenbank spielt die API beim Start selbst ein:

   ```bash
   docker compose --env-file .env up -d --build
   ```

4. Prüfen, ob die Web-Variante benutzbar ist (Schritt 8); das Skript muss mit "ist benutzbar" enden:

   ```bash
   sh check-web.sh
   ```

5. Alte Images und den Cache des Bauens entfernen; die Platte des Servers ist klein:

   ```bash
   docker image prune -f
   docker builder prune -f
   ```

In der App bleibt die Anmeldung über ein Update erhalten: Die Schlüssel der Tokens liegen im Volume `keys`. Im Browser
meldet ein Neustart der Web-Variante ab, denn ihre Sitzungen liegen im Arbeitsspeicher. Das Volume `web-keys` hält nur
die Schlüssel für Formulare und den Zustand der Seiten. Wer `keys` lesen kann, kann Tokens fälschen; beide Volumes
bleiben auf dem Server und kommen in keine Sicherung nach außen.

Reicht der Arbeitsspeicher beim Bauen nicht (der Bau bricht mit "Killed" ab), vorher die Web-Variante anhalten:
`docker compose --env-file .env stop web`, dann wie oben bauen und starten.

`docker compose down` will auch das Netz `plagevo` entfernen. Solange `cloudflared` daran hängt, geht das nicht, und
Compose meldet "network plagevo has active endpoints"; das schadet nicht, das Netz bleibt einfach stehen. Muss das Netz
wirklich neu entstehen, erst den Tunnel anhalten (`cd <tunnel-ordner> && docker compose down`), dann Plagevo
`down` und `up`, dann den Tunnel wieder `up -d`.

## 10. Sicherung

Der Dienst `backup` sichert die Datenbank, die `ConnectionStrings__Duofy` nennt, jeden Tag um `BACKUP_TIME` (Standard
03:00, deutsche Zeit) als `deploy/backups/duofy-JJJJ-MM-TT.sql.gz` auf dem Server. Er holt die Daten über das Netz, also
über Tailscale von deinem PostgreSQL-Server. Beim Start sichert er sofort, wenn es für heute noch keine Sicherung gibt.
Er hebt 14 Tage auf (`BACKUP_KEEP_DAYS`): heute und die 13 Tage davor; ältere Dateien löscht er nach dem Datum im Namen.

```bash
ls -l backups/
docker compose --env-file .env logs --tail=20 backup
```

Die Dateien sind nur für `BACKUP_UID` lesbar, denn sie enthalten alle Konten mit Passwort-Hashes; den Ordner selbst
ebenso schützen: `chmod 700 backups`.

Eine Sicherung sofort, zum Beispiel vor einem Update:

```bash
docker compose --env-file .env run --rm backup once
```

So liegen Datenbank und Sicherungen an zwei Orten: die Daten bei dir zu Hause, die Sicherungen auf dem Server. Eine
Kopie zu dir holst du zum Beispiel mit `scp` über Tailscale:

```bash
scp <benutzer>@<server-tailnet-ip>:<installationsordner>/deploy/backups/duofy-2026-10-26.sql.gz .
```

## 11. Wiederherstellen

So spielst du eine Sicherung zurück. Alles, was seit der Sicherung geschah, geht dabei verloren.

Auf dem Server Web-Variante und API anhalten, damit niemand schreibt:

```bash
docker compose --env-file .env stop web api
```

Auf deinem PostgreSQL-Server zu Hause die Sicherung holen, die Datenbank leer neu anlegen und die Sicherung einspielen
(Datum anpassen):

```bash
scp <benutzer>@<server-tailnet-ip>:<installationsordner>/deploy/backups/duofy-2026-10-26.sql.gz .
sudo -u postgres dropdb plagevo
sudo -u postgres createdb --owner=plagevo plagevo
gunzip -c duofy-2026-10-26.sql.gz | psql -h localhost -U plagevo -d plagevo --quiet --set ON_ERROR_STOP=1
```

Die Sicherung enthält keine Besitzer und Rechte (`--no-owner --no-privileges`); alles gehört danach dem Benutzer, der
einspielt, also `plagevo`. Dann auf dem Server wieder starten:

```bash
docker compose --env-file .env start api web
```

Zum Üben ohne Risiko lässt sich eine Sicherung auch in eine zweite, leere Datenbank einspielen und dort ansehen:

```bash
sudo -u postgres createdb --owner=plagevo probe
gunzip -c duofy-2026-10-26.sql.gz | psql -h localhost -U plagevo -d probe --quiet --set ON_ERROR_STOP=1
psql -h localhost -U plagevo -d probe -c "select count(*) from users"
sudo -u postgres dropdb probe
```

Mit der eigenen Datenbank aus `own-db.yml` (Schritt 12) läuft dasselbe im Container `db`, mit den Namen aus
`POSTGRES_DB` und `POSTGRES_USER` statt `plagevo`:

```bash
docker compose --env-file .env stop web api
docker compose --env-file .env exec -T db dropdb -U duofy duofy
docker compose --env-file .env exec -T db createdb -U duofy duofy
gunzip -c backups/duofy-2026-10-26.sql.gz | docker compose --env-file .env exec -T db psql -U duofy -d duofy --quiet --set ON_ERROR_STOP=1
docker compose --env-file .env start api web
```

Getestet am 26.10.2026 von Hand, mit einer lokalen Kopie des Aufbaus mit eigener Datenbank: Sicherung geschrieben, in
eine leere Datenbank eingespielt (dieselben Konten und alle 14 Migrationen da), dann der ganze Ablauf oben mit Leeren
und Einspielen der echten Datenbank; danach meldete sich das Konto aus der Sicherung wieder an.

## 12. Eigene Datenbank im Compose

Wer keinen eigenen PostgreSQL-Server hat, zum Beispiel auf einem frischen Server oder beim Selbstbetrieb von Duofy,
startet die Datenbank mit `deploy/own-db.yml` dazu. Die Datenbank (PostgreSQL 18, Volume `db-data`, 512 MB) hängt dann
nur im internen Netz `plagevo-backend` (`172.30.11.0/24`) ohne Weg nach draußen: `db` an `172.30.11.10`, dazu `api` an
`172.30.11.11` und `backup` an `172.30.11.13`. Der Tunnel tritt diesem Netz nie bei und sieht die Datenbank nicht.

In der `.env`:

```text
POSTGRES_DB=duofy
POSTGRES_USER=duofy
POSTGRES_PASSWORD=<langes Zufallspasswort>
ConnectionStrings__Duofy=Host=172.30.11.10;Port=5432;Database=duofy;Username=duofy;Password=<dasselbe Passwort>
COMPOSE_FILE=docker-compose.yml:own-db.yml
```

Mit `COMPOSE_FILE` nimmt jeder `docker compose`-Befehl in diesem Ordner beide Dateien, auch alle Befehle dieser
Anleitung. Ohne diese Zeile gehört zu jedem Befehl `-f docker-compose.yml -f own-db.yml`. Die Variablen `POSTGRES_*`
gelten nur für diese eigene Datenbank.

## 13. Wenn etwas nicht geht

| Zeichen | Ursache und Abhilfe |
| --- | --- |
| `required variable ConnectionStrings__Duofy is missing a value` | Die Verbindung zur Datenbank fehlt in `.env` (Schritt 3 und 4). |
| `backup check` zeigt `no response` | Der Server erreicht die Datenbank nicht: Tailscale, `listen_addresses`, `pg_hba.conf` und Firewall prüfen (Schritt 3). |
| Die API startet nicht | `docker compose --env-file .env logs api`; die letzte Zeile nennt die fehlende oder unsichere Einstellung. Bei "password authentication failed" passt das Passwort in der Verbindung nicht, bei "no pg_hba.conf entry" fehlt die Zeile aus Schritt 3. |
| `network plagevo declared as external, but could not be found` (beim Tunnel) | Plagevo lief noch nicht; erst Plagevo starten, dann den Tunnel (Schritt 2.2). |
| Mit `--profile own-tunnel` startet der Dienst `tunnel` immer wieder neu | `TUNNEL_TOKEN` fehlt in `.env` oder ist falsch (Fall B in Schritt 2.2). Compose prüft das Token nicht, damit Fall A ohne auskommt; `docker compose --env-file .env --profile own-tunnel logs tunnel` nennt den Grund. |
| Cloudflare zeigt "Bad gateway" (502) | Der Tunnel erreicht `web` oder `api` nicht: Sind beide "healthy", hängt `cloudflared` mit `172.30.10.2` im Netz `plagevo` (Schritt 8), und zeigen die Einträge im Dashboard auf `172.30.10.12:8080` und `172.30.10.11:8080`? |
| `Pool overlaps with other one on this address space` | Das Subnetz `172.30.10.0/24` ist auf diesem Server schon vergeben: `docker network ls` und `docker network inspect` zeigen, von wem. |
| Die Seiten laden, aber kein Knopf reagiert; `check-web.sh` meldet `/_framework/blazor.web.js` mit HTTP 404 | Dem Image fehlt das Skript von Blazor. Den neuesten Stand holen und `web` neu bauen: `docker compose --env-file .env build --no-cache web`, dann `up -d web` und `sh check-web.sh`. |
| Viele Anmeldungen der App enden mit 429 | `TRUSTED_PROXIES` passt nicht zur Adresse des Tunnels (Schritt 4), oder der Tunnel hat nicht `172.30.10.2`. |
| `backup` schreibt nichts, "Permission denied" | Der Ordner `backups` gehört nicht `BACKUP_UID`: `sudo chown "$(id -u):$(id -g)" backups`. |
| Nach einem Update ist jeder auch in der App abgemeldet | Das Volume `keys` ist verloren gegangen, zum Beispiel durch `docker compose down -v`. Dann einmal neu anmelden; `-v` nie benutzen, mit `own-db.yml` löscht es auch die Datenbank. |
