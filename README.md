# Duofy

**Eine Finanz-App, die plant statt trackt.**

[![CI](https://github.com/tom-schorn/Duofy/actions/workflows/ci.yml/badge.svg)](https://github.com/tom-schorn/Duofy/actions/workflows/ci.yml)
[![License: AGPL v3](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)

## Was ist Duofy

Duofy ist ein quelloffenes Haushaltsbuch für Haushalte in Deutschland. Die meisten
Haushaltsbücher fragen: *wo ist das Geld hin?* Duofy fragt vorher: *wo soll es
hin?* Aus deinen Verträgen entsteht der Plan für den Folgemonat. Du verteilst das
Geld auf Posten und hakst sie ab, sobald sie bezahlt sind — das Abhaken erzeugt die
Buchung. Nachverfolgen fällt als Nebenprodukt an, es ist nicht der Zweck.

Gebaut ist Duofy für **Paare und Familien**, also Erwachsene mit eigenem Einkommen.
Jede Person hat ihren eigenen Plan; der Haushaltsplan ist eine gemeinsame Sicht
darüber, keine eigene Tabelle. Der Haushalt besitzt nichts — kein Konto, keinen
Plan. Wer allein plant, kann Duofy genauso nutzen.

> **Erste Version.** Bis zum Erscheinen von 1.0 ändert sich noch etwas, und ein
> Update kann Handarbeit brauchen. Was schon läuft und was kommt, steht in der
> [Roadmap](https://github.com/tom-schorn/Duofy/wiki/Roadmap).

## Warum Duofy

**Richtlinien statt Konfiguration.** Duofy gibt eine Orientierung vor: 50 % vom
Verplanbaren für den Grundbedarf, 30 % für Wünsche, 20 % fürs Sparen. Das ist
eine Richtlinie und kein Zwang; man kann sie persönlich anpassen, aber niemand
muss sich erst durch Dutzende Budget-Schemata arbeiten, um anzufangen.

**Klare Definitionen statt Einstellungen.** Duofy ist auch für Menschen gedacht,
die klare Struktur brauchen. Im Zweifel gilt deshalb eine feste Regel statt einer
Option, und jedes Ding hat ein Wort: Frei ist, was noch keinem Posten zugeteilt
ist; Anstehend ist, was verplant, aber noch nicht abgehakt ist.

**Was eine Tabellenkalkulation nicht kann.** Duofy zeigt, auf welchem Konto wann im
Monat Geld fehlt — die Miete am 1. und das Gehalt am 28. gehen in der Summe auf,
im Verlauf nicht. Buchungen bleiben an den Plan gekoppelt, auch wenn sie ein
anderes Datum haben. Und der Haushaltsplan kann nicht auseinanderlaufen, weil jeder
Posten nur einmal existiert.

## Was Duofy kann

- **Monatsplan aus Verträgen:** Vertrag, Einnahme, Sparziel und Schuld werden beim
  Anlegen des Monats zu Posten; Verpflichtungen mit Häkchen, Limits ohne
- **50/30/20 als Richtlinie:** Grundbedarf, Wünsche, Sparen gegen die Quote, mit
  persönlicher Standardquote und einer Haushaltsquote
- **Sparziele:** ein erreichtes Ziel wird nicht mehr eingeplant
- **Haushaltsbuch:** Konten, Buchungen, Umbuchungen zwischen eigenen Konten,
  durchlaufende Posten
- **Verlauf mit Engpass-Hinweis** und optionalem **Monatsübertrag** als Startstand
- **Hinweise im Plan**, zum Beispiel für überfällige Posten
- **Haushalt mit Freigaben:** jede Person gibt pro Bereich (Planung, Verträge,
  Konten) frei, was die anderen sehen und ändern dürfen; ein Paar-Preset macht das
  in einem Schritt
- **Import** von Kontoauszügen als CSV und CAMT, mit Vorschlägen für die Zuordnung
- **Rückgängig** statt Rückfragen, Druckversion, Dark- und Light-Theme

## Voraussetzungen

- **Docker** mit **Compose v2** (der Befehl `docker compose`, nicht
  `docker-compose`)
- etwa **256 MB Arbeitsspeicher** — im Leerlauf sind es rund 100 MB, die drei
  Container sind zusammen auf 256 MB begrenzt
- ein **freier Port**, Vorgabe 8080
- nur wenn Duofy von außen erreichbar sein soll: eine **eigene Domain** und ein
  Reverse Proxy für HTTPS (Caddy, nginx proxy manager, Traefik oder ein
  Cloudflare-Tunnel; Duofy liefert keinen mit)

## Starten

```bash
mkdir duofy && cd duofy
curl -O https://raw.githubusercontent.com/tom-schorn/Duofy/main/docker-compose.yml
curl -O https://raw.githubusercontent.com/tom-schorn/Duofy/main/.env.example
cp .env.example .env
```

Zwei Werte in der `.env` sind Pflicht: ein Datenbankpasswort (`POSTGRES_PASSWORD`)
und `JWT_SECRET`, erzeugt mit `openssl rand -hex 32`. Für den Rest gibt es
Vorgaben, die den meisten genügen. Die, die du kennen solltest:

| Wert | Bedeutung |
|---|---|
| `REGISTRATION_MODE` | wer sich registrieren darf: `invite` (Vorgabe, nur mit Einladung eines Admins), `open` oder `closed` |
| `ADMIN_EMAIL` | die Adresse des ersten Admins; wer sie zuerst registriert, ist Admin |
| `IMPRINT_FILE`, `PRIVACY_FILE`, `TERMS_FILE` | Textdateien für Impressum, Datenschutzerklärung und AGB deiner Instanz; ohne Datei gibt es weder Seite noch Link |
| `DUOFY_PORT` | Port, unter dem Duofy erreichbar ist, Vorgabe 8080 |
| `COOKIE_SECURE` | `false` nur, wenn Duofy über einfaches http läuft |

```bash
docker compose up -d
```

Duofy antwortet auf `http://localhost:8080`. **Registriere dich gleich nach dem
ersten Start mit der Adresse aus `ADMIN_EMAIL`**, damit du Admin bist, bevor jemand
anderes es wird. Es sind drei Container, zusammen unter 256 MB.

Die [Installationsseite](https://github.com/tom-schorn/Duofy/wiki/Installation)
erklärt den Rest: Reverse Proxy, Rechtstexte, Sichern und Aktualisieren.

## Sprachen

Die Oberfläche ist vorerst nur auf Deutsch. Sie ist aber für weitere Sprachen
gebaut: wer eine ergänzen möchte, kopiert die Katalogdatei und schickt einen Pull
Request, ohne Code anzufassen. Die Anleitung steht im Wiki unter
[Neue Sprache beitragen](https://github.com/tom-schorn/Duofy/wiki/Neue-Sprache-beitragen).

## Dokumentation

Alles Weitere steht im [Wiki](https://github.com/tom-schorn/Duofy/wiki):

| | |
|---|---|
| [Installation](https://github.com/tom-schorn/Duofy/wiki/Installation) | Duofy zum Laufen bringen |
| [Erste Schritte](https://github.com/tom-schorn/Duofy/wiki/First-Steps) | vom leeren Konto zum ersten Monatsplan |
| [Konzept](https://github.com/tom-schorn/Duofy/wiki/Concept) | warum Duofy so gebaut ist |
| [Roadmap](https://github.com/tom-schorn/Duofy/wiki/Roadmap) | was läuft und was kommt |
| [Mitmachen](https://github.com/tom-schorn/Duofy/wiki/Contributing) | Regeln, Branches, Pull Requests |
| [Programmierrichtlinien](https://github.com/tom-schorn/Duofy/wiki/Coding-Guidelines) | wie hier Code aussieht |
| [Neue Sprache beitragen](https://github.com/tom-schorn/Duofy/wiki/Neue-Sprache-beitragen) | die Oberfläche in einer weiteren Sprache |

## Wohin mit was

| Du hast | Es gehört in |
|---|---|
| einen Fehler | ein [Issue](https://github.com/tom-schorn/Duofy/issues/new/choose) |
| eine Idee oder einen Fall, den Duofy nicht kann | [Discussions › Ideas](https://github.com/tom-schorn/Duofy/discussions/new?category=ideas) |
| eine Frage | [Discussions › Q&A](https://github.com/tom-schorn/Duofy/discussions/new?category=q-a) |
| eine Sicherheitslücke | ein [privater Bericht](https://github.com/tom-schorn/Duofy/security/advisories/new), nie ein öffentliches Issue |

Die Trennung ist Absicht: die Issue-Liste ist die Roadmap, deshalb steht dort nur
Vereinbartes. Eine Idee beginnt als Diskussion, und wenn wir uns einig sind, folgt
ein Issue.

**Fälle, die Duofy nicht abbilden kann, sind das Wertvollste, was du schicken
kannst.** Duofy ist aus einem Haushalt entstanden und trägt dessen Blickwinkel — eine
Zahlung, die in kein Feld passt, eine Einnahme, die sich nicht eintragen lässt, eine
Aufteilung, die Duofy nicht kennt. Sag, was bei dir nicht geht; die Lösung kommt
später.

Eine Regel gilt überall, denn dies ist eine Finanz-App: **keine echten Beträge,
Namen oder Kontonummern.** Ausgedachte Zahlen beschreiben ein Problem genauso gut,
und was in einem öffentlichen Issue landet, bleibt dort.

## KI-Unterstützung

Duofy wird mit KI-Unterstützung entwickelt und macht daraus kein Geheimnis. Die
Datei [CLAUDE.md](CLAUDE.md) gibt KI-Sitzungen den Kontext des Projekts; jedes Stück
Arbeit wird unabhängig geprüft, bevor es gemerged wird.

## Lizenz

[GNU AGPL-3.0](LICENSE). Nutzen, betreiben, ändern, weitergeben ist erlaubt. Wer
eine geänderte Fassung als Netzdienst anbietet, veröffentlicht auch deren
Quellcode — das ist die einzige Bedingung.
