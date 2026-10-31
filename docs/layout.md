# Layout: Breitenbereiche und Rahmen

Alle Seiten der Web-Version und der App benutzen dieselben drei Breitenbereiche (Spec 9):

| Bereich | Breite | Inhalt |
| --- | --- | --- |
| Handy | bis 599 px | volle Breite mit seitlichem Abstand, untere Navigation |
| Tablet | 600 bis 1279 px | volle Breite mit größerem seitlichem Abstand, untere Navigation |
| Desktop | ab 1280 px | Inhalt höchstens 1180 px breit und mittig, Seitenleiste (siehe Spec 10) |

## Wo die Werte stehen

- Die Grenzen und die festen Maße (1180 px Inhaltsbreite, 44 px Tippfläche) stehen als Konstanten in
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
