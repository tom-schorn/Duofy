# Datenschutzerklärung — Duofy

**Stand:** 2026-07-31
**Letzte Änderung:** 2026-07-31

> ⚠️ **Entwurf.** Erstellt aus einer Analyse der Datenmodelle und der
> Infrastruktur. Alle `[PLATZHALTER]` müssen vom Betreiber gefüllt werden.
> Keine Rechtsberatung — vor Veröffentlichung anwaltlich prüfen lassen.

---

## 1. Verantwortlicher

**[VOLLSTÄNDIGER_NAME]**
[STRASSE]
[PLZ] [ORT], [LAND]
E-Mail: [KONTAKT_EMAIL]
Website: [WEBSITE_URL]

Ein Datenschutzbeauftragter ist nicht benannt — die Voraussetzungen des
Art. 37 DSGVO liegen nicht vor.

---

## 2. Übersicht der Datenverarbeitung

| Datenkategorie | Zweck | Rechtsgrundlage | Speicherdauer |
|---|---|---|---|
| E-Mail, Vor- und Nachname | Konto, Anmeldung | Art. 6(1)(b) — Vertrag | bis Kontolöschung |
| Passwort-Hash | Anmeldung | Art. 6(1)(b) | bis Kontolöschung |
| Finanzplanungsdaten (Verpflichtungen, Beträge, Restschulden, Sparziele) | Kernfunktion | Art. 6(1)(b) | bis Kontolöschung |
| Monatspläne und Posten (geplante und tatsächliche Beträge, Zahlungsart, Zahldatum) | Kernfunktion | Art. 6(1)(b) | bis Kontolöschung |
| Änderungsprotokoll (wer hat welchen Posten wann geändert) | Nachvollziehbarkeit im Haushalt | Art. 6(1)(f) — berechtigtes Interesse | bis Kontolöschung |
| Haushaltszugehörigkeit und Rolle | gemeinsame Planung | Art. 6(1)(b) | bis Verlassen des Haushalts |
| E-Mail-Adresse eingeladener Personen | Einladung in einen Haushalt | Art. 6(1)(f) | bis Annahme, Ablehnung oder Ablauf |
| IP-Adresse, Zeitstempel, User-Agent | Betrieb, Angriffsabwehr | Art. 6(1)(f) | siehe Abschnitt 5 |

**Besondere Kategorien nach Art. 9 DSGVO werden nicht verarbeitet.** Finanzdaten
sind rechtlich keine besondere Kategorie, in der Praxis aber hoch schutzbedürftig
— sie werden entsprechend behandelt.

---

## 3. Kernfunktion des Dienstes

**Zweck:** Duofy ist eine Finanz-App mit Schwerpunkt auf Planung statt Tracking.
Nutzer erfassen Verpflichtungen (Verträge, Sparziele, Schulden) und erzeugen
daraus Monatspläne nach der 50/30/20-Regel. Optional teilen mehrere Personen
einen gemeinsam verwalteten Haushalt.

**Rechtsgrundlage:** Art. 6(1)(b) DSGVO — Vertragserfüllung
**Speicherdauer:** bis zur Löschung des Kontos
**Es findet kein Profiling und keine automatisierte Entscheidungsfindung im
Sinne des Art. 22 DSGVO statt.** Es werden keine KI-Dienste eingesetzt, damit
ist der EU AI Act für diesen Dienst derzeit nicht relevant.

---

## 4. Gemeinsame Haushalte — Datenweitergabe zwischen Nutzern

Tritt ein Nutzer einem Haushalt bei, werden die als **haushaltsbezogen**
markierten Verpflichtungen und Posten für die anderen Mitglieder sichtbar.
Als privat markierte Einträge bleiben privat.

Im Änderungsprotokoll ist erkennbar, **welches Mitglied welchen Posten geändert
hat**. Das ist gewollt — es macht gemeinsame Planung nachvollziehbar — bedeutet
aber, dass Aktivität innerhalb eines Haushalts für die anderen Mitglieder
sichtbar ist.

**Einladungen:** Wer eine Person einlädt, übermittelt deren E-Mail-Adresse an
Duofy. Die eingeladene Person ist zu diesem Zeitpunkt noch kein Nutzer. Sie wird
gemäß Art. 14 DSGVO mit der Einladung über die Verarbeitung informiert.
Rechtsgrundlage ist Art. 6(1)(f); das berechtigte Interesse liegt in der
Ermöglichung der gemeinsamen Nutzung.

---

## 5. Cloudflare — Auslieferung, Schutz und Netzwerkzugang

**Anbieter:** Cloudflare, Inc., 101 Townsend St., San Francisco, CA 94107, USA
sowie Cloudflare Germany GmbH
**Zweck:** Auslieferung des Frontends (Cloudflare Pages), Reverse Proxy und
Angriffsabwehr, sowie Cloudflare Tunnel als Zugang zum Backend — sämtlicher
Verkehr läuft über Cloudflare
**Übermittelte Daten:** IP-Adresse, Zeitstempel, angefragte URL, User-Agent,
Referrer sowie alle Inhalte der Anfragen und Antworten
**Rechtsgrundlage:** Art. 6(1)(f) DSGVO — berechtigtes Interesse an sicherem und
verfügbarem Betrieb
**Drittland:** JA, USA. Cloudflare stützt Übermittlungen auf die
EU-Standardvertragsklauseln (Art. 46 DSGVO) und ist zusätzlich unter dem
EU-U.S. Data Privacy Framework zertifiziert
**Auftragsverarbeitung:** Cloudflare Data Processing Addendum (Art. 28 DSGVO),
gilt automatisch für bestehende Verträge — **[PRÜFEN: abgeschlossen?]**
**Datenschutz des Anbieters:** https://www.cloudflare.com/trust-hub/gdpr/
**Consent-Flag im Code:** entfällt — technisch notwendig, keine Einwilligung
erforderlich

---

## 6. noez GmbH — Serverhosting

**Anbieter:** noez GmbH, Frankfurt am Main, Deutschland
**Zweck:** vServer, auf dem Backend und Datenbank laufen
**Übermittelte Daten:** alle in Abschnitt 2 genannten Daten, da sie dort
gespeichert werden
**Rechtsgrundlage:** Art. 6(1)(b) und (f) DSGVO
**Drittland:** NEIN — Rechenzentrum in Deutschland
**Auftragsverarbeitung:** **[PLATZHALTER: AVV mit noez abschließen]**

---

## 7. GitHub — Quellcode und Build

**Anbieter:** GitHub, Inc. (Microsoft), USA
**Zweck:** Quellcodeverwaltung; Cloudflare Pages baut das Frontend aus dem
Repository
**Übermittelte Daten:** keine Nutzerdaten — nur Quellcode und Build-Protokolle
**Rechtsgrundlage:** Art. 6(1)(f) DSGVO
**Drittland:** JA, USA — Standardvertragsklauseln, DPF-Zertifizierung

---

## 8. Cookies und lokale Speicherung

Es werden **keine Tracking-Cookies und keine Analyse-Werkzeuge** eingesetzt.
Eine Einwilligung nach § 25 TDDDG ist deshalb nicht erforderlich — alle unten
genannten Speicherungen sind für den Betrieb unbedingt erforderlich.

| Name | Art | Zweck | Dauer |
|---|---|---|---|
| `duofy-token` (o. ä.) | localStorage | Anmelde-Token, hält die Sitzung | bis Abmeldung |
| Theme-Einstellung | localStorage | hell/dunkel merken | dauerhaft, lokal |
| `sidebar_state` | Cookie | Zustand der Seitenleiste | 7 Tage |

Diese Daten verlassen das Gerät nicht, mit Ausnahme des Anmelde-Tokens, der bei
jeder Anfrage an das Backend gesendet wird.

---

## 9. Deine Rechte (Art. 15–22 DSGVO)

Du hast das Recht auf Auskunft (Art. 15), Berichtigung (Art. 16), Löschung
(Art. 17), Einschränkung der Verarbeitung (Art. 18), Datenübertragbarkeit
(Art. 20), Widerspruch (Art. 21) und Widerruf erteilter Einwilligungen
(Art. 7(3)).

Anfragen an: [KONTAKT_EMAIL]

**Hinweis zur Löschung im Haushalt:** Wird ein Konto gelöscht, werden dessen
Verpflichtungen und Pläne gelöscht. Einträge im Änderungsprotokoll, die andere
Mitglieder betreffen, werden anonymisiert, damit die Nachvollziehbarkeit der
Pläne der anderen erhalten bleibt. **[PLATZHALTER: Umsetzung noch offen —
aktuell löscht die Datenbank per CASCADE mit]**

---

## 10. Widerruf von Einwilligungen

Der Dienst arbeitet derzeit **ohne einwilligungsbasierte Verarbeitung**. Alle
Verarbeitungen stützen sich auf Vertragserfüllung oder berechtigtes Interesse.
Sollten später Dienste hinzukommen, die eine Einwilligung erfordern (etwa ein
Chat-Widget, Analyse oder Bankanbindung nach PSD2), wird dieser Abschnitt
ergänzt und eine Widerrufsmöglichkeit bereitgestellt.

---

## 11. Beschwerderecht

Du kannst dich bei einer Datenschutz-Aufsichtsbehörde beschweren. Zuständig ist
die Behörde deines Wohnsitzes oder die des Verantwortlichen:

**[PLATZHALTER: zuständige Landesbehörde nach Sitz des Verantwortlichen]**
Bundesbeauftragter für den Datenschutz und die Informationsfreiheit (BfDI):
https://www.bfdi.bund.de

---

## 12. Datensicherheit

- Übertragung ausschließlich über HTTPS
- Passwörter werden mit **argon2** gehasht, nie im Klartext gespeichert
- Die Datenbank ist von außen nicht erreichbar, das Backend ausschließlich über
  einen Cloudflare Tunnel ohne offene Ports
- **[OFFEN: kein Rate-Limiting an der Anmeldung — siehe Security-Prüfung]**

---

## 13. Änderungen

Bei wesentlichen Änderungen werden registrierte Nutzer informiert.
Stand: 2026-07-31
