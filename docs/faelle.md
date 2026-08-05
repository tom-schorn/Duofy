# Fälle

Wie Duofy mit echten Situationen umgeht. Gesammelt beim Benutzen, nicht am
Reißbrett — jeder Fall hier ist einmal aufgetreten und hat eine Entscheidung
erzwungen.

Gedacht als Vorlage für eine Hilfeseite. Aufbau je Fall: **Situation**, **so
trägt man es ein**, **warum so**.

---

## 1. Die Einnahme kommt im Vormonat an

**Situation.** ALG1 wird am 30. Juli überwiesen, Wohngeld am 31. Beide sind für
den August gedacht. Pflegegeld kam am 28. Juli.

**So trägt man es ein.** Der Posten steht im **August**. Die Buchung behält ihr
echtes Datum, den 30. Juli, und wird dem August-Posten zugeordnet.

**Warum so.** Duofy ordnet eine Buchung dem Monat ihres **Postens** zu, nicht
dem ihres Datums. Hat sie keinen Posten, gilt das Datum. Die meisten
Haushaltsbücher machen es umgekehrt und damit ist Wohngeld für immer ein
Juli-Vorgang, obwohl es den August bezahlt.

Die Regel schließt aus, statt zu ergänzen: eine zugeordnete Buchung steht in
**einem** Monat, nicht in zweien. Sonst zählte sie doppelt.

> Offen: Buchungen ohne Posten lassen sich keinem Monat zuweisen. Die
> BuT-Umbuchung aufs Tagesgeld bleibt deshalb im Juli, während der Eingang im
> August steht. Ein `plan_id` an der Buchung würde das lösen — Issue #2.

---

## 2. Geld, das nur durchläuft

**Situation.** BuT für Lios Schulbedarf, 130 €. Kommt an und wandert sofort aufs
Tagesgeld. Genauso die Nebenkostenrückzahlung über 1.138,93 €.

**So trägt man es ein.** Zwei Posten — Einnahme und Sparen — und bei **beiden**
das Häkchen **durchlaufend**.

**Warum so.** Ohne das Häkchen zählt Duofy die Einnahme ins Budget und das
Wegsparen in die Sparquote. Bei 1.139 € durchgereicht sieht das aus wie 1.139 €
gespart, und das Budget wächst um Geld, das niemandem zum Ausgeben gehört.

Der Unterschied zu echtem Sparen ist die **Entscheidung**: bei „Sparen
Allgemein" legt man eigenes Geld zurück, hier reicht man fremdes weiter.

Was es bewirkt: der Posten bleibt sichtbar, ist abhakbar und bewegt das Konto —
er zählt nur in kein Budget, in keine Quote und nicht als offene Zahlung.

Beispiel aus der Praxis: 1.269 € Durchlaufgeld verschoben den Bedarf von
scheinbar 33 % auf tatsächlich 50,7 %.

---

## 3. Sparen auf ein anderes eigenes Konto

**Situation.** 50 € im Monat fürs neue Handy, vom Girokonto aufs Tagesgeld.

**So trägt man es ein.** Vertrag vom Typ **Sparziel**, Betrag 50, Zielbetrag
300, Konto **Girokonto**, Zielkonto **Tagesgeld**.

**Warum so.** Die beiden Felder sagen „woher" und „wohin". Ist ein Zielkonto
gesetzt, bucht der Haken eine **Umbuchung** statt einer Ausgabe — das Girokonto
sinkt, das Tagesgeld steigt, der Gesamtstand bleibt gleich. Und das ist richtig:
die 50 € haben den Haushalt nicht verlassen.

Im Buch zählen sie trotzdem als ausgegeben, sobald das Tagesgeld auf „zählt
nicht als verfügbar" steht — verplanen kann man sie nicht mehr. Siehe Fall 8.

Bleibt das Zielkonto leer, geht das Geld raus. So bei Miete, Strom, Einkauf.

---

## 4. Bargeld und Umschlagmethode

**Situation.** Jasmin hebt das Geld für Lebensmittel und Sprit ab und teilt es
in einer Mappe auf. Sie erfasst keine Einzelausgaben.

**So trägt man es ein.** Die Abhebung als Ausgabe **ohne Posten**. Die
betroffenen Budgetposten werden **abgehakt**.

**Warum so.** Der Haken erzeugt nur dann eine Buchung, wenn dem Posten noch gar
nichts zugeordnet ist. Liegen schon Kartenzahlungen darauf, hakt er nur ab.
Deshalb entsteht kein Doppelzähler, obwohl die Abhebung und einzelne Einkäufe
beide im Buch stehen.

Kein Bargeld-Konto: die Mappe ist die Buchhaltung, Duofy baut sie nicht nach.
Das ist gewollt — planen statt tracken.

---

## 5. Die Lastschrift platzt

**Situation.** Die KFZ-Versicherung zieht 47,78 € ein und holt sie am selben Tag
mit dem Vermerk „fehlende Deckung" zurück.

**So trägt man es ein.** **Beide** Buchungen erfassen, Abgang und Rückläufer,
und **keine** davon einem Posten zuordnen. Der Posten bleibt offen.

**Warum so.** Der Kontostand stimmt nur, wenn beide Bewegungen drinstehen. Am
Posten wäre es falsch: `amount_actual` summiert Beträge ohne Vorzeichen, aus
−47,78 und +47,78 würden 95,56. Und offen ist der Posten wirklich — bezahlt ist
nichts.

---

## 6. Einer zahlt für den anderen

**Situation.** Tom überweist Jasmin 20 € und 70 €, weil sie etwas ausgelegt hat.

**So trägt man es ein.** Bei Tom als Ausgabe der Kategorie **Ausgleich**, bei
Jasmin als Einnahme derselben Kategorie.

**Warum so.** Wirtschaftlich ist es keine neue Ausgabe — der Einkauf wurde schon
beim anderen gebucht. Bei Auswertungen über den Haushalt muss die Kategorie
ausgeklammert werden, sonst zählt derselbe Einkauf zweimal. Siehe Issue #4.

Transferleistungen sind der verwandte Fall: Pflegegeld landet auf Toms Konto,
Kindergeld auf Jasmins, gehören aber beide dem Haushalt. Wer daraus zahlt,
reicht Geld durch und leistet keinen eigenen Beitrag.

---

## 7. Vier Zahlen, die alle „was ist übrig" heißen

Sie widersprechen sich nicht, sie beantworten verschiedene Fragen.

| Zahl | Wo | Rechnung | Frage |
|---|---|---|---|
| **Verplanbar** | Plan | Budget − verteilte Posten | Was kann ich noch einplanen? |
| **Noch offen** | Plan | Rest der unbezahlten Posten | Was geht diesen Monat noch weg? |
| **Verfügbar** | Buch | Stand der freien Konten | Was liegt gerade da? |
| **Frei nach Abzug** | Buch | Verfügbar − Noch offen | Kann ich mir das jetzt leisten? |

**Verplanbar** rechnet mit dem Soll und bewegt sich während des Monats nicht.
Eine Buchung ohne Posten ändert es nicht — mit dieser Zahl plant man am
Monatsanfang.

**Verfügbar** behauptet nichts, es schaut nach. Eine Monatsrechnung könnte den
Kontostand nie treffen: die August-Einnahmen kommen Ende Juli, und aus demselben
Geld wurden noch Juli-Ausgaben bezahlt.

Welche Konten als verfügbar gelten, steht am Konto. Beim Girokonto ja, beim
Tagesgeld nein — dort liegt Zweckgebundenes, das man nicht zweimal ausgeben
kann.

**Achtung:** im Plan steht kein Posten für Lebensmittel, wenn man keinen
angelegt hat. „Frei nach Abzug" kennt dann nur die Fixkosten und sieht
großzügiger aus, als der Monat ist. Dafür ist der Vertragstyp **Budget** da.

---

## 8. Umbuchung zwischen eigenen Konten

**Situation.** 210 € aufs Tagesgeld legen. Oder PayPal aufladen.

**So trägt man es ein.** Beides als **Umbuchung** mit Zielkonto. Beim Sparen
zusätzlich den **Posten** setzen.

**Warum so.** Zwei Felder, zwei unabhängige Fragen:

* **Zielkonto** entscheidet über die **Stände** — von hier nach dort.
* **Posten** entscheidet über das **Budget** — füllt diese Umbuchung eine Quote?

Daraus fallen alle vier Fälle heraus, ohne Sonderregeln. Geld aufs Tagesgeld
legen erfüllt die Sparquote, PayPal aufladen nicht. Würde man Umbuchungen
pauschal aus dem Budget nehmen, erfüllte Sparen nie sein Soll.

Im Gesamtstand bewegt eine Umbuchung nichts — außer das Zielkonto zählt nicht
als verfügbar. Dann gilt sie als Ausgabe, weil man das Geld nicht mehr frei hat.

---

## 9. Kategorien aus dem Bank-Export

**Situation.** Die CSV der Bank bringt Spalten `Kategorie` und
`ParentKategorie` mit.

**So trägt man es ein.** Gar nicht. Kategorie beim Import leer lassen und selbst
setzen.

**Warum so.** Die Werte stammen aus der Automatik des Bankprogramms und sind
falsch, sobald es interessant wird: ARAL wurde „Lebensmittel", Media Markt
„GEZ-Gebühren", ein Zahlungsdienstleister „Beruf". Verwertbar sind Datum,
Betrag, Empfänger, Verwendungszweck, IBAN — und die Spalte `Umbuchung` samt
Ziel-IBAN, daran erkennt man eine Umbuchung zwischen eigenen Konten.

---

## 10. Was der Partner sehen darf

**Situation.** Zwei Personen, ein Haushalt. Wie viel sieht der andere?

**So trägt man es ein.** Auf der Haushaltsseite steht in der **eigenen** Zeile
„Du teilst" mit drei Stufen:

| Stufe | Was der andere sieht |
|---|---|
| Nur gemeinsame Posten | was gemeinsam geplant ist, sonst nichts |
| Buch und Konten sichtbar | dazu Buchungen, Kontostände und private Posten |
| Darf auch ändern | dazu Posten ändern und abhaken |

**Warum so.** Die Stufe hängt an der eigenen Mitgliedschaft, nicht an der des
anderen: wessen Daten es sind, der entscheidet. Wer sich selbst Einblick geben
könnte, hätte keine Beschränkung, sondern eine Zeile in der Oberfläche.

Vorgabe ist die unterste, auch nach einem Update. Niemand soll durch eine
Migration Einblick bekommen, den er nie gegeben hat.

Private Posten sind ab Stufe zwei mit dabei. Eine Stufe, die das Buch zeigt,
aber einen Posten verbirgt, wäre keine Vertrauensstufe, sondern eine Lücke — im
Buch stünde die Buchung ohnehin.

---

## 11. Gemeinsamer Plan und Personenansicht

**Situation.** Man will sehen, wie der Haushalt dasteht.

**So trägt man es ein.** Zwei getrennte Orte unter „Haushalt": der
**gemeinsame Plan** fasst alle Posten mit Haushaltszuordnung zusammen, mit dem
Vornamen dahinter. Die **Personenansicht** zeigt eine Person für sich.

**Warum so.** Ein zusammengeworfenes Buch wäre unlesbar. Zwei Kontolisten
summiert ergäben eine Zahl aus dem Depot des einen und dem Girokonto der
anderen, aus der niemand etwas ableiten kann.

Der gemeinsame Plan beantwortet „tragen wir den Monat", die Personenansicht
„wie steht der andere da". Geändert wird im eigenen Plan.

Der gemeinsame Plan ist **zusammengesetzt, nicht gespeichert**. Deshalb gibt es
dort kein „Monat anlegen" und kein „Monat bestätigen" — es existiert kein
Objekt, das man anlegen könnte.

---

## Offene Fälle

Noch nicht entschieden oder nicht gebaut:

* **Buchung einem Plan zuordnen**, ohne Posten — Fall 1, Issue #2
* **Bestätigung durch beide** im gemeinsamen Monat — es fehlt die Tabelle
* **Plan nachziehen**, wenn nach dem Anlegen Verträge dazukommen
* **Einnahmen als eigener Typ** — ein Gehalt ist heute ein „Vertrag"
* **Kategorie-Beschreibungen** im Formular, damit Grenzfälle wie der
  Rundfunkbeitrag klar sind (gehört zu `housing`, weil pro Wohnung geschuldet)
