# Texte der Erklärspalte (R13)

Stand: 03.11.2026. Von Tom am 03.11.2026 freigegeben („Ja, so einbauen“) mit zwei Korrekturen des Leads: „Letzte Buchungen“ auf Start gestrichen, Beginn der Tageskurve präzisiert. Am 03.11.2026 ergänzt nach Toms Entscheidung „Am 1. des Monats“: Einnahmen, die noch nicht gebucht sind, zählen am Monatsanfang.

Änderung vom 06.11.2026 (Issue #94, Start und Plan-Übersicht): Die Texte „Dein Monat auf einen Blick“, „Töpfe in Euro“ (neu), „Haushalt und Jahr“ (neu), „Die Monatsübersicht“, „Ungeplant“ und „Geänderte Verträge“ beschreiben die neue Oberfläche. Von Tom am 06.11.2026 freigegeben, mit einer Änderung: In „Ungeplant“ entfällt der Halbsatz zum Ist der Richtwerte.

Die Erklärspalte steht am PC ab 1280 Pixel Breite rechts neben dem Inhalt, wie im alten Duofy. Jede Seite hat zwei bis vier kurze Abschnitte, jeder mit Überschrift und ein bis drei Sätzen. Die Texte folgen dem Ton des alten Duofy (`Duofy-alt/frontend/src/locales/de.help.json`), beschreiben aber nur Regeln, die es bei uns gibt. Hinter jedem Abschnitt steht in Klammern, wo die Regel im neuen Duofy festgelegt ist; diese Klammer erscheint nicht in der App.

Am Ende steht, was aus dem alten Duofy bewusst fehlt und warum.

## Start

**Dein Monat auf einen Blick**
Hier steht der laufende Monat: was verplanbar ist, was auf deinen verfügbaren Konten liegt und wie viel du in jedem Topf schon gebucht hast, gemessen an dem, was du geplant hast. (Start, Issue #94 R1, R2)

**Anstehend und überfällig**
Anstehend ist eine Zahlung, die heute oder in den nächsten sieben Tagen fällig ist und noch nicht abgehakt wurde. Überfällig ist sie ab dem Tag nach ihrer Fälligkeit, ohne Schonfrist. Einnahmen und durchlaufende Posten stehen nicht in dieser Liste. (MonthView.UpcomingDays, DueRows)

**Töpfe in Euro**
Jeder Topf zeigt, wie viel du schon gebucht hast und wie viel du geplant hast. Ist mehr gebucht als geplant, endet der Balken rot, und der Topf ist überzogen. Jeder Topf hat überall dieselbe Farbe: Grundbedarf blau, Wünsche gelb, Sparen grün. (Start, Plan-Übersicht, Monat; Issue #94 R2, R3)

**Haushalt und Jahr**
Bist du in einem Haushalt, steht unter deinen Zahlen ein kleinerer Block mit Verplanbar und den Töpfen des Haushalts. Die Jahreslinie zeigt für jeden Monat bis heute, was übrig war (grün) oder fehlte (rot); ein Tipp öffnet die Monatsübersicht. (Start; Issue #94 R4, R5)

## Meine Planung (Monatsübersicht)

**Die Monatsübersicht**
Oben steht der laufende Monat, darunter die vergangenen. Jeder zeigt Verplanbar, was noch ansteht und seine drei Töpfe: Der Balken eines Topfs ist die geplante Summe, gefüllt ist, was schon gebucht ist. Künftige Monate stehen nur da, wenn es sie schon gibt; den nächsten legst du mit „Nächsten Monat anlegen“ an. Steht beim laufenden Monat „noch nicht angelegt“, entsteht er beim ersten Öffnen. (Komponente Plan, Regel 1; Issue #94 R6, R7)

**Wie ein Monat entsteht**
Beim ersten Öffnen übernimmt Duofy alle Verträge, die in diesem Monat fällig sind, als Posten. Die Posten sind Kopien: Was du im Monat änderst, ändert den Vertrag nicht. (Komponente Plan, Regel 1)

**Wann geplant wird**
Ein Monat wird nicht abgeschlossen. Geplant wird vor dem Monat, nachgetragen wird laufend. (Komponente Plan: kein Abschluss vorhanden)

## Monat, Reiter Plan

**Was der Monatsplan ist**
Der Plan enthält alles, was in diesem Monat hereinkommen und hinausgehen soll. Er beschreibt den Soll-Zustand; was tatsächlich passiert ist, steht unter Buchungen. (Komponente Plan, Zweck)

**Verplanbar**
Die geplanten Einnahmen minus die geplanten Ausgaben, dazu ein Übertrag aus dem Vormonat, wenn es einen gibt. Jeder Posten zählt mit dem geplanten Betrag oder, wenn mehr gebucht ist, mit dem gebuchten, auch abgehakt. Gebuchte Ausgaben ohne Posten werden abgezogen, Einnahmen ohne Posten kommen dazu. Geld, das du von einem nicht verfügbaren Konto auf ein verfügbares zurückholst, erhöht Verplanbar; Geld, das du ohne Posten auf ein nicht verfügbares Konto zurücklegst, senkt es. Der Text an der Zahl sagt, ob ein Übertrag enthalten ist. (Komponente Plan: free amount, PlanFigures.Free)

**Posten abhaken**
Der Haken öffnet einen Dialog mit dem heutigen Datum und dem geplanten Betrag; beides lässt sich vor dem Bestätigen ändern. Erst dann entsteht die Buchung, und der Posten fällt aus „Anstehend“. Liegen auf dem Posten schon Buchungen, entsteht keine weitere. Ein anderes Buchungsdatum verschiebt den Posten nicht in einen anderen Monat. (PlanTickDialog, Spec 82 A11)

**Ungeplant**
Was du ohne Posten gebucht hast, steht als Zeile Ungeplant am Ende des Topfs, in dem du gebucht hast, mit Anzahl und Summe der Buchungen. Ein Tipp zeigt die Buchungen. Es zählt beim gebuchten Betrag mit. Umbuchungen zwischen deinen Konten und der Übertrag sind keine Ausgabe und fehlen hier. (Komponente Plan: Booked per pot, Spec 72 R5; Issue #94 R8)

**Übertrag**
Der Übertrag hält fest, was vom Vormonat wirklich übrig ist, auch null oder weniger. Vorgeschlagen wird der gerechnete Stand; weicht deine Bank ab, trägst du den echten Stand ein und entscheidest, ob noch Buchungen fehlen oder der Unterschied ausgeglichen wird. (PlanCarryOverDialog, Komponente Plan)

**Geänderte Verträge**
Hat sich ein Vertrag geändert, nachdem der Monat entstanden ist, zeigt der Monat das oben als eine schmale Zeile „Verträge geändert · Ansehen“. Ein Tipp zeigt die Unterschiede und die Knöpfe zum Übernehmen. Übernommen wird es erst, wenn du zustimmst; abgehakte und von Hand geänderte Posten ändert Duofy nie von selbst. (Komponente Plan, Regel 2; Issue #94 R9)

## Monat, Reiter Buchungen

**Was hier steht**
Alle Buchungen, die in diesem Monat zählen, nach Datum. Ausgaben stehen rot mit Minus, Einnahmen ohne Vorzeichen. (Komponente Booking: Month book)

**Datum und Planmonat**
Eine Buchung kann in einem anderen Monat zählen, als ihr Datum sagt, höchstens einen Monat davor oder danach. Ein Gehalt vom 28. September kann zum Oktober gehören. (Komponente Booking, Spec 66 R7)

**Umbuchung**
Geld zwischen zwei eigenen Konten ist keine Ausgabe, es liegt nur woanders. Gehört zur Umbuchung ein anderer Topf als Quelle und Ziel, verschiebt sie den Betrag zwischen den Töpfen: Der Quelltopf sinkt, der Zieltopf steigt, beim gebuchten Betrag und beim Richtwert, in ihrem Planmonat. Verplanbar, das Feste und die Kontostände ändern sich dadurch nicht; ein Quelltopf darf dabei unter null fallen. (Komponente Booking, Spec 112)

## Monat, Reiter Auswertung

**Soll und Ist**
Die runden Diagramme zeigen je Topf und je Kategorie, was geplant ist und was davon gebucht ist. Solange im Monat noch keine Einnahme gebucht ist, rechnet Duofy die Anteile mit den geplanten Einnahmen. (Spec 82, Änderung vom 03.11.2026)

~~Der Geldfluss~~ – entfällt nach Toms Entscheidung 03.11.2026 ("sankey ... eigentlich kanns weg"), mit dem Flussdiagramm.

**Die Tageskurve**
Die Kurve zeigt den Stand über den Monat: bis heute aus den echten Buchungen, ab morgen aus den offenen Posten an ihrem Fälligkeitstag. Sie beginnt mit dem Übertrag und den Einnahmen, die schon zu diesem Monat gehören. Einnahmen, die noch nicht gebucht sind, zählen am Monatsanfang. Fällt sie unter null, markiert Duofy den Tag und nennt den Betrag der Lücke. (Spec 82, Änderung vom 03.11.2026; Korrektur des Leads: Buchungen dieses Planmonats mit Datum vor dem Monatsanfang und geplante Einnahmen mit Fälligkeit im Vormonat zählen am 1. Tag)

## Unser Haushalt

**Was ein Haushalt ist**
Ein Haushalt sagt, wer zusammen plant, mehr nicht. Er besitzt nichts: Konten, Verträge, Pläne und Buchungen gehören immer einer Person. Du bist in höchstens einem Haushalt, und in keinem, bis du einen gründest oder beitrittst. (Komponente Household, Zweck und Regel 1)

**Wer ihn verwaltet**
Wer den Haushalt gegründet hat, verwaltet ihn allein: Name, Richtwerte, Einladungen und Mitglieder. Weitere Rollen gibt es nicht. (Komponente Household, Regel 2)

**Einladen**
Eine Einladung ist ein Link, auch als QR-Code. Wer ihn hat und in keinem Haushalt ist, kann einmal damit beitreten, innerhalb von 14 Tagen. Bis zu zehn Einladungen können gleichzeitig offen sein. (Komponente Household, Invitation)

**Austreten**
Wer austritt oder entfernt wird, ist danach in keinem Haushalt; Daten wandern nicht. Wer gegründet hat, kann erst austreten, wenn alle anderen gegangen sind; dann löst sich der Haushalt auf. (Komponente Household, Regeln 3 und 4)

## Haushaltsplan

**Was der Haushaltsplan ist**
Er ist keine eigene Liste, sondern die Zusammenstellung der Monatspläne aller Mitglieder, gruppiert nach Person. Jeder Posten existiert genau einmal, im Plan seiner Person. (HouseholdPlanStore, Spec 69)

**Nur zum Lesen**
Geändert und abgehakt wird im Plan der jeweiligen Person. Hier siehst du, wie der Monat für alle zusammen aussieht. (HouseholdPlan_ReadOnly)

**Wer mitzählt**
In einem Monat zählt jedes Mitglied, das bis zum letzten Tag des Monats dazugekommen ist. Konten anderer siehst du nur, wenn sie sie dir freigegeben haben; wer fehlt, steht dabei. (HouseholdPlanStore R1, R8)

**Gemeinsame Richtwerte**
Der Haushalt hat eigene Richtwerte, getrennt von denen jeder Person. Sie stellt ein, wer den Haushalt gegründet hat. (Komponente Household: Household quotas)

## Freigaben

**Was du freigibst**
Du legst für jede Person im Haushalt einzeln fest, was sie mit deinen Daten tun darf, getrennt für Planung, Buchungen, Konten, Verträge sowie Sparen und Schulden. Nur du vergibst das, und jede Wahl gilt sofort. (Komponente Grant, Grants_Lead)

**Die Stufen**
Nichts, Ansehen, Anlegen, Ändern und Löschen. Jede Stufe enthält die davor: Wer ändern darf, darf auch ansehen und anlegen. (Komponente Grant: Level)

**Vorlagen**
„Partner: alles“, „Mitlesen“, „Kind“ und „Nichts“ setzen alle Bereiche auf einmal. Änderst du danach einen Bereich, ist es eine eigene Auswahl. (GrantPresets)

**Wann eine Freigabe gilt**
Nur solange ihr im selben Haushalt seid. Wer austritt oder entfernt wird, verliert alle Freigaben von und an ihn. (Komponente Grant, Regeln 2 und 3)

## Verträge

**Was hier hineingehört**
Alles Wiederkehrende: Verträge, Einnahmen, Sparziele und Schulden. Einmal angelegt, kommt es in jeden neuen Monat, in dem es fällig ist. (Komponente Commitment, Commitments_Lead)

**Abstand und Fälligkeit**
Du gibst an, alle wie viele Monate etwas fällig wird, von 1 bis 120, und die erste Zahlung. Gibt es den Tag im Monat nicht, gilt der letzte Tag des Monats. (Komponente Commitment, Regel 1)

**Ab wann ein Vertrag gilt**
Ein neuer oder geänderter Vertrag gilt für Monate, die danach entstehen. Bestehende Monate zeigen die Änderung als Hinweis und übernehmen sie erst nach deiner Zustimmung. (Komponente Plan, Regeln 1 und 2)

**Beenden statt löschen**
Ein Vertrag wird beendet, nie gelöscht. „Beenden“ setzt die letzte Zahlung auf die letzte bis heute. (Komponente Commitment, Regel 3)

## Konten

**Wofür Konten da sind**
Ein Konto ist ein Ort, an dem Geld liegt: Girokonto, Sparkonto oder Bargeld. Sein Stand ist der Startbetrag plus alle Buchungen danach. (Komponente Account)

**Verfügbar oder zurückgelegt**
Nicht jedes Konto ist Geld für diesen Monat. Ein Sparkonto mit einer Rücklage hat einen Stand, ist aber zurückgelegt; dafür gibt es den Schalter „zählt als verfügbar“. (Komponente Account: Counts as available)

**Standardkonto**
Ein Konto ist beim Buchen vorgewählt, das Standardkonto. Es gibt höchstens eines. (Komponente Account, Regel 3)

**Auflösen statt löschen**
Ein Konto wird aufgelöst, nie gelöscht. Seine Buchungen bleiben, und du kannst es wieder öffnen. (Komponente Account, Regel 4)

## Sparen

**Sparziele**
Ein Sparziel sammelt, was du für etwas Bestimmtes zurücklegst, mit oder ohne Zielbetrag. Ist es mit einem Vertrag verbunden, fließt die Rate dem Ziel zu, sobald du den Posten im Plan abhakst. (CommitmentDialog_GoalHint, Field_SavingsGoalTargetHint)

**Schulden und Kredite**
Hier steht, was du jemandem schuldest und was noch offen ist. Ist eine Schuld mit einem Vertrag verbunden, zahlt die Rate darauf ein, sobald du den Posten abhakst. (CommitmentDialog_DebtHint)

**Hilfe**
Schulden sind kein Makel. Eine Schuldnerberatung hilft kostenlos und unabhängig, zum Beispiel über die Verbraucherzentrale, die Caritas oder die Diakonie. (Savings_Help)

## Benutzerkonto

**Deine Richtwerte**
Welcher Anteil der Einnahmen in Grundbedarf, Wünsche und Sparen gehen soll, in ganzen Prozent und zusammen 100. Neue Monate übernehmen sie, ältere behalten ihre. (Account_QuotasHint)

**Abmelden überall**
Meldest du dich überall ab, sind andere Geräte spätestens nach 15 Minuten abgemeldet. (Account_SignOutEverywhereHint)

## Verwaltung

**Einladungen auf den Server**
Ob sich jemand ohne Einladung registrieren darf, legt der Betreiber fest. Ein Einladungslink gilt sieben Tage und einmal; mit E-Mail-Adresse nur für diese Person. (Komponente Instance)

**Was eine Verwaltung nicht sieht**
Pläne, Buchungen und Konten anderer bleiben dir verborgen. Einblick gibt es nur über die Freigaben im Haushalt. (Admin_Lead)

## Aus dem alten Duofy bewusst weggelassen

- Limit-Posten („Verpflichtung und Limit“, Limit im Verlauf): gibt es bei uns nicht (Spec 82, R10).
- Import, Parkposition, CAMT-Datei, IBAN-Abgleich, „Die andere Seite“: der Import ist entfallen. Die IBAN am Konto gibt es, sie hat aber noch keine Aufgabe, deshalb kein Text dazu.
- Private Posten: folgen erst in V2; bei uns sehen alle Mitglieder alle Posten im Haushaltsplan.
- „Eine Abbuchung, ein Vertrag“ und „Warum kein Depot“: Ratschläge ohne Regel dahinter; können zurück, wenn Tom sie will.
- Hinweis „Gemeinsames Buch: wer fehlt“: bei uns gibt es kein gemeinsames Buch, nur die Konten im Haushaltsplan mit dem Satz, wer fehlt.
- Admins des Haushalts: bei uns verwaltet allein, wer gegründet hat.
- Umschalter „unter dem Logo“: die Personenauswahl sitzt jetzt im Profil-Menü (R11); der Abschnitt „Du siehst gerade eine andere Person“ erscheint deshalb nicht.
