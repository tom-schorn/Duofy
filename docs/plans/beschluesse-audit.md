# Prüfung aller Beschlüsse gegen den Code

Stand: 01.11.2026, Code in `main` (5b317f9). Auslöser: Tom, 01.11.2026, "Entscheidungen werden ignoriert". Geschrieben von ida.

Dieses Dokument ist eine Arbeitsliste, kein Erfolgsbericht. Es sagt zu jedem Beschluss von Tom, ob der Code ihn einhält, und nennt den Beleg. Was ich nicht belegen konnte, steht als "ungeprüft" da und nicht als "umgesetzt".

## Wie geprüft wurde, und was nicht

Gelesen habe ich: alle zwölf Protokolle in `.workday/minutes`, alle Karten und Annahmen (`.workday/parking-lot.md`, `.workday/assumptions.md`, `.workday/dockets`), die sechs Übergaben in `.workday/handoffs`, `docs/plans/release-1-0-plan.md` und `docs/plans/radzen-plan.md`, und alle 81 Issues in tom-schorn/duofy-dotnet mit allen Kommentaren (nur lesend). Der Code ist der von `main`, gelesen, nicht nur die Titel.

Nicht getan, und deshalb nirgends behauptet:

- Keine Tests ausgeführt. Wo ein Test genannt ist, heißt das: er existiert und prüft die Sache. Ob er gerade grün ist, habe ich nicht gemessen.
- Keinen Browser und kein Handy angesehen. Alles, was nur man sehen kann (Aussehen, Kontrast, Höhe eines Dialogs auf dem Handy), ist höchstens "Code vorhanden".
- Den Server nicht angesehen. Einstellungen in Toms `.env` (Registrierung nur per Einladung, Markenname) sind ungeprüft.
- Das Repo der Landingpage nicht gelesen.

Die Quellen unter `.workday` liegen im Hauptordner, nicht in `main`, weil sie dort noch nicht eingecheckt sind. Ich habe sie von dort gelesen, einschließlich der Änderungen von heute.

Hinweis zum Arbeitsplatz: `.worktrees/ida` war kein gültiger Arbeitsbaum mehr (kein `.git`, bei Git als "prunable" geführt). Ich habe deshalb `.worktrees/ida-audit` neu angelegt, Zweig `review/decisions-audit` von `main`. Nichts anderes wurde angefasst.

## Legende

- umgesetzt: Der Code tut, was Tom beschlossen hat. Beleg steht dabei.
- teilweise: Ein Teil ist da, ein benannter Teil fehlt oder ist nicht belegt.
- fehlt: Beschlossen, im Code nicht vorhanden.
- widerspricht: Der Code tut etwas anderes, als Tom gesagt hat.
- ungeprüft: Ich konnte es mit dem, was ich hatte, nicht belegen.

Das Wort "schließbar" bedeutet: Code und Tests belegen alle Kriterien des Issues, die man im Code belegen kann. Gesehen hat Tom es damit noch nicht. Nach Toms Regel schließt ein Issue erst, wenn er den Stand gesehen hat und Ja sagt.

## Übersicht in Zahlen

63 Beschlüsse geprüft (B-01 bis B-63):

| Gruppe | Anzahl |
|---|---|
| 1 Widersprüche zu Tom | 5 |
| 2 Fehlt (davon 3 gehören nach Toms Aussagen zu V1, 2 ungeklärt) | 5 |
| 3 Teilweise | 14 |
| 4 Umgesetzt (davon 14 Issues schließbar nach Toms Sicht) | 35 |
| Ungeprüft | 4 |

Dazu 19 offene Fragen an Tom (Abschnitt 5).

## Alle Beschlüsse im Einzelnen

Spalten: Nr., was Tom beschlossen hat (mit Quelle), Status, Beleg im Code.

### Aussehen und Bedienoberfläche

| Nr. | Beschluss (Quelle) | Status | Beleg |
|---|---|---|---|
| B-01 | Schrift Inter, lokal, kein Fremdserver, Beträge mit tabellarischen Ziffern, Lizenzdatei im Projekt (29.10., Issue 2) | umgesetzt | `Shared/Duofy.Shared/wwwroot/fonts/inter-latin-wght-normal.woff2`, `fonts/LICENSE-Inter-OFL.txt`, `app.css:21` tabular-nums, Test `FontInterTests.cs` (kein Google-Fonts-Aufruf). Issue 2 ist geschlossen. |
| B-02 | Rundung 12 px (30.10. Zwischenmeeting, Issues 47 und 55) | umgesetzt | `Theme/RadzenVariables.cs:16` Radius 12px für 15 Teile (Karte, Knopf, Eingabe, Menü, Dialog ...). Älterer Beschluss "eckig" siehe Frage 3. |
| B-03 | Keine Schatten-Karten, stattdessen Linien und Flächen (29.10., Issues 1, 3, 6) | widerspricht | `Theme/RadzenVariables.cs:97` und `:138` setzen `--rz-card-shadow` in hell und dunkel; `app.css:506` zeichnet `box-shadow`. Die Radzen-Demo vom 30.10. kann das überholt haben, das hat Tom nicht gesagt. Frage 4. |
| B-04 | Palette: Marineblau, Salbei für Einnahmen, Korall für Festes, Gelb für Freies (29.10., Issue 1); am 30.10. "Palette Altes Duofy" freigegeben (Issue 47) | teilweise | Hauptfarbe `#1e3a5f` (`RadzenVariables.cs:87`). Die drei Farbblöcke mischen aber Erfolg, Gefahr und Warnung zu je 50 Prozent mit der Karte (`app.css:792-802`): das ist Grün, Rot und Gelb, nicht Salbei und Korall. Abgeleitete Werte (A-021) hat Tom nicht gesehen. Frage 5. |
| B-05 | Dunkles Design bestätigt: Grund `#0b0e14`, Karte `#141922`, Hauptfarbe `#86ace0` (30.10., Issue 55) | umgesetzt | `RadzenVariables.cs:124-128` und `:168`. Der ältere Wert aus Issue 4 (Grund `#0F1729`, gelber Hauptknopf) ist damit überholt, siehe Frage 9. |
| B-06 | Kontrast mindestens 4,5 zu 1 hell und dunkel (Issues 3, 4, 55) | umgesetzt | Test `Tests/Duofy.Shared.Tests/ThemeVariablesTests.cs` prüft die Paare; kai nennt gemessene Werte in der Übergabe vom 01.11. (Lauf von mir nicht wiederholt). |
| B-07 | Drei Wahlmöglichkeiten Hell, Dunkel, wie das Gerät; Standard "Gerät"; Wechsel ohne Neuladen und ohne falsche Farbe beim Start (Issue 5) | umgesetzt | `Layout/ThemeChoiceSwitch.razor:30-34`, `Theme/ThemeBoot.cs` (Skript im Kopf, setzt die Farbe vor dem ersten Zeichnen), `Services/LocalStorageThemePreferenceStore.cs`. |
| B-08 | Hell/Dunkel/Gerät "pro Person gemerkt" (30.10., Kopf von Issue 53, als Zusammenfassung des Leads) | teilweise | Gemerkt wird je Gerät: Browser `localStorage`, App `Preferences`. Am Konto auf allen Geräten ist es nicht (Karte P-071 offen, Frage 8). |
| B-09 | Symbole: Material Symbols statt Tabler (30.10., Radzen-Plan Frage 4, in Issue 53 als entschieden geführt) | umgesetzt | Kein Tabler mehr im Code (`git grep` findet nur den Changelog); `Layout/AppDestinations.cs` und `AccountIconSet.cs` nennen Material-Namen. Issue 7 ist mit den Tabler-Kriterien geschlossen, diese Kriterien gelten nicht mehr. |
| B-10 | Jede Kontoart hat ein Standardsymbol: Girokonto Bank, Sparkonto Sparschwein, Bargeld Scheine; anpassbar (29.10., Issue 14) | umgesetzt | `Backend/Duofy.Schemas/Accounts.cs:37-42`, `Services/AccountIconSet.cs:14-19`, `Pages/AccountIconPicker.razor`, Migration `20261101084726_AddAccountIcon`. |
| B-11 | Zahlart als Symbol-Knöpfe mit Text (Issue 15) | umgesetzt | `Pages/CommitmentPaymentMethodSelect.razor:22`, benutzt in `CommitmentDialog.razor:206`. |
| B-12 | Konten mit Symbol in Buchung, Vertrag und Plan-Posten (Issue 16) | umgesetzt | `AccountSelect` in `BookingDialog.razor` und `PlanPositionDialog.razor`, `CommitmentAccountSelect.razor` im Vertrag. |
| B-13 | Logo und App-Symbole: Favicon, Android-Symbol, Store-Kachel, Logo in Kopfzeile und Anmeldeseite (Issue 8) | teilweise | Dateien da: `wwwroot/brand/plagevo-*.svg`, `Mobile/Duofy/Duofy.csproj:26,92,95` (Titel Plagevo, Symbol, Startbild), `Services/AppBrand.cs`. Die Safe-Zone des Android-Symbols und das Aussehen auf dem Handy sind ungeprüft. |
| B-14 | Responsives Gerüst: drei Breiten, Inhaltsbreite begrenzt, Tippflächen mindestens 44 px, nichts läuft bei 360, 768, 1280 aus dem Bild (Issue 9) | teilweise | `Layout/WidthRanges.cs:12-24` (599, 1279, 1280, Inhalt 1180), `app.css:157`. Der Durchlauf bei den drei Breiten ist auf dem Endstand nicht belegt (Übergabe lead 01.11. 21:58, Übergabe kai). |
| B-15 | Am PC das volle Menü links: Start, Plan, Buchungen, Konten, Verträge, Haushalt, Benutzerkonto, Admin nur für Admins, Lizenzen, Rechtstexte, Abmelden (01.11., Kommentar an Issue 10) | umgesetzt | `Layout/AppDestinations.cs:27-37` (`Side`), `Layout/SideAccountEntries.razor:22-31`. Zusätzlich steht dort "Sparen", das nicht in Toms Liste ist, siehe B-17. |
| B-16 | Menü ohne Trenner, aktiver Eintrag ohne Balken (30.10., Issue 47 und 55) | umgesetzt | `RadzenVariables.cs:62-63` (Rand none, Balken transparent), `app.css:723` "ohne Balken". |
| B-17 | Untere Leiste am Handy mit vier Zielen (Issue 61: "zeigt vier Ziele mit Symbol und Text"; Kommentar an Issue 10: "bleibt") | widerspricht | `Layout/AppDestinations.cs:27-33`: fünf Ziele (Start, Plan, Buchungen, Sparen, Mehr). Das fünfte kam durch eine Annahme von mir (A-015) ohne Antwort von Tom. Der Kommentar in `SideNavigation.razor` sagt selbst noch "vier Ziele". Frage 6. |
| B-18 | Profilfeld unten in der Seitenleiste als Personen-Umschalter; der Umschalter und der Knopf Haushalt immer sichtbar, der Haushalt erklärt, was fehlt (30.10. und 29.10., Issues 60 und 44) | umgesetzt | `Layout/MemberSwitch.razor:44-83` (RadzenProfileMenu; Haushalt ab zwei Mitgliedern, sonst ausgegraut mit Grund und Weg zur Seite), `app.css:735-742` (Fuß der Seitenleiste). |
| B-19 | Wenige eigene Stile, sonst reines Radzen; "kein eigenes CSS und feste Farben nur nach Rückfrage" (Tom, 30.10./31.10.; Radzen-Plan, Regeln für alle Slices; `app.css:1` nennt es "Toms Vorgabe") | widerspricht | `wwwroot/app.css`: 1889 Zeilen, 294 Regeln, 13 mal `!important` (`:751-759, 1173, 1277-1308, 1417`). Zu Beginn der Umstellung waren es 365 Zeilen (Radzen-Plan, Abschnitt "Was der Code tatsächlich hergibt"). Plan R24 wollte es auf Ausnahmen zurückführen. Frage 7. |
| B-20 | MudBlazor ganz weg, Umstellung komplett (30.10., Issue 48 und 76) | umgesetzt | `git grep -i mudblazor` außerhalb `docs` und `.workday` findet nur `CHANGELOG.md` und den Wächtertest `RazorFilesTests.cs`. |
| B-21 | Untere Leiste, Plus-Knopf und Vollbild-Formulare am Handy bleiben (30.10.) | umgesetzt | `Layout/AppLayout.razor:65-72` (RadzenFab), `Services/FormDialogs.cs:28,82` (volle Breite in der App). Wie es auf dem Telefon aussieht, ist ungeprüft. |
| B-22 | Radzen läuft auf dem Android-Telefon; Tom prüft das (Issue 54, R2) | ungeprüft | Kein Beleg in den Quellen. Übergabe lead 01.11. 13:10: Release-Build scheitert an `java.exe` Code 2, Debug-Build ist auf Toms Handy. Plan sagt: scheitert Android, anhalten und Tom fragen. |

### Buchungen, Plan, Konten, Verträge

| Nr. | Beschluss (Quelle) | Status | Beleg |
|---|---|---|---|
| B-23 | Jede Buchung gehört mindestens einem Topf (Entscheidung 9, 30.10.) | umgesetzt | `Backend/Duofy.Backend/Api/BookingValidator.cs:273` (`PotRequired`), Dialog startet leer als Pflicht (A-009, angenommen), Spec 72. |
| B-24 | Auch Umbuchungen brauchen einen Topf (Einspruch gegen A-008, 30.10.) | umgesetzt | `BookingValidator.cs:226` (`TransferPotsRequired`: Quell- und Zieltopf), `BookingDialog.razor:103` (Zieltopf), Migration `20261031105412_AddTransferTargetPot`, `PlanEndpoints.cs:340` (Abhaken). Der Umfang ("welcher Topf") sollte laut Issue 49 mit Tom geklärt werden; Karte P-062 steht noch offen. Frage 10. |
| B-25 | Keine Buchungen ohne Topf, keine Migration, keine Prüfung (P-059, 30.10.) | umgesetzt | Keine Migration dazu vorhanden (Liste in `Backend/Duofy.Backend/Data/Migrations`). |
| B-26 | Ungeplante Buchungen zählen im Topf und im Lückendiagramm, nicht in den Richtwert-Balken (Entscheidung 4, Wrap-up 30.10.) | umgesetzt | `Domain/PlanFigures.cs:61-90` (`QuotaBase` zählt nur Posten), `Api/PlanStore.cs:273` (`Unplanned` nur in den Topfsummen). |
| B-27 | "Buchung nur im jeweiligen Plan" anlegen (Entscheidung 5, Wrap-up 30.10.; heute 01.11. bekräftigt) | widerspricht | Neue Buchungen gehen auch von der Seite Buchungen (`Pages/Bookings.razor:139` Plus-Knopf, in der App über `:235` das globale Plus), von der Startseite (`Pages/Start.razor:176-186`) und über das Plus in `Layout/AppLayout.razor:65-72`. Nur `Plan.razor:412` gibt den Monat vor. Frage 1. |
| B-28 | Das Datum der Buchung ist ein Tag in diesem Plan-Monat; die Regel "höchstens ein Monat Abstand" entfällt für den eigenen Plan-Monat (Entscheidung 5, Issue 49) | widerspricht | `BookingValidator.cs:171` (`IsPlanMonthAllowed`, Abstand höchstens 1) und `:114` (kein Datum nach heute); `BookingDialog.razor:53` lässt Datum und Planmonat getrennt wählen. Folge: Auf dem Plan Dezember ist am 21.10. gar keine Buchung möglich, weil das Datum nicht in der Zukunft liegen darf. Issue 49 steht offen, A-010 hat die Regel bewusst unangetastet gelassen. Frage 2. |
| B-29 | Posten abhaken darf zum Reiter Plan wechseln (P-055, 30.10.) | umgesetzt | `Pages/Plan.razor:87,225` (`OnTick="ShowPlanTab"`). |
| B-30 | Buchungslisten: Einnahme, Ausgabe, Umbuchung, Übertrag farbig und zusätzlich mit Wort, statt Notiz (P-055, Issue 51) | fehlt | `Blocks/LineTableRow.cs` hat kein Feld für die Art oder Farbe einer Zeile; Issue 51 hat keine Spezifikation. |
| B-31 | Aufteilen bei Buchungen (Spec 59 A21, 21.10.; heute 01.11.) | umgesetzt | Backend: 1 bis 20 Zeilen, Summe muss stimmen (`BookingValidator.cs`, `CheckLines`). Dialog: `BookingDialog.razor:246` Knopf "Aufteilen" (`booking-split`), Zeilenblöcke ab `:127`. Dass es im Browser sichtbar funktioniert, habe ich nicht gesehen. |
| B-32 | Aufteilen bei Posten (Tom, 01.11., laut Auftrag an mich) | fehlt | `Pages/PlanPositionDialog.razor` kennt keine Mehrfachzeilen; in den Protokollen steht dazu nichts, es gibt keine Spezifikation. Frage 12. |
| B-33 | Übertrag: verständliche Meldung, wenn das Konto zu spät startet; bitte ein anderes Konto wählen (P-065, 31.10.) | teilweise | Meldung vorhanden (`Services/ErrorTexts.cs:19`). Eine Kontowahl im Übertrag-Dialog gibt es nicht (`PlanCarryOverDialog.razor` fragt nur Salden), und kai meldet, dass eine neue Person im ersten Monat deshalb nicht übertragen kann (Übergabe kai 01.11.). Frage 15. |
| B-34 | Der Übertrag ist eine eigene Zeile im Plan (01.11., P-069 überholt) | umgesetzt | `Pages/Month/MonthBookings.razor:31-33` (`CarryOvers`), `Services/MonthCarryOverText.cs`. Die Summenzeile zählt das Paar weiter (A-013, nicht von Tom gesehen, Frage 18). |
| B-35 | Haushalts-Monat: letzte Buchungen nur von Mitgliedern mit Freigabe Buchungen (30.10., Issue 46) | umgesetzt | `Api/HouseholdPlanStore.cs:80`, fünf Backend-Tests laut Wrap-up. Gerät in Konflikt mit dem neuen Beschluss vom 01.11. (Issue 81), siehe B-47 und Frage 11. |
| B-36 | Buchen im neuen Dialog: Gerüst, Symbole, höchstens zwei Schritte oder ein Bildschirm am Handy (Issue 21) | teilweise | `BookingDialog.razor` ist ein `StepDialog` mit Symbol-Konten. Die Höhe auf dem Handy und "alle Buchungsarten möglich" sind nicht belegt. |
| B-37 | Dialoge für Konto, Haushalt und Plan-Posten im neuen Gerüst; Esc schließt (Issue 22) | umgesetzt | Alle fünf Dialoge benutzen `<StepDialog>` (`AccountDialog`, `BookingDialog`, `CommitmentDialog`, `HouseholdDialog`, `PlanPositionDialog`), Fokus in `wwwroot/dialog-focus.js` (A-025). Höhe am Handy ungeprüft. |
| B-38 | Standardkonto beim Registrieren; Name nach der Sprache, vorerst immer Deutsch (29.10., P-040 Antwort C) | umgesetzt | `Domain/DefaultAccountName.cs`; `Duofy.Schemas/Auth.cs` hat bewusst kein Sprachfeld. Issue 13 ist geschlossen. |
| B-39 | Registrierung ohne Besitzerprüfung, eng begrenzt (29.10., P-041 Antwort A) | umgesetzt | `Api/AuthEndpoints.cs:172` (`Bypass("Erstes Konto bei der Registrierung")`). |
| B-40 | Vertrag in drei Schritten (Was, Wann, Wer und Wohin); Einnahme und Ausgabe wählbar; eine einmalige Einnahme bleibt ein Plan-Posten (Issues 17 bis 20, Entscheidung vom 01.11.) | umgesetzt | `Pages/CommitmentDialog.razor:24,96` (Schritte), `Services/CommitmentDraft.cs:26` (`OfferedTypes` nur Ausgabe und Einnahme), Zusammenfassung ab `:251`. Gespeicherte Sparziele und Schulden zeigt die Liste ohne Bearbeiten-Knopf (A-004). |
| B-41 | Plan-Übersicht mit Monatsauswahl; Monatsplan mit den Blöcken Einnahmen, Fest, Frei und Soll-Ist-Balken (Issues 23 bis 25) | umgesetzt | `Pages/PlanOverview.razor`, `Pages/Month/MonthOverview.razor`, Blöcke in `app.css:785-826`, `Charts/TargetActualBar.razor`. Die Begriffe "wie Archiv-Issue 24" (Issue 24) habe ich nicht gegen das Archiv geprüft. |
| B-42 | Monatsende-Auswertung mit mindestens zwei Diagrammen, Textalternative, Leerzustand, auch für abgeschlossene Monate (Issue 26) | umgesetzt | `Pages/Month/MonthReview.razor`: Verteilung (`BarChart`) und Soll gegen Ist (`TargetActualBar`), Text unter dem Diagramm, Leerzustand `:21`, Reiter in `MonthTabs.razor:19`. |
| B-43 | Richtwerte je Person (Issue 27); gemeinsame und eigene Sicht, sichtbar wenn der Plan aufgeht und eine Person eine Lücke hat (Issue 28) | teilweise | Backend je Person: Issue 27 geschlossen. Die Umschaltung läuft über das Profilfeld, `Pages/Month/HouseholdGuidelines.razor` zeigt die Richtwerte. Dass "nebeneinander" und "Lücke wird sichtbar" so gezeigt wird, ist ungeprüft; Issue 28 hat zudem den Vermerk zu Issue 81. |
| B-44 | Sparen: Menüpunkt mit Rechten, Sparziele mit Stand, Schulden und Kredite, Sparziel im Vertrag (Issues 29 bis 32) | teilweise | `Pages/Savings.razor`, `Domain/Grant.cs:57` (Bereich Sparen), `Domain/SavingsGoal.cs`, `Api/SavingsGoalEndpoints.cs:51-62` (erreichtes Ziel verlangt keine Rate mehr), `Api/DebtEndpoints.cs`, `CommitmentDialog.razor:178-185`. Offen: Die Fallverwaltung mit Mahnstufen (Forderungen) ist nicht gebaut (P-057). Außerdem sagt meine Annahme A-017 "es gibt keinen Anfangsbestand", der Code hat aber "Schon angespart" (`SavingsGoal.cs:29`, `SavingsGoalDialog.razor:109,139`). Frage 17. |
| B-45 | Startseite: Begrüßung, Frei-Betrag, schnelle Buchung, Hinweis "Mit jemandem zusammen planen?"; später Anstehendes und Soll-Ist (Issues 33, 34) | teilweise | `Pages/Start.razor` hat Begrüßung (`Start_Greeting`), Frei, Aktion und Haushalt-Hinweis (`:121`). Anstehendes, Soll-Ist und der Hinweis auf Abweichung vom Richtwert (Issue 34) sind nicht Punkt für Punkt belegt. Tom sagte am 29.10. auch, die Startseite "kommt später". |
| B-46 | Jahresblick: monatsweise geplant gegen real, auch auf der Startseite (30.10., Issue 50) | fehlt | Kein Code, keine Spezifikation, kein Meilenstein. Frage 13. |
| B-47 | Heute 01.11.: Das Freigabesystem im Haushalt wird gelockert, die Einzelrechte aus Spec 46 und 68 fallen weg, einzelne Buchungen lassen sich privat schalten (Issue 81) | teilweise | Als Auftrag mit offenen Fragen angelegt, keine Spezifikation, nichts gebaut. Gleichzeitig hat der Code am selben Tag einen neuen Freigabebereich Sparen bekommen (A-015, `Grant.cs:57`, Migration `20261101080731_AddSavingsGrantArea`): ein Recht mehr, wo Tom weniger will. Frage 11. |
| B-48 | Demokonto mit interaktiver Einführung (30.10., Issue 52) | fehlt | Kein Code, keine Spezifikation. Frage 13. |

### Datenschutz, Betrieb, Plattform

| Nr. | Beschluss (Quelle) | Status | Beleg |
|---|---|---|---|
| B-49 | Einwilligungsbaustein, Standard "nicht einverstanden"; Crisp-Chat nur nach Einwilligung und per Einstellung (Issues 35, 36) | umgesetzt | `Layout/ConsentBanner.razor`, `wwwroot/consent.js`, `Services/ConsentCategory.cs`, `Web/Duofy.Web/Components/WebRoutes.razor:31`, Einstellung `Crisp__WebsiteId` in `deploy/docker-compose.yml`, Test `Tests/Duofy.Web.Tests/CrispTests.cs` (Skript nie in der ausgelieferten Seite). Das Crisp-Konto und die Bedingungen des Gratis-Tarifs hat Tom. |
| B-50 | Cloudflare-Auswertung behalten und in die Datenschutzerklärung aufnehmen; Datenschutztexte zu Crisp und Cloudflare; Verträge (29.10., P-042, Issues 37, 43) | teilweise | Das Skript lädt erst nach Zustimmung (`WebRoutes.razor:31`, `Consent_Text` in `SharedResources.resx:77`). Textvorlagen liegen in `docs/rechtstexte/datenschutz-cloudflare.txt` und `datenschutz-crisp.txt`. Offen bei Tom: die Verträge zur Auftragsverarbeitung, die rechtliche Prüfung, das Einsetzen in die Rechtstexte des Servers. |
| B-51 | Ursache von "Rejoining server" klären (Issue 38) | teilweise | Befund vom 01.11. steht als Kommentar (Abrisse im Tunnel, Updates). Die Web-Schlüssel liegen jetzt auf einem Volume (`Web/Duofy.Web/Services/WebKeyRing.cs`, `deploy/docker-compose.yml:94`). Die Tunnel-Einstellung liegt bei Tom. |
| B-52 | Microsoft-Store-App als MAUI Windows (Entscheidung 01.11., Issue 39) | umgesetzt | Entscheidung steht als Kommentar an Issue 39. |
| B-53 | Microsoft-Store-App bauen, Kachel, Datenschutzhinweis; Einreichung macht Tom (Issue 40) | fehlt | Windows ist nur ein Testziel (`Mobile/Duofy/Duofy.csproj:42`), ungepackt und unsigniert; Kachel und Datenschutzhinweis für den Store habe ich nicht gefunden (nur `docs/store` für Android, nicht geprüft). |
| B-54 | Barrierefreiheit prüfen, Mängel als Einzel-Issues (Issue 41, Tom macht Screenreader) | ungeprüft | Keine Prüfung durch rea in den Quellen; Issue 41 steht bei Tom. |
| B-55 | Standardserver der App ist Plagevo "Plagevo (gehostet)"; Turnstile erst V2; Web-Container von der Anmeldebremse ausgenommen; Absendername "Plagevo" (26. und 27.10.) | umgesetzt | `Mobile/Duofy/appsettings.json:3`, `SharedResources.resx:87`; kein Turnstile im Code (`git grep` leer); `deploy/docker-compose.yml` (`ExemptAddresses`, `Mail__FromName` Standard Plagevo). |
| B-56 | Härtung vor dem Betrieb (P-019: Proxy-Adressen, Schlüssel bleiben, https und TLS Pflicht) | teilweise | `Api/ForwardedClients.cs` und `TrustedProxies` in der Compose-Datei, Schlüssel bleiben (`Api/KeyRing.cs`), https und TLS Pflicht auf gehosteten Servern (`Api/HostedSettings.cs:78-117`). Die Bremse je E-Mail-Adresse statt je Adresse ist ungeprüft. |
| B-57 | Einbetten in Home Assistant zurückstellen (31.10., P-066 bis P-068) | umgesetzt | Kein `frame-ancestors` im Code von `main`; der Zweig `feat/frame-ancestors` ist nicht gemergt. |
| B-58 | Marke: "Plagevo by Duofy", gehostet heißt Plagevo, selbst betrieben Duofy (26.10.) | teilweise | Code richtig (`Services/AppBrand.cs`, `Mobile/Duofy/Duofy.csproj:26`). Ob der Server "Plagevo" anzeigt, hängt an Toms `.env` (`Duofy__AppName`, Übergabe lead 01.11.): ungeprüft. |
| B-59 | Testseite nur im Entwicklungsbetrieb (29.10., P-044) | umgesetzt | `Web/Duofy.Web/Program.cs:33`. |
| B-60 | Zugriffs-Tests kennen die Route der Monatsliste (30.10., Issue 45) | umgesetzt | `Tests/Duofy.Backend.Tests/AccessWalkTests.cs:19,81`. Von Tom im Wrap-up 30.10. angenommen. |
| B-61 | Lizenz AGPL bleibt (Tom, laut Übergabe) | umgesetzt | `LICENSE` ist GNU AGPL v3. |
| B-62 | Landingpage: Beschlüsse 5, 6, 7, 11, 12 vom 30.10. und P-063 (kein Unternehmen, privates Angebot) | ungeprüft | Anderes Repo (`plagevo-landing`), nicht gelesen. |
| B-63 | Migrationen warten auf Tom (Dauerregel) | ungeprüft | Vier Migrationen vom 01.11. liegen in `main`: `AddSavingsGrantArea`, `AddAccountIcon`, `AddSavingsGoals`, `AddDebts`. Eine Freigabe von Tom steht in keiner Quelle. Frage 16. |

## Sortiert nach Dringlichkeit

### 1. Widersprüche zu Tom (5)

Der Code tut das Gegenteil von dem, was beschlossen ist. Zuerst diese.

1. **B-27** Buchungen entstehen nicht nur im Plan, sondern auch auf der Seite Buchungen, auf der Startseite und über das Plus.
2. **B-28** Das Datum einer Buchung ist nicht an den Plan-Monat gebunden; die Regel mit einem Monat Abstand gilt noch, und ein Datum in der Zukunft ist verboten. Auf einem fernen Plan-Monat kann man deshalb nicht buchen.
3. **B-19** Die eigenen Stile sind von 365 auf 1889 Zeilen gewachsen, mit 13 mal `!important`; Tom wollte wenige.
4. **B-17** Die untere Leiste hat fünf statt vier Ziele; Sparen kam ohne Toms Antwort dazu.
5. **B-03** Karten haben Schatten, beschlossen waren Linien statt Schatten (29.10.). Möglicherweise durch die Radzen-Demo überholt, das hat Tom nicht gesagt.

### 2. Fehlt und gehört zu V1 (3 von 5)

Alle drei sind von Tom beschlossen und im Meilenstein 1.0 oder in seinen Aussagen von heute und vom 30.10. enthalten.

- **B-30** Buchungslisten nach Art farbig und mit Wort (Issue 51).
- **B-32** Aufteilen bei Posten (Toms Punkt von heute; keine Spezifikation, Frage 12 zuerst).
- **B-53** Microsoft-Store-App bauen und Store-Unterlagen (Issue 40). Einreichen macht Tom.

Fehlt, aber ob V1 oder V2, hat Tom nicht gesagt (Frage 13): **B-46** Jahresblick (Issue 50), **B-48** Demokonto (Issue 52).

### 3. Teilweise (14)

B-04 Farbblöcke und Palette, B-08 Hell/Dunkel je Person, B-13 Logo und App-Symbole, B-14 Responsives Gerüst (Durchlauf auf Endstand), B-33 Übertrag (Meldung ja, Kontowahl nein), B-36 Buchungsdialog am Handy, B-43 gemeinsame und eigene Sicht, B-44 Sparen (Fallverwaltung fehlt, Anfangsbestand weicht von A-017 ab), B-45 Startseite, B-47 Haushalt lockern (Spec fehlt, Sparen-Recht dazugekommen), B-50 Datenschutztexte und Verträge, B-51 Rejoining-Ursache, B-56 Härtung, B-58 Markenname live.

### 4. Erledigt (35 Beschlüsse), Issue schließbar nach Toms Sicht (14 Issues)

Die Issues, deren Kriterien ich im Code belegen konnte. Reine Sichtkriterien (hell, dunkel, Handy) bleiben bei Tom.

| Issue | Beleg |
|---|---|
| 5 Umschalter Hell, Dunkel, Gerät | B-07 |
| 10 Seitennavigation für den Desktop | B-15, B-16 (Tom hat das Kriterium am 01.11. selbst ersetzt) |
| 14 Symbole für Kontoarten | B-10 |
| 15 Zahlart als Symbol-Knöpfe | B-11 |
| 16 Konten mit Symbolen | B-12 |
| 19 Vertrag, Schritt 3 | B-40 (der Test "gespeicherter Vertrag entspricht den Eingaben" ist nicht von mir gesehen) |
| 26 Monatsende-Auswertung | B-42 |
| 39 Microsoft-Store, Entscheidung | B-52 |
| 45 Zugriffs-Tests | B-60, von Tom angenommen |
| 46 Haushalts-Monat, letzte Buchungen | B-35, von Tom angenommen (Konflikt mit Issue 81 bedenken) |
| 47 Spike Radzen | von Tom im Wrap-up 30.10. angenommen |
| 55 Radzen Theme-Variablen | B-02, B-05, B-06 |
| 56 Radzen Hell und Dunkel | B-07 |
| 60 Radzen Profilfeld | B-18 |

Nicht schließbar, obwohl "fertig" gesagt wurde: Issue 3 (B-03 Schatten), Issue 4 (Hauptknopf gelb gegen blau, Frage 9), Issue 59 (Menü klappt nicht ein, A-024), Issue 61 (vier Ziele, B-17), Issue 76 (eigenes CSS, B-19), Issue 54 (Android, B-22), Issue 24 und 21 (siehe B-36, B-41).

Die übrigen Radzen-Teile (Issues 57, 58, 62 bis 75, 77 bis 80) habe ich nicht Punkt für Punkt gegen ihre Kriterien geprüft. Kein MudBlazor mehr und die Komponenten sind da (B-20), mehr behaupte ich nicht.

Zwei Hinweise zu bereits geschlossenen Issues: Issue 7 steht unter Tabler-Kriterien geschlossen, Tabler ist aber entfernt (B-09). Zwölf Issues plus Nummer 20 wurden am 01.11. geschlossen "per Code und Test, ohne Sichtprüfung" mit Toms Freigabe; das ist dokumentiert und in Ordnung.

### 5. Offene Fragen an Tom, einzeln

Jede Frage so, dass ein Ja, Nein oder eine Wahl reicht.

1. Buchungen nur im Plan (B-27): Soll "Neue Buchung" nur noch im Monatsplan existieren, also das Plus auf der Startseite, im unteren Menü und auf der Seite Buchungen wegfallen? Und was wird dann aus dem Menüpunkt Buchungen: als reine Liste zum Ansehen und Bearbeiten behalten, in Bewegungen am Konto umbenennen, oder ganz weg?
2. Datum der Buchung (B-28): In einem Plan-Monat, der noch nicht erreicht ist (Dezember heute), liegt jedes Datum in der Zukunft, und heute ist ein Datum in der Zukunft verboten. Soll ein künftiges Datum erlaubt sein, oder soll man nur in laufenden und vergangenen Plan-Monaten buchen?
3. Rundung (B-02): Gilt 12 px für alles, auch für die Farbblöcke und Knöpfe, oder sollen Blöcke und Knöpfe eckig sein, wie am 29.10. beschlossen ("eckige Knöpfe")?
4. Schatten (B-03): Sollen Karten einen leichten Schatten haben, wie in der Radzen-Demo, oder Linien ohne Schatten, wie am 29.10.?
5. Farbblöcke (B-04): Einnahmen Salbei, Festes Korall, Freies Gelb (29.10.), oder Grün, Rot und Gelb der Palette Altes Duofy, wie jetzt gebaut?
6. Untere Leiste (B-17): Vier Ziele (welche?) oder die jetzigen fünf mit Sparen? Und gehört Sparen ins Menü am PC, obwohl es in deiner Liste vom 01.11. nicht steht?
7. Eigene Stile (B-19): Ab welcher Größe ist "wenige" erreicht? Soll ich `app.css` auf eine von dir genannte Zeilenzahl zurückbauen und die `!important` abschaffen, auch wenn dabei Feinheiten (Abstände, Tippflächen) wegfallen?
8. Hell/Dunkel merken (B-08): Je Gerät, wie heute, oder am Konto, sodass die Wahl auf jedes Gerät folgt?
9. Dunkles Design (B-05): Bleibt der Hauptknopf im Dunkelmodus blau (`#86ace0`, 30.10.) oder gelb mit dunkler Schrift (Issue 4, 29.10.)?
10. Umbuchung (B-24): Sind beide Töpfe Pflicht, Quelle und Ziel, und darf es derselbe sein? Ist das, was gebaut ist, der Umfang, den du bei deinem Einspruch gemeint hast (Karte P-062)?
11. Haushalt lockern (B-35, B-47): Bis die Spezifikation zu Issue 81 steht, bleiben die letzten Buchungen im Haushalts-Monat nur mit Freigabe. Ist das richtig? Und soll der neue Freigabebereich Sparen bleiben, obwohl du die Einzelrechte abschaffen willst?
12. Aufteilen bei Posten (B-32): Was soll aufgeteilt werden: ein Posten auf mehrere Töpfe oder Kategorien, oder sein Betrag auf mehrere Monate? Ein Beispiel von dir reicht.
13. Jahresblick und Demokonto (B-46, B-48): Gehören beide noch in Version 1, oder in V2?
14. Knopfbeschriftung: Die Knöpfe von Radzen schreiben alles in Großbuchstaben. Das alte Theme hatte normale Schreibung. Welche willst du? (Kein Beschluss dazu in den Quellen.)
15. Übertrag (B-33): Soll der Übertrag-Dialog eine Kontowahl bekommen, damit eine neue Person im ersten Monat nicht an ihrem Standardkonto scheitert?
16. Migrationen (B-63): Sind die vier Migrationen vom 01.11. (Sparen-Recht, Kontosymbol, Sparziele, Schulden) von dir freigegeben, oder hätten sie auf dich warten müssen?
17. Sparziel (B-44): Soll es ein Feld "Schon angespart" als Anfangsbestand geben, wie im Code, oder gar keinen Anfangsbestand, wie meine Annahme A-017 sagt?
18. Summenzeile im Plan (B-34): Soll die Zeile "gebucht" im Plan den Übertrag mitzählen (heute ja, Oktober zeigt 3000 gebucht auf beiden Seiten), oder ohne?
19. Fallverwaltung mit Mahnstufen (B-44, P-057): Gehört sie in Version 1 oder später?

## Ungeprüft (4)

B-22 Radzen auf dem Android-Telefon (Tom). B-54 Barrierefreiheit (Screenreader, Tom und rea). B-62 Landingpage (anderes Repo). B-63 Freigabe der Migrationen (Frage 16).

## Annahmen, die Tom noch nicht gesehen hat

Im Annahmenprotokoll steht der Zustand "logged" ohne Antwort von Tom bei A-002 bis A-007 und A-011 bis A-025. Angenommen hat er laut Wrap-up 30.10. nur A-009 und A-010; widersprochen hat er A-008. Diese haben Folgen, die ich oben schon als Widerspruch oder Frage geführt habe: A-015 (Sparen als fünftes Ziel und eigener Freigabebereich, B-17 und B-47), A-021 (abgeleitete Farben, B-04), A-024 (Menü klappt unter 1280 px nicht ein, Issue 59), A-017 (kein Anfangsbestand, Code hat einen), A-019 (zwei Spalten auf der Startseite erst ab 1280 px), A-022 (StepDialog bleibt in beiden Welten, damit nichts bricht), A-016 (Beschriftung "Benutzerkonto").

## Was das für die Reihenfolge heißt

Gebaut wird erst nach Toms Antworten auf die Fragen 1, 2, 6 und 7, weil sie die Widersprüche B-27, B-28, B-17 und B-19 entscheiden. Ob B-03 eine Korrektur ist, entscheidet Frage 4. Die Fragen 3, 5, 8, 9 und 14 sind Aussehen und kosten wenig. Fragen 10 bis 13, 15 bis 19 sind Fachfragen und blockieren nur ihre eigene Sache.
