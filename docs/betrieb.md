# Plagevo betreiben

Diese Anleitung beschreibt Schritt für Schritt, wie Plagevo auf dem eigenen Server läuft: hinter einem Cloudflare
Tunnel, ohne einen offenen Port außer SSH. Plagevo ist die gehostete Version von Duofy ("Plagevo by Duofy"); der Code
heißt weiter Duofy. Die Anforderungen stehen in [docs/specs/79-operations](specs/79-operations).

Du brauchst diese Anleitung selten, deshalb steht jeder Schritt ausgeschrieben da, auch die Befehle.

## Überblick

Auf dem Server laufen fünf Container, alle aus `deploy/docker-compose.yml`:

| Dienst | Aufgabe | Speichergrenze |
| --- | --- | --- |
| `db` | PostgreSQL 18 mit den Daten, auf dem Volume `db-data` | 512 MB |
| `api` | Die Duofy-API, erreichbar als `api.plagevo.de` | 384 MB |
| `web` | Die Web-Variante, erreichbar als `app.plagevo.de` | 512 MB |
| `backup` | Sichert die Datenbank jeden Tag nach `deploy/backups` | 128 MB |
| `cloudflared` | Der Tunnel zu Cloudflare; läuft schon auf dem Server, gehört nicht zu diesem Compose | – |

Der Weg einer Anfrage: Der Browser oder die App spricht https mit Cloudflare. Cloudflare schickt die Anfrage durch den
Tunnel zum Container `cloudflared` auf dem Server. Der leitet `app.plagevo.de` an `http://web:8080` und `api.plagevo.de`
an `http://api:8080` weiter. Dafür treten `web` und `api` dem Docker-Netz des Tunnels bei
(`infrastructure_cloudflared_network`). Die Datenbank hängt nur in einem internen Netz und ist von außen nicht erreichbar.

Die Web-Variante fragt die API intern über ein eigenes Netz (`app`) unter der festen Adresse `172.30.79.3`. Die API nimmt
diese Adresse von ihrer eigenen Bremse für Anmeldungen aus, denn sonst teilten sich alle Web-Nutzer ein Budget. Vor der
Web-Variante bremst deshalb die Regel bei Cloudflare (Schritt 2.3).

## 1. Was du brauchst

- den Server mit Docker und dem Compose-Plugin (`docker compose version` zeigt eine Version 2)
- den laufenden Container `cloudflared` im Netz `infrastructure_cloudflared_network`, oder einen frischen Server (siehe
  Schritt 2.2, Fall B)
- die Domain `plagevo.de` in Cloudflare (kostenloser Plan reicht)
- das Postfach `noreply@example.org` mit seinen SMTP-Zugangsdaten
- dieses Repository auf dem Server, zum Beispiel unter `/opt/plagevo`:

  ```bash
  git clone https://github.com/tom-schorn/Duofy.git /opt/plagevo
  cd /opt/plagevo/deploy
  ```

Alle weiteren Befehle laufen im Ordner `deploy`.

## 2. Cloudflare einrichten

Alles hier geht im Dashboard von Cloudflare unter [dash.cloudflare.com](https://dash.cloudflare.com), mit dem
kostenlosen Plan.

### 2.1 Domain

`plagevo.de` muss in Cloudflare als Domain eingetragen sein, und beim Registrar müssen die Nameserver von Cloudflare
stehen. Das Dashboard zeigt dann bei der Domain den Status "Active". Die Einträge für `app` und `api` legt der Tunnel im
nächsten Schritt selbst an.

### 2.2 Tunnel

**Fall A: Der Tunnel läuft schon (so ist es auf dem Server).** Im Dashboard unter Zero Trust, Networks, Tunnels den
vorhandenen Tunnel öffnen, dann den Reiter "Public Hostname" (je nach Version "Published application routes"). Zwei
Einträge anlegen:

| Subdomain | Domain | Typ | URL |
| --- | --- | --- | --- |
| `app` | `plagevo.de` | HTTP | `web:8080` |
| `api` | `plagevo.de` | HTTP | `api:8080` |

Cloudflare legt dazu die DNS-Einträge `app.plagevo.de` und `api.plagevo.de` an. Der Typ ist HTTP, nicht HTTPS: Die
Verschlüsselung macht Cloudflare, im Tunnel ist die Verbindung ohnehin geschützt.

Dann das Subnetz des Tunnel-Netzes nachsehen; es kommt in Schritt 3 in die Umgebungsdatei:

```bash
docker network inspect infrastructure_cloudflared_network --format '{{(index .IPAM.Config 0).Subnet}}'
```

**Fall B: Ein frischer Server ohne Tunnel.** Im Dashboard unter Zero Trust, Networks, Tunnels einen neuen Tunnel vom Typ
"Cloudflared" anlegen. Bei "Install connector" steht ein Befehl mit einem langen Token nach `--token`; nur dieses Token
kommt als `TUNNEL_TOKEN` in die Umgebungsdatei. Die beiden Einträge `app` und `api` wie in Fall A anlegen. Auf dem
Server einmal das Netz anlegen, das der Tunnel und Plagevo teilen:

```bash
docker network create infrastructure_cloudflared_network
```

Beim Start (Schritt 6) kommt dann `--profile own-tunnel` dazu; das startet den Dienst `tunnel` aus der Compose-Datei.

### 2.3 Die eine Regel gegen zu viele Anfragen

Der kostenlose Plan erlaubt genau eine Regel. Unter der Domain `plagevo.de`, Security, WAF, Reiter "Rate limiting rules",
eine Regel anlegen:

- Name: `Plagevo Anmeldung`
- Wenn eingehende Anfragen passen, als Ausdruck ("Edit expression"):

  ```text
  (starts_with(http.request.uri.path, "/api/v1/auth/")) or (http.host eq "app.plagevo.de" and starts_with(http.request.uri.path, "/_blazor"))
  ```

- Mit denselben Eigenschaften: IP (beim kostenlosen Plan fest)
- Wenn die Rate übersteigt: 10 Anfragen in 10 Sekunden
- Dann: Block, für 10 Sekunden

Der erste Teil bremst die App, die direkt mit `api.plagevo.de` spricht. Der zweite Teil bremst, wie oft jemand eine
Verbindung zur Web-Variante aufbaut. Wichtig: Die Web-Variante ist von der Bremse der API ausgenommen, diese Regel ist für
sie also die einzige Grenze vor dem Server, und sie muss `app.plagevo.de` abdecken. Was innerhalb einer schon offenen
Verbindung der Web-Variante passiert, zum Beispiel mehrere Anmeldeversuche nacheinander, sieht Cloudflare nicht. Dann
greift nur noch die Sperre eines Kontos nach fünf falschen Passwörtern für fünf Minuten.

### 2.4 Turnstile

Turnstile (der Schutz der Formulare vor Bots) ist auf Version 2 verschoben: Die erste Version läuft im Modus
`invite`, ohne Einladung registriert sich niemand. Jetzt nichts einrichten.

### 2.5 Web Analytics

Web Analytics zählt Seitenaufrufe ohne Cookies, deshalb braucht es keinen Cookie-Hinweis. Im Dashboard unter Analytics
& Logs, Web Analytics, "Add a site": als Hostname `app.plagevo.de` eintragen und die automatische Einrichtung
ausschalten, falls sie angeboten wird. Cloudflare zeigt dann einen Schnipsel mit `data-cf-beacon='{"token": "..."}'`;
nur den Wert hinter `token` in die Umgebungsdatei als `CLOUDFLARE_ANALYTICS_TOKEN` übernehmen. Ohne Token gibt es keine
Zählung.

## 3. Die Umgebungsdatei

Alle Einstellungen und Geheimnisse stehen in `deploy/.env` auf dem Server, und nur dort. Die Datei kommt nie ins
Repository (`deploy/.gitignore` schließt sie aus).

```bash
cp .env.example .env
chmod 600 .env
nano .env
```

Was hinein muss:

| Einstellung | Wert |
| --- | --- |
| `PLAGEVO_VERSION`, `PLAGEVO_BUILD_DATE` | die Version, die du baust, und die Zeit des Baus; landen als Labels im Image |
| `POSTGRES_PASSWORD` | ein langes Zufallspasswort, zum Beispiel aus `openssl rand -base64 32` |
| `DUOFY_HOSTED` | `true`; dann gibt es Mails, und die API verlangt https und Mails mit TLS |
| `DUOFY_PUBLIC_URL` | `https://app.plagevo.de`; dahin zeigen die Links in Mails und Einladungen |
| `DUOFY_REGISTRATION_MODE` | `invite` für die erste Version mit Testern |
| `DUOFY_SETUP_CODE` | nur für den ersten Start, siehe Schritt 6 |
| `DUOFY_IMPRINT_FILE` und so weiter | die Rechtstexte, siehe Schritt 5 |
| `DUOFY_MAIL_*` | die Mail, siehe Schritt 4 |
| `CLOUDFLARED_NETWORK` | das Netz des Tunnels, `infrastructure_cloudflared_network` |
| `TRUSTED_PROXIES` | das Subnetz aus Schritt 2.2, zum Beispiel `172.18.0.0/16` |
| `TUNNEL_TOKEN` | nur in Fall B |
| `CLOUDFLARE_ANALYTICS_TOKEN` | aus Schritt 2.5, oder leer |
| `BACKUP_UID`, `BACKUP_GID` | dein Benutzer auf dem Server (`id -u`, `id -g`); ihm gehört der Ordner der Sicherungen |

`TRUSTED_PROXIES` ist wichtig: Nur aus diesem Netz darf eine Anfrage sagen, von welcher Adresse die Person kommt
(`CF-Connecting-IP`). Fehlt es, sieht die API alle App-Nutzer als eine Adresse, und zehn Anmeldungen pro Minute gelten
für alle zusammen. Das ganze Subnetz zu nennen ist nur sicher, solange in diesem Netz nichts außer dem Tunnel und
Plagevo hängt. Kommt ein weiterer Container dazu, dem Tunnel eine feste Adresse geben und nur diese nennen, zum Beispiel
`172.18.0.2/32`.

## 4. Mails über noreply@example.org

Plagevo verschickt Mails (Adresse bestätigen, Passwort vergessen, Einladungen) über das Postfach `noreply@example.org`.
Host und Port stehen in den Zugangsdaten des Anbieters für SMTP:

```text
DUOFY_MAIL_HOST=<SMTP-Server laut Anbieter>
DUOFY_MAIL_PORT=587
DUOFY_MAIL_USER=noreply@example.org
DUOFY_MAIL_PASSWORD=<das Passwort des Postfachs>
DUOFY_MAIL_FROM=noreply@example.org
DUOFY_MAIL_USE_TLS=true
```

Port 465 verschlüsselt von Anfang an, jeder andere Port (meist 587) mit STARTTLS. `DUOFY_MAIL_USE_TLS` bleibt `true`:
Mit `false` startet die gehostete Version nicht, und das Log nennt die Einstellung. Das Passwort steht nur in der
`.env` auf dem Server.

## 5. Rechtstexte

Impressum, Datenschutzerklärung und Nutzungsbedingungen sind einfache Textdateien im Ordner `deploy/legal` auf dem
Server; die API sieht ihn als `/legal`, nur lesend. Der Ordner kommt nicht ins Repository.

```bash
mkdir -p legal
nano legal/impressum.txt
nano legal/datenschutz.txt
nano legal/nutzungsbedingungen.txt
```

Die Namen müssen zu `DUOFY_IMPRINT_FILE`, `DUOFY_PRIVACY_FILE` und `DUOFY_TERMS_FILE` passen. Ein Text, der fehlt, leer
oder größer als 200 KB ist, gilt als nicht vorhanden; das Log nennt dann seinen Namen.

## 6. Erster Start mit dem Einrichtungscode

Beim ersten Start gibt es noch keinen Administrator. Den macht der Einrichtungscode.

1. Einen Code erzeugen und als `DUOFY_SETUP_CODE` in die `.env` schreiben (mindestens 16 Zeichen):

   ```bash
   openssl rand -base64 24
   mkdir -p backups && chmod 700 backups
   ```

2. Bauen und starten. Das erste Bauen dauert einige Minuten:

   ```bash
   docker compose --env-file .env up -d --build
   ```

   In Fall B (eigener Tunnel) zusätzlich mit `--profile own-tunnel`, hier und bei jedem späteren Befehl, der alle
   Dienste anfasst.

3. Warten, bis alles gesund ist; `docker compose ps` zeigt bei `db`, `api` und `web` "healthy":

   ```bash
   docker compose --env-file .env ps
   ```

4. Im Browser `https://app.plagevo.de/registrieren` öffnen. Solange es keinen Administrator gibt, zeigt die Seite das Feld
   "Einrichtungscode". Mit dem Code registrieren; dann kommt eine Mail an die eigene Adresse, und der Link darin bestätigt
   sie. Danach anmelden: Das Konto ist Administrator.

5. Den Code aus der `.env` löschen (`DUOFY_SETUP_CODE=`) und die API neu starten, damit er nicht mehr gilt:

   ```bash
   docker compose --env-file .env up -d api
   ```

Testerinnen und Tester lädt der Administrator danach auf der Seite "Administration" ein; ohne Einladung kann sich im
Modus `invite` niemand registrieren.

## 7. Prüfen, ob alles läuft

```bash
docker compose --env-file .env ps
docker compose --env-file .env logs --tail=50 api
curl -s https://api.plagevo.de/api/v1/health
```

Der letzte Befehl antwortet mit `Healthy`. Bricht die API beim Start ab, steht im Log, welche Einstellung fehlt oder
unsicher ist, zum Beispiel "Gehosteter Server ohne Einstellung Duofy:Mail:Host" oder "Gehosteter Server mit
unverschlüsselter Einstellung Duofy:PublicUrl".

Der Speicher: `docker stats --no-stream` zeigt, was jeder Container braucht. Im Leerlauf sind es zusammen gut 150 MB.

## 8. Update

1. Vorher sichern (siehe Schritt 9):

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

4. Alte Images und den Cache des Bauens entfernen; die Platte des Servers ist klein:

   ```bash
   docker image prune -f
   docker builder prune -f
   ```

Die Anmeldungen bleiben über ein Update erhalten: Die Schlüssel der Tokens liegen im Volume `keys`. Wer dieses Volume
lesen kann, kann Tokens fälschen; es bleibt auf dem Server und kommt in keine Sicherung nach außen.

Reicht der Arbeitsspeicher beim Bauen nicht (der Bau bricht mit "Killed" ab), vorher die Web-Variante anhalten:
`docker compose --env-file .env stop web`, dann wie oben bauen und starten.

## 9. Sicherung

Der Dienst `backup` sichert die Datenbank jeden Tag um `BACKUP_TIME` (Standard 03:00, deutsche Zeit) als
`deploy/backups/duofy-JJJJ-MM-TT.sql.gz`. Beim Start sichert er sofort, wenn es für heute noch keine Sicherung gibt.
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

Die Sicherungen liegen auf demselben Server wie die Datenbank. Gegen einen Ausfall des ganzen Servers hilft nur eine
Kopie woanders hin, zum Beispiel ab und zu mit `scp` auf den eigenen Rechner:

```bash
scp benutzer@server:/opt/plagevo/deploy/backups/duofy-2026-10-26.sql.gz .
```

## 10. Wiederherstellen

So spielst du eine Sicherung zurück. Alles, was seit der Sicherung geschah, geht dabei verloren.

```bash
# 1. Web-Variante und API anhalten, damit niemand schreibt
docker compose --env-file .env stop web api

# 2. Die Datenbank leeren: löschen und leer neu anlegen
docker compose --env-file .env exec -T db dropdb -U duofy duofy
docker compose --env-file .env exec -T db createdb -U duofy duofy

# 3. Die Sicherung einspielen (Datum anpassen)
gunzip -c backups/duofy-2026-10-26.sql.gz | docker compose --env-file .env exec -T db psql -U duofy -d duofy --quiet --set ON_ERROR_STOP=1

# 4. Wieder starten
docker compose --env-file .env start api web
```

Heißen Datenbank oder Benutzer anders (`POSTGRES_DB`, `POSTGRES_USER`), diese Namen statt `duofy` einsetzen.

Getestet am 26.10.2026 von Hand, mit einer lokalen Kopie dieses Aufbaus: Sicherung geschrieben, in eine leere
Datenbank eingespielt (dieselben Konten und alle 14 Migrationen da), dann der ganze Ablauf oben mit Leeren und
Einspielen der echten Datenbank; danach meldete sich das Konto aus der Sicherung wieder an.

Zum Üben ohne Risiko lässt sich eine Sicherung auch in eine zweite, leere Datenbank einspielen und dort ansehen:

```bash
docker compose --env-file .env exec -T db createdb -U duofy probe
gunzip -c backups/duofy-2026-10-26.sql.gz | docker compose --env-file .env exec -T db psql -U duofy -d probe --quiet --set ON_ERROR_STOP=1
docker compose --env-file .env exec -T db psql -U duofy -d probe -c "select count(*) from users"
docker compose --env-file .env exec -T db dropdb -U duofy probe
```

## 11. Wenn etwas nicht geht

| Zeichen | Ursache und Abhilfe |
| --- | --- |
| `network infrastructure_cloudflared_network declared as external, but could not be found` | Das Netz des Tunnels fehlt oder heißt anders: `docker network ls`, dann `CLOUDFLARED_NETWORK` anpassen; in Fall B das Netz anlegen (Schritt 2.2). |
| Cloudflare zeigt "Bad gateway" (502) | Der Tunnel erreicht `web` oder `api` nicht: Sind beide "healthy", und hängen sie im Netz des Tunnels? `docker network inspect infrastructure_cloudflared_network` listet die Container. |
| Die API startet nicht | `docker compose --env-file .env logs api`; die letzte Zeile nennt die fehlende oder unsichere Einstellung. |
| Viele Anmeldungen der App enden mit 429 | `TRUSTED_PROXIES` fehlt oder passt nicht zum Netz des Tunnels (Schritt 3). |
| `backup` schreibt nichts, "Permission denied" | Der Ordner `backups` gehört nicht `BACKUP_UID`: `sudo chown "$(id -u):$(id -g)" backups`. |
| Nach einem Update ist jeder abgemeldet | Das Volume `keys` ist verloren gegangen, zum Beispiel durch `docker compose down -v`. Dann einmal neu anmelden; `-v` nie benutzen, es löscht auch die Datenbank. |
