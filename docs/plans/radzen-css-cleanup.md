# Radzen: eigenes CSS aufräumen (Vorbereitung für #76)

Stand: Zweig `feat/radzen-pages` bei Commit 7969c35, 01.11.2026. Das ist eine Bestandsliste mit Vorschlägen. Geändert wurde
nichts; `app.css` und fremde Seiten gehören bis zum Abschluss von #76 dem Zweig `feat/radzen-pages`.

Tom hat entschieden: nur wenige eigene Stile, sonst reines Radzen. Die Liste unten sagt je Fund, ob er durch einen
Radzen-Baustein, eine Radzen-Variable oder eine vorhandene Hilfsklasse ersetzbar ist oder als begründete Ausnahme bleiben
sollte. Was ich nur vermute, steht als "zu prüfen" da: Das geht erst mit einem Versuch im Browser, nicht aus dem Code.

## Zahlen vorweg

- Die Zahl 408 aus dem Bericht zu #53 bis #80 war zu hoch. Sie zählte jedes `…Style="`, also auch `ButtonStyle=`,
  `AlertStyle=` und `TextStyle=` von Radzen, und das sind keine Inline-Stile. Echte Inline-Stile (`Style="…"` und `style="…"`)
  gibt es **112** in 40 Razor-Dateien.
- Davon sind **68** nur `width: 100%` (61 an Eingabefeldern, 6 an Knöpfen, 1 an einer Stapelung), weitere 11 enthalten es zusammen mit anderem. Ohne die 68 bleiben 44 Stile.
- `Shared/Duofy.Shared/wwwroot/app.css` hat 1729 Zeilen mit rund 200 Regeln auf oberster Ebene und 158 eigenen Klassennamen in den
  Razor-Dateien (ohne `rz-…`). `Mobile/Duofy/wwwroot/app.css` hat 24 Zeilen, `ReconnectModal.razor.css` ist klein.
- Feste Farben: in `app.css` 10 Hex-Werte (Zeilen 186 bis 189, 393 bis 448), in Razor nur die drei weißen QR-Hintergründe.
- Auffällig außerdem: `ChartBlockTests.Chart_styles_take_colours_only_from_theme_variables` ist auf 7969c35 rot, weil im
  Diagrammabschnitt `color-mix(in srgb, …)` steht und der Test auf die Zeichenfolge "rgb" prüft. Das ist kein Fehler der Farben,
  sondern des Tests (oder ein `color-mix` ohne "srgb" macht ihn grün). Kai sollte es bei #76 mitnehmen.

## Teil 1: Inline-Stile

### Gruppen mit Vorschlag

| Gruppe | Fundstellen | Vorschlag |
| --- | --- | --- |
| A. `width: 100%` an `RadzenTextBox` (36), `RadzenNumeric` (18), `RadzenPassword` (7) | 61 (alle Dialoge, Konto, Anmeldung, Registrieren, Verwaltung, Haushalt, Verträge, Buchung, Sparen, Schulden) | Ersetzbar. Entweder eine einzige Regel für `.rz-form-field` (eine begründete Ausnahme statt 61 Stile) oder `RadzenStack` mit `AlignItems.Stretch` um die Felder. Zu prüfen an einer Seite: Dehnt `Stretch` das `RadzenFormField` tatsächlich? Wenn ja, ganz ohne eigenen Stil. |
| B. `width: 100%` an Knöpfen (Anmelden, Registrieren, Zurücksetzen, Bestätigen, Einladung) | 6 (dazu 2 zusammen mit Gruppe C) | Ersetzbar durch dieselbe Lösung wie A (Stretch in der umgebenden `RadzenStack`). Ein Vollbreit-Knopf ist auf dem Handy gewollt. |
| C. Knopf "gesperrt, aber erreichbar": `color`/`border-color: var(--rz-text-disabled-color); cursor: default` (mit `background-color: var(--rz-base-300)` an einer Stelle) | 6: `Login.razor:43`, `Account.razor:157`, `Household.razor:97` und `127`, `Invitation.razor:49`, `Register.razor:125` | Ersetzbar durch die vorhandene Klasse `grant-locked` (Zeile 708 in `app.css`, wird schon am Plus-Knopf benutzt) oder durch Radzens Hilfsklasse `rz-text-disabled-color`. Der Stil sitzt dann an einer Stelle, nicht an sechs. |
| D. Liste "Mehr": `width: 100%; justify-content: flex-start; text-align: left` | 8 in `Mehr.razor` (Zeilen 34 bis 60) | Ersetzbar. Die Einträge sind Navigation; `RadzenPanelMenu` mit Einträgen (wie die Seitenleiste) braucht keinen Stil. Zu prüfen: gleicher Abstand und 44 px Tippfläche. |
| E. `RadzenProgressBar` mit `--rz-progressbar-height: 0.5rem; --rz-progressbar-border-radius: var(--rz-border-radius)` | 2 (`Savings.razor:77` und `139`) | Ersetzbar durch Variablen: beide stehen in der Radzen-Datei (`--rz-progressbar-height`, `--rz-progressbar-border-radius`) und gehören einmal in `RadzenVariables.Common`. |
| F. Flex-Elemente: `flex: 1 1 auto` / `1 1 10rem`, `min-width: 0` | 5: `Plan.razor:118`, `Savings.razor:65` und `126`, `HouseholdPlanView.razor:99` | Zu prüfen: Mit `RadzenRow`/`RadzenColumn` (Größen `Size`/`SizeXS`) statt Flex-Stapel ohne Stil. Sonst eine Hilfsklasse `fill` (es gibt schon `flex-grow-1` in `app.css:1726`). |
| G. QR-Code: `width: 160px/240px; max-width: 100%; background: #ffffff` | 3 Kästen (`Admin.razor:87` und `119`, `InviteDialog.razor:23`) plus `word-break: break-all` (3), `align-self: flex-start` (2), `width: 100%` (1) | Begründete Ausnahme, aber als eine Klasse `qr-code` statt dreimal Inline. Weiß bleibt auch im Dunkelmodus, sonst lässt sich der Code nicht lesen (so steht es in #72). Damit verschwindet auch die feste Farbe aus dem Razor. `word-break` wird `long-link`, `align-self` entfällt mit Stretch/`AlignItems.Start` an der Stapelung. |
| H. Fließtext mit Umbruch: `white-space: pre-wrap; overflow-wrap: anywhere` (`Legal.razor:34`) und `pre` mit `font-size: 0.75rem` (`Licenses.razor:28`) | 2 | Begründete Ausnahme als Klasse `plain-text`: Rechtstexte und Lizenzen sind Fließtext mit Zeilenumbrüchen aus der Quelle. |
| I. `StepDialog`: Rahmen, Kopf, Schritte, Aktionen | 4 in `StepDialog.razor` (Zeilen 20, 21, 24, 27), zum Teil in C# zusammengesetzt (`FrameStyle`, `StepsStyle`) | Zu prüfen: Kopf und Aktionsleiste als `RadzenStack` (Gap/Orientation) und `RadzenCard`-Rand; `max-height: 60vh` und das Rollen bleiben eine Ausnahme (Klasse `step-dialog-scroll` neben dem vorhandenen `step-dialog`). Der Unterschied App/Browser (`Host.IsApp`) gehört in zwei Klassen statt in Zeichenketten. |
| J. `LinkButton`: `width: 100%` (nur wenn `FullWidth`) | 1 | Ersetzbar durch Klasse (`rz-w-100` gibt es in Radzen nicht sicher; zu prüfen) oder die Lösung aus B. |
| K. `NativeSelect`: `width`, `padding`, `border`, `border-radius`, `background`, `color`, `font` an `<select>` | 1 (lang) | Begründete Ausnahme als Klasse `native-select` in `app.css`: Das Gerät soll die Auswahl öffnen (R6), `RadzenDropDown` kann das nicht. Die Werte sollen aus `--rz-input-*` kommen (`--rz-input-height`, `--rz-input-padding-*` gibt es). |
| L. Symbolgröße: `font-size: 28px` (`CommitmentPaymentMethodSelect.razor:22`), `font-size: {Size}px` (`AccountIconMark.razor`) | 2 | Zu prüfen: `RadzenIcon` hat keinen Größenparameter; Hilfsklassen `rz-font-size-…` sind nicht sicher vorhanden. Sonst Ausnahme als zwei Klassen (`icon-md`, `icon-lg`). |
| M. Ausgefüllter Stern: `font-variation-settings: 'FILL' 1` (`Accounts.razor:81`) | 1 | Begründete Ausnahme (eine Klasse `icon-filled`); Material Symbols steuert die Füllung nur so. |
| N. Marke im Soll-Ist-Balken: `left: {Prozent}` (`TargetActualBar.razor:23`) | 1 | Bleibt: Der Wert kommt zur Laufzeit. Besser als benutzerdefinierte Eigenschaft (`--mark: 80%`) und eine Regel in `app.css`. |
| O. Demoseiten (`BuildingBlocksDemo`, `ChartsDemo`, `StepDialogDemo(Dialog)`) mit `@PanelStyle(...)` und `width: 100%` | 6 | Kein Handlungsbedarf für Radzen: Es sind Test- und Vorschauseiten. Entscheidung für Tom: Behalten (dann bleibt ihr CSS in `app.css` Zeilen 808 bis 830) oder aus der ausgelieferten App nehmen. |

Nach diesen Vorschlägen bleiben von 112 Inline-Stilen etwa 10 (G, H, K, L, M, N je als Klasse oder Eigenschaft), dazu die Fälle, die noch geprüft werden müssen (D, F, I, J);
alle anderen verschwinden oder werden eine Zeile in `RadzenVariables`.

### Zahl je Datei

Echte Inline-Stile je Datei, absteigend (dahinter die Gruppen):

| Datei | Stile | Gruppen |
| --- | --- | --- |
| `Pages/Account.razor` | 9 | A, C |
| `Pages/Mehr.razor` | 8 | D |
| `Pages/Admin.razor` | 8 | A, G |
| `Pages/Register.razor` | 7 | A, B, C |
| `Pages/CommitmentDialog.razor` | 7 | A |
| `Pages/Login.razor` | 5 | A, B, C |
| `Pages/Household.razor` | 5 | A, C |
| `Pages/DebtDialog.razor` | 5 | A |
| `Pages/BookingDialog.razor` | 5 | A |
| `Pages/SavingsGoalDialog.razor` | 4 | A |
| `Pages/Savings.razor` | 4 | E, F |
| `Pages/AccountDialog.razor` | 4 | A |
| `Dialogs/StepDialog.razor` | 4 | I |
| `Pages/PlanPositionDialog.razor` | 3 | A |
| `Pages/InviteDialog.razor` | 3 | G |
| `Pages/Invitation.razor` | 2 | B, C |
| `Pages/HouseholdDialog.razor` | 2 | A |
| `Pages/PlanCarryOverDialog.razor` | 2 | A |
| `Pages/ResetPassword.razor` | 2 | A, B |
| `Pages/BuildingBlocksDemo.razor`, `Charts/ChartsDemo.razor`, `Dialogs/StepDialogDemoDialog.razor` | je 2 | O |
| je 1: `ServerDialog`, `SavingsMoveDialog`, `PlanTickDialog`, `Plan`, `Licenses`, `Legal`, `HouseholdPlanView`, `DeleteAccount`, `DebtPaymentDialog`, `ConfirmEmail`, `ConfirmEmailChange`, `CommitmentPaymentMethodSelect`, `Accounts`, `AccountIconMark`, `Layout/NativeSelect`, `Layout/LinkButton`, `Charts/TargetActualBar` | 17 | A, B, F, H, J, K, L, M, N |

(Die genauen Zeilen je Fund stehen oben in den Gruppen; die vollständige Rohliste mit Datei, Zeile, Element und Wert lässt
sich mit `git grep -n -P '(?<![A-Za-z])[Ss]tyle="[^"]*"' -- '*.razor'` neu erzeugen.)

## Teil 2: Abschnitte in `app.css`

Die Datei hat 13 zusammenhängende Abschnitte. "Ausnahme" heißt: Radzen kann es nicht, der Grund gehört als Kommentar an die
Regel (das verlangt #76). "Variable" heißt: Der Wert gehört in `RadzenVariables.cs`, nicht in `app.css`.

| Zeilen | Abschnitt | Regeln | Vorschlag |
| --- | --- | --- | --- |
| 1 bis 20 | Inter (`@font-face`), Ziffern gleicher Breite | 3 | Ausnahme, schon im Plan so vorgesehen (die Radzen-Themes bringen nur Roboto mit). |
| 21 bis 102 | Seitenrahmen `app-page`, drei Breitenbereiche (Spec 9) | 6 | Teils Variable (`--rz-layout-body-padding` ist schon gesetzt, seitlicher Abstand je Breite dort); die Höchstbreiten 1180 und 1600 px bleiben Ausnahme. |
| 103 bis 254 | Kopfzeile, Tippflächen 44 px, Fokus, Überspringen-Link, Feldbeschriftung | 20 | Fokus: Variable (`--rz-outline-width`, `--rz-outline-offset`, `--rz-focus-outline-color` gibt es). Kopfzeilenfarbe der Knöpfe: Variable (`--rz-header-color`). 44 px: zu prüfen, ob `--rz-input-height`, `--rz-button-line-height` genügen; sonst Ausnahme (Barrierefreiheit). Überspringen-Link: Ausnahme (kein Radzen-Baustein). |
| 255 bis 451 | Anmeldelayout, Marke und Logo (Issue 8) | 20 | Marke/Logo (SVG, Seitenverhältnis 3:1): Ausnahme. Anmeldelayout: zu prüfen, ob `RadzenRow`/`RadzenColumn` und `RadzenCard` es ohne Regeln tragen. Die festen Farben in 393 bis 448 (`#14213d`, `#fbfaf6`) gehören auf Variablen. |
| 452 bis 576 | App-Layout: untere Leiste, Plus-Knopf, Seitenleiste | 15 | Plus-Knopf: Variablen (`--rz-fab-inset-block-end`, `--rz-fab-inset-inline-end` gibt es). Untere Leiste und Sicherheitsabstand (`safe-area-inset`): Ausnahme, schon im Plan (Frage 1) genehmigt. Seitenleiste: Variablen (`--rz-sidebar-*`). |
| 577 bis 736 | Vollbild-Kopf, Profilfeld, Menü, "gesperrt", nur für Screenreader | 23 | Profilfeld/Menü: zu prüfen, vieles ist `rz-profile-menu`-Anpassung und über `--rz-profile-menu-*` machbar; `visually-hidden` und `grant-locked`: Ausnahme (klein). |
| 737 bis 830 | Farbblock und Linientabelle, Demo-Panel | 14 | Farbblock und Linientabelle: Ausnahme, es sind die eigenen Bausteine der Marke ohne Radzen-Gegenstück (Farben aus Variablen). Demo-Panel: siehe Gruppe O. |
| 831 bis 976 | Diagramme (`RadzenProgressBar`) und Planübersicht | 21 | Diagramme: Variablen (`--rz-progressbar-*`) plus die Soll-Marke als Ausnahme. Planübersicht (`plan-month…`): zu prüfen, ob `RadzenDataList` oder `RadzenCard` in `RadzenStack` es ohne Regeln kann. Der `color-mix`-Test ist rot (siehe oben). |
| 977 bis 1062 | Schritt-Dialog | 13 | Kopf/Aktionen als `RadzenStack`; Fortschrittsbalken: `RadzenProgressBar` mit Variablen statt `step-dialog-bar-segment`. Rollen im Dialog bleibt Ausnahme (Gruppe I). |
| 1063 bis 1210 | Monatsseite: Tabs, Blöcke, Listen | 14 | Tabs: zu prüfen mit `RadzenTabs`/`RadzenTabsItem` (Umbruch am schmalen Handy). Blöcke und Linientabellen: wie bei 737. Spalten am Handy: zu prüfen mit `RadzenRow`. |
| 1211 bis 1399 | Breite Seiten ab 1280 px (Tom, 01.11.), Beträge nie umbrechen | 3 (aber rund 190 Zeilen) | Größter einzelner Brocken. Er wiederholt für viele Seiten dieselben Spaltenbreiten; zu prüfen, ob eine gemeinsame Klasse `page-wide` mit `RadzenRow` und `RadzenColumn Size` es ersetzt. Betrag ohne Umbruch: eine Ausnahme-Regel (`white-space: nowrap`). |
| 1400 bis 1530 | Startseite, Einwilligungsleiste | 19 | Start: `RadzenCard`, `RadzenRow`; der Farbblock bleibt Ausnahme. Einwilligungsleiste: `RadzenCard` fixiert am unteren Rand ist eine Ausnahme, die Knopfgrößen kommen aus Variablen. |
| 1531 bis 1730 | Konten mit Symbol, Zahlungsart, Richtwerte, Buchungszeilen | 28 | Auswahl als Optionsgruppe mit Symbol und Zahlart-Knöpfen: Ausnahme (Radzen hat keine Auswahlkarten mit Symbol); `RadzenRadioButtonList` ist zu prüfen. Richtwerte und Buchungszeilen: `RadzenStack`/`RadzenCard`. |

Die größten Hebel in dieser Reihenfolge:

1. Gruppe A und B (67 Inline-Stile) durch eine Regel oder durch Stretch.
2. Der Abschnitt 1211 bis 1399 (breite Seiten): fast 190 Zeilen, die Radzens Raster ersetzen könnte.
3. Gruppe C, E (8 Stile) durch eine vorhandene Klasse beziehungsweise zwei Variablen.
4. Feste Farben (10 in `app.css`, 3 in Razor) durch Variablen, sonst bleibt das Kriterium "keine festen Farben" offen.

## Vorschlag für die Reihenfolge

Jeder Schritt wäre ein eigener Commit mit grünem `Duofy.Shared.Tests` und Sichtprüfung hell und dunkel:

1. `RadzenVariables.Common`: Fortschrittsbalken, Fokus-Werte, Plus-Knopf-Abstände. Gruppe E fällt weg.
2. Klassen `grant-locked` an die sechs Knöpfe (Gruppe C).
3. Eine Regel (oder Stretch) für die Eingabefelder und Knöpfe (Gruppen A, B). Erst an einer Seite versuchen, dann überall.
4. QR, Klartext, NativeSelect, Symbolgrößen als je eine benannte Klasse mit Begründung (G, H, K, L, M).
5. `Mehr`, `StepDialog` und das Raster der breiten Seiten (D, I, Abschnitt 1211 bis 1399).
6. Feste Farben auf Variablen; `ChartBlockTests` anpassen.

## Offene Fragen an Tom

- Reicht eine einzige Regel für `.rz-form-field { width: 100% }` als Ausnahme, oder soll es ganz ohne gehen (Stretch)?
- Bleiben die Demoseiten (Bausteine, Diagramme, Schritt-Dialog) in der ausgelieferten App?
- Darf der Schritt-Dialog seine Scroll-Höhe (`max-height: 60vh`) als Ausnahme behalten?

## Stand der Umsetzung (Zweig feat/start-guideline)

Umgesetzt, jeweils mit grünen Tests und Vergleich der Bilder vor und nach der Änderung im Browser (Chromium, 1912 und 360 px,
hell, 1912 px dunkel; Seiten Anmelden, Registrieren, Start, Konto, Konten, Verträge, Buchungen, Sparen, Haushalt, Mehr, Plan,
Lizenzen und die Dialoge Konto, Vertrag, Buchung, Sparziel; die Bilder waren Pixel für Pixel gleich):

- Gruppe A und B: eine Regel für `.rz-textbox` und `.rz-numeric`; Vollbreit-Knöpfe über Radzens eigene Klasse `rz-w-100`.
- Gruppe C: Klasse `unavailable` (6 Stellen).
- Gruppe D: Klasse `more-entry` (8 Einträge auf Mehr); die tote Regel `.more-item.rz-button` ist entfernt.
- Gruppe E: Höhe und Rundung der Fortschrittsbalken stehen in `RadzenVariables.Common`.
- Gruppen F, G, H, K, L, M: benannte Klassen mit Begründung (`fill`, `fill-wrap`, `qr-code`, `long-link`, `align-start`, `plain-text`,
  `native-select`, Symbolgrößen, `icon-filled`).
- Gruppe I: der Schritt-Dialog hat Klassen statt Zeichenketten (`step-dialog-app`, `step-dialog-top`, `step-dialog-scroll`, `step-dialog-foot`);
  die Höhe von 60 vh bleibt als Ausnahme (Tom).
- Gruppe N und O: bleiben (laufzeitabhängig beziehungsweise nur im Entwicklungsbetrieb); alle anderen Inline-Stile sind weg. Zwei Tests
  halten das fest: Inline-Stile nur in diesen drei Dateien, und im Stylesheet keine feste Farbe außer dem Weiß des QR-Codes.
- Feste Farben: die zehn Hex-Werte in `app.css` sind bis auf das QR-Weiß auf Variablen des Themes umgestellt (Fehlerleiste, Markenfläche der
  Anmeldeseite, Töpfe in der Legende).
- `ChartBlockTests`: prüft nur noch die Farbfunktionen `rgb(` und `rgba(` und erlaubt eine Mischung aus zwei Theme-Variablen.

Nicht umgesetzt, mit Grund:

- Abschnitt "Breite Seiten ab 1280 px" (rund 190 Zeilen): Er ist ein eigenes Raster mit `data-row`, `data-cells` und `data-column-N`
  und nicht gegen Radzens Raster gestellt worden. Toms Bedingung (nur ersetzen, wenn es im Browser gleich gut aussieht) ist nicht
  geprüft. Offen für einen eigenen Schritt.
- Gruppe J, Teile von 103 bis 254 (44 px, Fokus), Profilfeld/Menü (577 bis 736), Planübersicht (918 bis 976), Tabs der Monatsseite: nur
  mit Versuchen im Browser zu beurteilen; unverändert.
- Nicht im Bild geprüft, weil im Testkonto keine Daten dafür da waren: Verwaltung (QR, Link, Knöpfe), die Einladung mit QR, die
  Rechtstexte und Planposten mit `fill`. Die Werte der Klassen sind gleich den früheren Inline-Werten; Klassen und Regeln prüfen Tests.
