# Layout: Breitenbereiche und Rahmen

Alle Seiten der Web-Version und der App benutzen dieselben drei Breitenbereiche (Spec 9):

| Bereich | Breite | Inhalt |
| --- | --- | --- |
| Handy | bis 599 px | volle Breite mit seitlichem Abstand, untere Navigation |
| Tablet | 600 bis 1279 px | volle Breite mit größerem seitlichem Abstand, untere Navigation |
| Desktop | ab 1280 px | Inhalt mittig, höchstens 1180 px breit oder 1600 px bei breiten Seiten (siehe unten), Seitenleiste mit dem vollen Menü |

## Navigation (Spec 10)

Handy und Tablet behalten die untere Leiste mit Start, Plan, Buchungen und Mehr. Ab 1280 px steht links die Seitenleiste
mit dem vollen Menü: Start, Plan, Buchungen, Konten, Verträge, Haushalt, Benutzerkonto, Admin (nur für Personen, die den
Server verwalten), Lizenzen, die Rechtstexte des Servers und Abmelden. "Mehr" gibt es dort nicht.

**Änderung vom 01.11.2026, von Tom verlangt (Spec 10, R2):** Die ursprüngliche Regel "gleiche Einträge und Reihenfolge wie
auf dem Handy" gilt nicht mehr. Der PC zeigt alles direkt; die feste Liste steht in `AppDestinations.Side`, der vom Server
abhängige Teil in `SideAccountEntries.razor`.

## Breite Seiten ab 1280 px

Start, Plan, Monatsübersicht, Buchungen, Konten, Verträge und Haushalt nutzen ab 1280 px bis zu 1600 px Breite (von Tom am
01.11.2026 verlangt); andere Seiten, zum Beispiel Formulare, bleiben bei 1180 px. Eine Seite wird breit, indem ein
Element in ihr die Klasse `page-wide` trägt. Listen werden zu Tabellen, ohne dass sich am Aufbau für das Handy etwas ändert:

- `data-table` um die Liste, mit einer eigenen Klasse, die `--data-columns` setzt
- `data-table-head` für die Kopfzeile (`aria-hidden`, unter 1280 px ausgeblendet)
- `data-row` an der Zeile, `data-row-frame` am Rahmen darum, `data-cells` an Gruppen, die sich auflösen
- `data-column-N` legt die Spalte fest, `data-amount` richtet Beträge rechts aus

Alle diese Regeln stehen im Desktop-Bereich von `app.css`; darunter bleibt alles, wie es war. Ein Test prüft das.

## Wo die Werte stehen

- Die Grenzen und die festen Maße (1180 und 1600 px Inhaltsbreite, 44 px Tippfläche) stehen als Konstanten in
  `Shared/Duofy.Shared/Layout/WidthRanges.cs`.
- Das Stylesheet `Shared/Duofy.Shared/wwwroot/app.css` kann sie nicht lesen und wiederholt sie in seinen
  Medienabfragen. Der Test `ResponsiveScaffoldTests` prüft, dass beide übereinstimmen.
- Die Randwerte (`--page-gutter`, `--block-gap`) und die Schriftgrößen (`--type-*`) stehen im ersten `:root`-Block
  von `app.css`; nur die drei Bereiche darunter überschreiben sie. Die Layouts benutzen die Klasse `app-page`.
- Die Schriftskala (Titel, Abschnitt, Betrag, Text, Zusatz) kommt aus `DuofyTheme.TypeScale`; die Werte im
  Stylesheet sind die des Desktops, der Abschnittstitel ist auf dem Handy und dem Tablet kleiner.

## Regeln

- Tippflächen (Knöpfe, Symbolknöpfe, Einträge der Navigation) sind mindestens 44 px hoch und breit.
- Der Rahmen `app-page` läuft nie über den Bildschirm hinaus (`max-width: 100%`, `overflow-x: clip`).
- Eigene Abstände oder Schriftgrößen im Rahmen gibt es nicht; neue Werte kommen in den `:root`-Block.
