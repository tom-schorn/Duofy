# AVV-Status — Duofy

Stand: 2026-08-19

Auftragsverarbeitungsverträge nach Art. 28 DSGVO. Ein AVV ist für jeden Dienst
nötig, der im Auftrag des Betreibers personenbezogene Daten verarbeitet.

| Dienst | Anbieter | Zweck | Drittland | AVV abgeschlossen | Notiz |
|---|---|---|---|---|---|
| Cloudflare | Cloudflare, Inc. / Cloudflare Germany GmbH | Auslieferung des Frontends, Reverse Proxy, Tunnel zum Backend | JA, USA — SCCs + EU-U.S. DPF | **[PRÜFEN]** | Data Processing Addendum gilt automatisch für bestehende Verträge: https://www.cloudflare.com/cloudflare-customer-dpa/ |
| noez GmbH | noez GmbH, Frankfurt am Main | vServer für Backend und Datenbank | NEIN — Deutschland | **[OFFEN]** | Sämtliche Nutzerdaten liegen hier. AVV anfordern und ablegen |
| GitHub | GitHub, Inc. (Microsoft) | Quellcode, Build des Frontends | JA, USA — SCCs + DPF | nicht erforderlich | Es werden keine Nutzerdaten übermittelt, nur Quellcode |

## Kein AVV nötig

- **Der Bankdatei-Import** bringt keinen Auftragsverarbeiter hinzu. Der Nutzer
  lädt eine Datei hoch, die er selbst bei seiner Bank bezogen hat; es gibt keine
  Verbindung zu einem Dritten. Die Bank ist nicht Auftragsverarbeiterin des
  Betreibers, sondern verarbeitet als eigene Verantwortliche gegenüber ihrem
  Kunden

## Falschmeldungen der automatischen Erkennung

Die Suche nach Dienstnamen im Quellcode meldet regelmäßig Treffer, die keine
Integrationen sind. Geprüft am 2026-08-19:

- **PayPal** — steht in Kommentaren als Beispiel für einen Kontotyp
  (`AccountsPage.tsx`, `MonthBook.tsx`). Keine Integration
- **AWS** — Teil des Wortes „draws" in einem Kommentar zu Recharts
- **Segment** — Teil von „literal segment" in einem Kommentar zur
  Routen-Reihenfolge
