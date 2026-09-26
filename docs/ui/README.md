# UI-Leitfaden

Dieser Leitfaden sagt, wie die Oberfläche von Duofy bedient wird. Er richtet sich an alle, die eine
Oberflächenänderung bauen, und an alle, die sie prüfen. Jede Änderung an der Oberfläche wird gegen ihn
gebaut und im Review gegen die Prüfliste am Ende gehalten.

Der Grundgedanke: **Gleiche Dinge werden gleich bedient.** Wer eine Liste kennt, kennt alle. Das hilft
besonders Menschen, die Vorhersehbarkeit brauchen oder schnell abgelenkt sind, und es macht die App
schneller zu lernen.

Die Regeln sind das Ziel. Nicht jede ist schon umgesetzt; die Umbauten laufen als eigene Issues. Wo
Code und Leitfaden sich widersprechen, gilt der Leitfaden, und der Code wird angepasst.

Die Beispiele nennen nur erfundene Demo-Daten (siehe „Keine echten Daten“ in `CLAUDE.md`).

## Zeile

**1. Ein Klick auf eine Zeile öffnet sie zum Bearbeiten, überall in der Zeile.**
Name, Untertitel, Betrag und Leerraum tun dasselbe; nur die eigenen Bedienelemente der Zeile (das
Kästchen zum Abhaken, das Menü ⋯) tun etwas anderes.
*Warum:* Dieselbe Geste soll überall dasselbe bewirken. Große Flächen sind leicht zu treffen.
*So:* Klick auf „Miete“ oder auf den Betrag daneben öffnet „Posten bearbeiten“. *Nicht so:* Nur der
Name ist klickbar, der Betrag tut nichts, und bei Verträgen geht Bearbeiten nur über ein Menü.

**2. Was klickbar ist, sieht klickbar aus; was nicht, sieht nicht so aus.**
Klickbare Zeilen haben Hand-Cursor, leichten Hintergrund beim Überfahren, sichtbaren Fokusring, sind
mit Tab erreichbar und öffnen mit Enter. Nicht klickbare Karten (Kennzahlen, Kontostände im Buch)
haben weder Hover noch Ring oder Schatten, die wie eine Schaltfläche wirken.
*Warum:* Wer Hover und Ring sieht, erwartet eine Wirkung; bleibt sie aus, verliert man Vertrauen.
*So:* Eine Kennzahl ist flach. *Nicht so:* Eine Kennzahl mit Ring und Schatten, die nichts tut.

**3. Fehlt das Recht, fehlt die Bedienung.**
Im Lesemodus gibt es kein Hover, kein abgegrautes ⋯ und keinen abgeschalteten Knopf. Der Zustand
(etwa ein gesetzter Haken) bleibt sichtbar, ein Satz oben sagt „Nur zum Ansehen“.
*Warum:* Ein abgegrauter Knopf fragt „warum nicht?“ und beantwortet es nicht.
*So:* Fremder Plan: Zeilen ohne Hover, oben der Satz. *Nicht so:* Ein grauer Knopf, der nichts erklärt.

**4. Was man anlegen kann, kann man bearbeiten.**
Das gilt auch für die Buchung. Beträge stehen rechtsbündig und formatiert und sind nie ein eigener
Knopf.
*Warum:* Wer sich vertippt hat, soll nicht löschen und neu anlegen müssen.
*So:* Klick auf eine Buchung öffnet sie zum Bearbeiten. *Nicht so:* Buchung falsch, also löschen.

## Menü und Löschen

**5. Das Menü ⋯ enthält nur, was nicht Bearbeiten ist.**
Beenden, Archivieren, Duplizieren, Löschen, Austreten. Hat eine Zeile keine solche Aktion, hat sie kein
⋯.
*Warum:* Bearbeiten geht schon mit einem Klick auf die Zeile; ein Menü mit einem einzigen Eintrag ist
Umweg und Lärm.
*So:* Buchungszeile ohne ⋯ oder mit „Löschen“ darin. *Nicht so:* ⋯ mit dem einen Eintrag „Bearbeiten“.

**6. Menüs sind geordnet, ehrlich und groß genug.**
Häufiges vor Seltenem, Zerstörendes zuletzt, rot und durch eine Linie abgesetzt. Jeder Eintrag ist ein
Verb im Infinitiv mit Symbol. Nie ein Eintrag ohne Funktion. Der Knopf ⋯ trägt Namen und Objekt
(„Miete: weitere Aktionen“), ist mindestens 32 px groß und sitzt am rechten Zeilenrand.
*Warum:* Wer ein Menü öffnet, soll ohne Lesen wissen, wo Gefährliches steht; tote Einträge zerstören
das Vertrauen in die übrigen.
*So:* „Beenden“, Linie, „Löschen“ (rot). *Nicht so:* „Einstellungen“ im Menü, obwohl es nichts öffnet.

**7. Löschen sitzt im ⋯-Menü und ist umkehrbar oder erklärt.**
- Kleines (Buchung, Posten, Import-Zeile) wird sofort gelöscht; die Meldung bietet zehn Sekunden
  „Rückgängig“ an. Der Client verzögert dazu den Aufruf, das Backend bleibt unverändert.
- Verträge und Konten sind nur bis zur ersten Nutzung löschbar (Vertrag in keinem Plan, Konto ohne
  Buchungen). Danach gibt es nur Beenden beziehungsweise Archivieren. Das ist eine feste Regel, keine
  Einstellung.
- Was sich sonst nicht zurückholen lässt (etwa einen Haushalt verlassen), fragt einmal nach, und der
  Satz sagt, was verloren geht und was bleibt.

*Warum:* „Rückgängig“ ist besser als „Bist du sicher?“, weil man Rückfragen wegklickt. Wer etwas
Benutztes löscht, reißt Daten mit; deshalb gibt es dort nur Beenden.
*So:* Posten löschen, Meldung „Miete gelöscht“ mit „Rückgängig“. *Nicht so:* Papierkorb in der Zeile
ohne Rückgängig, oder ein Löschen-Knopf im Dialogfuß.

## Aktionen

**8. Höchstens ein Hauptknopf je Seite, oben rechts, immer an derselben Stelle.**
Er ist gefüllt, hat ein Symbol und ist in jeder Ansicht gleich groß, auch im fremden Plan. Alles andere
ist ein Randknopf oder Textknopf; „Drucken“ ist ein Randknopf daneben.
*Warum:* Wer die Seite wechselt, soll die wichtigste Aktion nicht suchen.
*So:* „+ Vertrag anlegen“ oben rechts. *Nicht so:* Vier graue „+ Posten in …“-Knöpfe zusätzlich zum
Hauptknopf.

**9. Neues heißt überall „… anlegen“; der Dialogknopf wiederholt das Verb des Auslösers.**
„Hinzufügen“ und „Sichern“ entfallen. Beim Bearbeiten heißt der Knopf „Speichern“. Ein Leerzustand
wiederholt den Hauptknopf der Seite.
*Warum:* Ein Wort, das seinen Namen wechselt, ist eine Fehlerquelle.
*So:* Auslöser „Konto anlegen“, Dialogknopf „Anlegen“. *Nicht so:* Auslöser „Konto anlegen“,
Dialogknopf „Sichern“.

**10. Abhaken bleibt ein schlanker Dialog, Betrag und Datum sind sofort änderbar.**
Das Kästchen ist ein eigener Knopf, mindestens 24 px groß, und öffnet nur diesen Dialog. Das
Buchungsdatum ist nicht der Planmonat: Eine Zahlung am Monatsende kann in den nächsten Plan gehören.
Die Meldung danach sagt, was passiert ist.
*Warum:* Der tatsächliche Betrag weicht oft vom Plan ab; ein Dialog, den man erst öffnen muss, um ihn
zu korrigieren, ist eine Hürde.
*So:* Meldung „Miete abgehakt: 950,00 € gebucht.“ *Nicht so:* „Posten aktualisiert“.

## Überlagerungen

**11. Das Muster folgt der Aufgabe.**
Kurze Frage mit bis zu fünf Feldern oder Ja/Nein: Dialog. Längeres Formular mit Gruppen: Seitenblatt.
Auswahl aus einer Liste: Popover. Rückfrage mit Folgen: Bestätigungsdialog. Bearbeiten in der Zeile
selbst gibt es nicht.
*Warum:* Wer das Muster kennt, weiß, was passiert, bevor er klickt.
*So:* „Konto anlegen“ als Dialog. *Nicht so:* Ein Popover mit sieben Feldern.

**12. ✕, Esc, Klick daneben und „Abbrechen“ tun dasselbe: schließen ohne Speichern.**
Hat man etwas geändert, schließt Klick daneben nicht mehr, und Esc fragt einmal „Änderungen
verwerfen?“. Bestätigungsdialoge schließen nie durch Klick daneben.
*Warum:* Wer mitten im Tippen danebenklickt, verliert sonst alles.
*So:* Feld geändert, Klick daneben: nichts passiert. *Nicht so:* Dialog schließt, Eingabe weg.

**13. Der Fokus geht mit.**
Beim Öffnen ins erste Feld (im Bestätigungsdialog auf den sicheren Knopf „Abbrechen“), beim Schließen
zurück zu dem Element, von dem man kam.
*Warum:* Tastaturnutzer und Screenreader verlieren sonst den Faden.
*So:* Dialog zu, Fokus wieder auf dem ⋯ der Zeile. *Nicht so:* Fokus landet irgendwo auf der Seite.

**14. Die Fußzeile ist immer gleich, und der Dialog bleibt offen, bis der Server „ja“ gesagt hat.**
Rechts „Abbrechen“ (Randknopf) und daneben der Hauptknopf; links nichts. Der Knopf zeigt „Speichert…“
und ist gesperrt. Ein Fehler steht im Dialog, die Eingabe bleibt. Zwei Breiten (schmal für Fragen,
normal für Formulare), ein Titelstil.
*Warum:* Ein Dialog, der bei einem Fehler schließt, wirft die Eingabe weg und lässt offen, ob etwas
ankam.
*So:* Server sagt nein, Dialog bleibt, Fehlerbox über den Knöpfen. *Nicht so:* Dialog schließt, Fehler
verschwindet.

## Rückmeldung

**15. Jede Änderung meldet sich mit einem Satz aus Objekt und Tat.**
Mit Namen oder Betrag, wenn möglich. Gibt es eine Umkehr, steht „Rückgängig“ (zehn Sekunden) daran,
sonst genügen fünf Sekunden. Es steht immer nur eine Meldung, an fester Stelle unten mittig.
*Warum:* Eine App mit Geld muss zeigen, dass etwas ankam. Unten mittig überdeckt sie die Hilfe rechts
nicht.
*So:* „Buchung gelöscht. Rückgängig“. *Nicht so:* Zwei Meldungen übereinander ohne Knopf.

**16. Ein Fehler verschwindet nie lautlos.**
Er steht dort, wo man ihn beheben kann: in einer Bauform (Box mit Rand und roter Fläche,
`role="alert"`) im Dialog über den Knöpfen. Aktionen ohne Formular (Haken, Löschen, Austreten) melden
den Fehler als Meldung, die stehen bleibt und „Erneut versuchen“ anbietet. Darunter fängt ein
gemeinsamer Fehlerweg alles ab, was niemand einzeln behandelt.
*Warum:* „Still gescheitert“ ist der schlimmste Zustand, weil man dem Bildschirm glaubt.
*So:* „Konnte nicht gespeichert werden. Erneut versuchen“. *Nicht so:* Nichts passiert, kein Hinweis.

**17. Laden und Leere haben je eine Bauform.**
Listen zeigen graue Platzhalter. Knöpfe zeigen „Speichert…“ (immer „…“, kein Leerzeichen davor) und
sind gesperrt. Ein Leerzustand ist ein Satz, der sagt, was hier steht und wie es entsteht, plus der
Hauptknopf der Seite.
*Warum:* Wer noch nichts angelegt hat, sucht den Knopf dort, wo der Text ihn nennt.
*So:* „Noch kein Vertrag.“ mit Knopf „+ Vertrag anlegen“. *Nicht so:* Nackter grauer Text ohne Knopf.

## Navigation

**18. Alles Ansehbare hat eine Adresse.**
Monat (zweistellig, `/plan/2026/09`), Reiter (`?tab=`), Filter (`?status=`), Person (`?member=`),
Buchmonat (`/book?month=2026-09`). Dialoge und Seitenblätter haben keine. Eine ungültige Adresse zeigt
die Nicht-gefunden-Seite; ein gültiger Monat ohne Plan zeigt „Monat anlegen“ mit genau diesem Monat
vorbelegt.
*Warum:* Eine Adresse ist Gedächtnis: Lesezeichen, Zurück-Taste und Neuladen funktionieren.
*So:* Buchmonat im Lesezeichen. *Nicht so:* Neuladen springt zurück auf heute.

**19. Zurück ist sichtbar, Monate wechselt man überall gleich.**
Der Rückweg heißt wie das Ziel („← Alle Pläne“). Monate wechselt man mit ‹ Monat Jahr › in der
Kopfzeile, Pfeile mit Namen („Vorheriger Monat“, „Nächster Monat“), Stand in der Adresse. Reiterwechsel
füllen den Verlauf nicht. Ein Menüpunkt ist ein Element (der Link ist der Knopf, nicht ein Knopf im
Link).
*Warum:* Zurück muss immer gehen; „Monat wechseln“ darf nicht auf drei Arten gehen.
*So:* Im Plan und im Buch dieselbe Baugruppe. *Nicht so:* Im Buch Pfeile ohne Monatsnamen, im Plan gar
keine.

**20. Die Kopfzeile zeigt den Seitentitel, die Hilfe ein festes „?“.**
Oben rechts sitzt der Knopf „?“; er öffnet die Hilfe als Seitenblatt auf jeder Breite. Auf großen
Bildschirmen kann sie dauerhaft offen bleiben, die Wahl wird gemerkt. Ein Platzhalter im Kopf ist
verboten.
*Warum:* Hilfe soll an derselben Stelle erreichbar sein und nichts verdecken.
*So:* Kopfzeile „Verträge“ und „?“. *Nicht so:* Ein fester Platzhaltertext in der Kopfzeile.

## Formulare

**21. Pflicht ist der Normalfall; freiwillig sagt es am Feld.**
Ein Formular zeigt zuerst nur das Nötige, den Rest unter „Weitere Angaben“.
*Warum:* Weniger Felder auf den ersten Blick senken die Last.
*So:* IBAN mit „optional“. *Nicht so:* Zehn Felder, von denen man drei ausfüllen muss, ohne es zu
sehen.

**22. Geprüft wird beim Absenden, in Duofys Worten, unter dem Feld.**
Alle Fehler auf einmal, der Fokus springt auf das erste. Keine Sprechblasen des Browsers.
*Warum:* Deren Wortlaut hängt vom Browser ab und nennt immer nur einen Fehler.
*So:* „Bitte einen Betrag über 0,00 € eingeben.“ unter dem Feld. *Nicht so:* „Fülle dieses Feld aus.“

**23. Ein Betragsfeld für alle Beträge.**
Texteingabe mit Zifferntastatur, Komma und Tausenderpunkt erlaubt, kein „e“, rechtsbündig, „€“ als
Endung, mindestens 0,01 (das Vorzeichen kommt aus der Art: Einnahme oder Ausgabe), zwei Nachkommastellen
beim Verlassen des Felds.
*Warum:* Ein Geldfeld, das „12e3“ nimmt und „1.234,56“ ablehnt, spricht nicht die Sprache der Nutzer.
*So:* „1.234,56“ wird zu „1.234,56 €“. *Nicht so:* „1.234,56“ wird abgelehnt.

**24. Ein Datum hat eine Bauform je Aufgabe, und Vorbelegung spart das Tippen.**
Ein Tag ist ein Kalender im Popover; zählt nur Monat und Jahr, ein Monatswähler; ein Tag im Monat ist
ein Zahlfeld mit „am [n]. jedes Monats“. Vorbelegt ist, was in neun von zehn Fällen stimmt, damit Enter
genügt (Monat anlegen: der nächste fehlende Monat). Alles in einem Formular wirkt erst mit „Speichern“;
was sofort wirkt (Freigabe, Filter, Person), steht außerhalb von Formularen, sieht anders aus
(Umschalter, Reiter) und meldet sich.
*Warum:* Wer sich zwischen drei Datumswegen entscheiden muss, macht Fehler; wer nicht weiß, ob eine
Auswahl sofort wirkt, prüft nach.
*So:* Neuer Monat: nächster fehlender Monat vorgewählt. *Nicht so:* Immer der heutige Monat, auch wenn
es ihn schon gibt.

## Wörter

**25. Ein Ding, ein Wort.**
Die Begriffe stehen in der Tabelle „Begriffe“ in `CLAUDE.md`. In der Oberfläche gilt:

- **Frei** ist, was noch keinem Posten zugeteilt ist; bei 0 steht „Alles verplant“.
- **Anstehend** ist, was verplant, aber noch nicht abgehakt oder bezahlt ist.
- **Grundbedarf** heißt das Budget `needs`, überall (nicht Fixkosten, nicht Bedarf).
- **Anlegen** für Neues, **Speichern** beim Bearbeiten; „Hinzufügen“ und „Sichern“ gibt es nicht.
- **Beenden** für Verträge, **Archivieren** für Konten, sobald sie genutzt wurden.

*Warum:* Ein Wort, das seinen Namen wechselt, kostet Menschen mit Bedarf an Vorhersehbarkeit jedes Mal
Aufmerksamkeit.
*So:* „Frei: 120,00 €“. *Nicht so:* „Offen“ für zwei verschiedene Dinge.

## Bausteine

Damit die Regeln im Code stehen und nicht nur im Text, braucht es gemeinsame Komponenten. Sie sind das
**Ziel**; die meisten gibt es noch nicht.

- **Listenzeile** (Regeln 1–4): eine Zeile, bei der der Name die ganze Fläche aufspannt; Kästchen und ⋯
  liegen darüber. Kennt Hover, Fokusring und Lesemodus.
- **Zeilenmenü** (5–7): das ⋯ mit Namen, Größe, Reihenfolge und rotem Zerstörenden am Ende.
- **Rückgängig-Helfer** (7, 15): verzögertes Löschen im Client mit der einen Meldung und dem Knopf
  „Rückgängig“.
- **Dialograhmen** (11–14): Titel, Fuß, Fokus, Wächter für ungespeicherte Änderungen, Sperre bis zur
  Serverantwort.
- **Fehlerweg** (16): eine Fehlerbox im Dialog und ein gemeinsamer Fänger für alle Änderungen.
- **Leerzustand** (17): ein Satz plus Hauptknopf der Seite.
- **Monatswechsel** (19): ‹ Monat Jahr › mit benannten Pfeilen und Stand in der Adresse.
- **Betragsfeld** (23): ein Feld, eine Regel für alle Beträge.

## Prüfliste für Reviews

- [ ] Öffnet ein Klick auf die Zeile das Bearbeiten, und sind Kästchen und ⋯ eigene Ziele?
- [ ] Sieht Klickbares klickbar aus (Hand, Hover, Fokusring, Tab, Enter), Nicht-Klickbares nicht?
- [ ] Fehlt bei fehlendem Recht die Bedienung, statt abgegraut zu sein?
- [ ] Enthält das ⋯ nur Aktionen außer Bearbeiten, ohne toten Eintrag, Zerstörendes zuletzt und rot?
- [ ] Löscht Kleines sofort mit „Rückgängig“, und sind Verträge und Konten nur bis zur ersten Nutzung löschbar?
- [ ] Gibt es höchstens einen Hauptknopf, oben rechts, und heißt Neues „… anlegen“?
- [ ] Ist das Überlagerungsmuster (Dialog, Seitenblatt, Popover, Bestätigung) passend gewählt?
- [ ] Schließen ✕, Esc, Klick daneben und „Abbrechen“ gleich, und geht nach Änderungen nichts ungefragt verloren?
- [ ] Geht der Fokus beim Öffnen ins erste Feld und beim Schließen zurück zum Auslöser?
- [ ] Bleibt der Dialog bis zur Serverantwort offen, mit „Speichert…“ und dem Fehler im Dialog?
- [ ] Meldet jede Änderung sich mit einer Meldung (eine, unten mittig), und verschwindet kein Fehler lautlos?
- [ ] Haben Laden und Leere ihre Bauform, der Leerzustand mit Hauptknopf?
- [ ] Hat alles Ansehbare eine Adresse, und funktionieren Zurück und Monatswechsel?
- [ ] Werden Formulare beim Absenden in Duofys Worten geprüft, mit dem gemeinsamen Betragsfeld?
- [ ] Stimmen die Wörter (Frei, Anstehend, Grundbedarf, anlegen) mit der Begriffstabelle überein?
