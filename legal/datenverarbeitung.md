# Datenverarbeitung in Duofy — was der Code speichert und tut

**Zweck:** Sachliche Grundlage für `datenschutz.md` und `agb.md`. Jede Zeile
sagt, was der Code heute tut, mit der Fundstelle in Klammern (Pfade relativ zu
`backend/app/` bzw. `frontend/src/`). Diese Datei enthält keine rechtliche
Bewertung; wo eine nötig ist, steht `[TOM: …]`. Wenn sie dem Code widerspricht,
gilt der Code.

**Stand:** Hauptzweig `main`, Commit `1a09083`.

## 1. Gespeicherte Daten

### Person und Anmeldung

- Benutzer: E-Mail (eindeutig), Vorname, Nachname, Passwort-Hash, Flags
  `is_active`, `is_superuser`, `is_verified` (`models/user.py`)
- Das Passwort wird nur als Hash gespeichert; `fastapi-users` 15 hasht über
  `pwdlib`, Standard ist Argon2 (`core/auth.py`, `uv.lock`)
- Eine E-Mail-Bestätigung gibt es nicht: `is_verified` wird nirgends gesetzt
  oder geprüft (`core/auth.py`, `api/v1/auth.py`)
- Sitzungen: je Gerät eine Zeile mit SHA-256-Hash des Tokens, dem vorherigen
  Hash, Ablaufzeit und letzter Nutzung; das Token selbst steht nicht in der
  Datenbank (`models/refresh_token.py`)
- Zeilen beendeter Sitzungen werden gelöscht, nicht markiert
  (`services/refresh_tokens.py`)

### Finanzdaten

- Verträge: Name, Betrag, Kategorie, Budget, Abstand, erste Fälligkeit,
  Laufzeitende, Zahlungsart, Sparziel-Betrag und -Datum, Verknüpfung zu Konten
  und Haushalt (`models/commitment.py`)
- Monatspläne mit Zielverteilung und Puffer; Posten mit Bezeichnung, geplantem
  und tatsächlichem Betrag, Zahltag, Zahlzeitpunkt (`paid_at`) (`models/plan.py`)
- Es gibt kein Feld „Restschuld“ mehr; Schulden sind ein Vertragstyp
  (`models/commitment.py`, `models/enums.py`)
- Konten: Name, Typ, Startstand, Startdatum, Standard-/Aktiv-Flags und
  `external_ref` (`models/account.py`)
- **`external_ref` eines Kontos enthält die IBAN des eigenen Kontos**, sobald
  eine Umsatzdatei importiert wurde; sie stammt aus der Datei oder vom
  Client (`api/v1/imports.py` `_target_account`)
- Kontostand wird nicht gespeichert, nur Startstand plus Buchungen
  (`models/account.py`)
- Buchungen: Datum, Betrag, Notiz (bis 200 Zeichen), Kategorie, Budget, Verweis
  auf Posten, Name und IBAN der Gegenseite (`models/transaction.py`)
- Änderungsprotokoll an Posten: Posten, ändernde Person, Feld, alter und neuer
  Wert (`models/plan.py` `PlanPositionChange`)
- `changed_by_id` ist `NOT NULL` mit `ON DELETE CASCADE` (`models/plan.py`);
  die Anonymisierung aus #66 ist noch nicht gebaut
- Hinweise im Plan werden berechnet und nicht gespeichert (`services/hints.py`)

### Haushalt

- Haushalt: Name, Zielverteilung, Puffer (`models/household.py`)
- Mitgliedschaft: Person, Rolle, drei Freigabestufen (`grants_plan`,
  `grants_commitments`, `grants_accounts`), Standard jeweils `plan`
  (`models/household.py`)
- Einladung: E-Mail-Adresse der eingeladenen Person, einladende Person, Token,
  Status, Ablauf nach 14 Tagen (`models/household.py`, `INVITATION_LIFETIME`)
- Einladungen werden **nicht gelöscht**: angenommene, abgelehnte und
  abgelaufene Zeilen bleiben mit ihrem Status stehen; der Code enthält keinen
  Löschlauf (`api/v1/households.py`)
- Die E-Mail einer Einladung wird kleingeschrieben gespeichert
  (`api/v1/households.py`)

### Import von Umsatzdateien

- Formate: CAMT (ISO 20022) und CSV (`services/statements.py` `read_upload`)
- Höchstens 10 MB, in Stücken gelesen (`api/v1/imports.py` `MAX_UPLOAD_BYTES`)
- Die Datei wird im Arbeitsspeicher gelesen und nirgends abgelegt
  (`api/v1/imports.py` `_read_body`, `services/statements.py`)
- Gespeichert je Umsatz: Kennung der Bank (`external_ref`), Buchungs- und
  Wertstellungsdatum, Betrag, Richtung, Name und IBAN der Gegenseite,
  Verwendungszweck (bis 1000 Zeichen) (`models/imported_entry.py`)
- Nicht gespeichert: Kontostände aus der Datei, Kontoinhabername
  (`services/statements.py`; die Stände werden nur zum Abgleich beim Lesen
  benutzt)
- Verworfene Umsätze bleiben mit `discarded_at` stehen, damit ein erneuter
  Import sie nicht zurückbringt (`api/v1/imports.py` `discard`)
- Keine Verbindung zu einer Bank, keine Zugangsdaten (kein Bankmodul im Code)

## 2. Cookies und lokale Speicherung

- Refresh-Cookie `duofy_refresh`: `HttpOnly`, `Secure` (außer lokale
  Entwicklung), `SameSite=Lax`, Pfad `/api/v1/auth`, Dauer 30 Tage Inaktivität,
  bei jeder Erneuerung ausgetauscht (`api/v1/auth.py` `_set_cookie`,
  `core/config.py`)
- Zugriffstoken: 15 Minuten gültig, nur im Arbeitsspeicher des Frontends, nicht
  in `localStorage` (`core/config.py`, `frontend/src/lib/api.ts`)
- Bei Wiederverwendung eines alten Refresh-Tokens werden alle Sitzungen der
  Person beendet (`services/refresh_tokens.py`)
- Abmelden löscht die Sitzung; „überall abmelden“ alle Sitzungen der Person
  (`api/v1/auth.py`)
- `localStorage` `duofy-theme`: hell/dunkel/System (`components/ThemeProvider.tsx`)
- `localStorage` Schlüssel für die angeheftete Hilfespalte (`lib/help-state.ts`)
- Cookie `sidebar_state`: Zustand der Seitenleiste, 7 Tage, per Skript gesetzt,
  Pfad `/` (`components/ui/sidebar.tsx`)
- Das Frontend setzt keine Tracking- oder Analysewerkzeuge selbst ein.
  Was Cloudflare an der Auslieferung zusätzlich einstellt, steht nicht im Code:
  **[TOM: prüfen, ob die öffentliche Instanz Cloudflare-Analyse oder
  -Cookies aktiviert]**

## 3. Protokollierung

- Ausgabe nach stdout, Stufe per `LOG_LEVEL`, Zeilen im Format `key=value`
  (`core/logging.py`)
- Nie im Log: Anfragekörper, Query-Strings, Header, Cookies, die Meldung
  unerwarteter Fehler; Fehler nur mit Klasse und Stack (`core/logging.py`)
- Pfade werden maskiert: UUIDs, Zahlen und Segmente über 16 Zeichen (z. B.
  Einladungstoken) werden zu `{id}` (`core/logging.py` `mask_path`)
- SQL, Datenbanktreiber und HTTP-Clients bleiben auf WARNING
  (`core/logging.py` `_QUIET_LOGGERS`)
- Die Zugriffszeile von Uvicorn enthält Methode, maskierten Pfad, Status und
  die Adresse des Gegenübers; der Code schaltet sie nicht ab
  (`core/logging.py` `_MaskAccessPath`). Hinter Cloudflare ist das die Adresse
  des Proxys, sofern Uvicorn nicht mit Proxy-Header-Auswertung startet — der
  Startbefehl liegt in den Docker-Dateien, nicht in `app/`: **[TOM: prüfen, ob
  Client-IPs im Backend-Log landen]**
- Der Code kennt keine Aufbewahrungsfrist und keine Rotation für Logs; sie
  liegt bei der Laufzeitumgebung (`docker compose logs`)

## 4. Was wer sieht

- Jede Person sieht ihre eigenen Daten; fremde nur nach Freigabe der
  Besitzerin oder des Besitzers (`core/permissions.py`, `AccessLevel`)
- Standard einer Mitgliedschaft: die anderen sehen nur gemeinsame Posten, kein
  Buch, keine Konten, keine Verträge (`models/household.py`)
- Niemand stellt Rechte für andere ein; nur die Person selbst hebt ihre
  Stufen an (`api/v1/households.py`)
- Es gibt keine Verwaltungsoberfläche und keine Admin-Routen im Backend
- Ein Konto mit `is_superuser` erhält über den Standard-Nutzerrouter von
  `fastapi-users` Lese-, Änderungs- und Löschzugriff auf Benutzerdatensätze
  (`api/v1/auth.py`, Router unter `/users`); das Flag lässt sich nur in der
  Datenbank setzen. Auf Finanzdaten anderer gibt ihm das Flag keinen Zugriff
  (`core/permissions.py` prüft nur Besitz und Freigabe)
- Wer die Datenbank oder den Server betreibt, kann technisch alles lesen; es
  gibt keine Verschlüsselung einzelner Felder im Code

## 5. E-Mail

- Duofy versendet keine E-Mails: kein Mailversand im Code, keine
  Bestätigungs-, Einladungs- oder Passwort-Mails (`api/v1/households.py`, TODO
  im Docstring von `create_invitation`)
- Eine Einladung ist für die eingeladene Person nur sichtbar, wenn sie sich mit
  genau dieser Adresse anmeldet oder registriert (`api/v1/households.py`)
- Der Antwortkörper der Einladung trägt das Token an die einladende Person
  (`api/v1/households.py`)
- Der Passwort-Zurücksetzen-Router von `fastapi-users` ist eingebunden, aber
  ohne Mailversand: das Zurücksetzen ist in der Praxis nicht nutzbar
  (`api/v1/auth.py`, `core/auth.py` ohne `on_after_forgot_password`)

## 6. Aufbewahrung und Löschung

- Kontolöschung durch die Person selbst: **es gibt weder Endpunkt noch
  Schaltfläche.** `DELETE /users/{id}` verlangt Superuser; im Frontend findet
  sich keine Löschfunktion (`api/v1/auth.py`, `frontend/src`)
- Löscht ein Superuser oder der Betreiber einen Benutzer, löscht die
  Datenbank per `ON DELETE CASCADE`: Verträge, Pläne und ihre Posten, Konten,
  Buchungen, importierte Umsätze, Sitzungen, Mitgliedschaften, Einladungen,
  die er ausgesprochen hat, und Änderungsprotokolle, die er verfasst hat
  (`models/*.py`)
- Konten und Buchungen haben untereinander `RESTRICT`; die Kette ist im Code
  nicht durch einen einzelnen Löschbefehl abgesichert — **[TOM: Löschvorgang
  einmal gegen eine Testdatenbank durchspielen, bevor die Datenschutzerklärung
  ihn zusagt]** (`models/account.py`, `models/transaction.py`)
- Änderungsprotokoll: Einträge an Posten anderer Mitglieder verschwinden heute
  mit dem Konto (siehe oben; Anonymisierung noch offen)
- Austritt aus einem Haushalt: löscht nur die Mitgliedschaft. Eigene Pläne,
  Konten und Verträge bleiben bei der Person; **die anderen sehen ihre Posten
  danach in keinem Monat mehr**, auch nicht in vergangenen; nichts wird
  gelöscht; der letzte Besitzer kann nicht austreten
  (`api/v1/households.py` `leave_household`)
- Löscht man einen Vertrag, bleiben Posten stehen (`ON DELETE SET NULL` am
  Verweis) (`models/plan.py`)
- Es gibt keinen Datenexport für Personen: kein Export-Endpunkt im Backend
  (`api/v1/`)
- Es gibt keine automatische Löschung nach Zeit, außer bei Sitzungen
  (abgelaufene Zeilen werden beim Ausstellen neuer entfernt)
  (`services/refresh_tokens.py`)

## 7. Sicherheit

- Kein Rate-Limit an der Anmeldung; geplant für V2 (#170)
- Anmeldefehler antworten für falsches Passwort und unbekannte Adresse gleich
  (`api/v1/auth.py` `login`)
- Registrierung ist offen für jeden; es gibt keine Freigabe durch den Betreiber
  (`api/v1/auth.py`, `get_register_router`)
- CORS nur für die konfigurierten Ursprünge (`core/config.py`)
- Transportverschlüsselung, Cloudflare Tunnel und Netzabschottung sind
  Betrieb und stehen nicht im Code: **[TOM: durch den Betreiber zu bestätigen]**

## 8. Was der Code nicht hergibt

- Auftragsverarbeitungsverträge, Server- und Cloudflare-Angaben, Standort:
  nicht im Repo (`legal/avv_status.md`)
- Sicherungen der Datenbank und ihre Aufbewahrung: nicht im Repo
- Drittdienste im Backend: keine (kein HTTP-Client zu Fremddiensten in
  `app/`; `httpx` nur als leise gestellter Logger)
