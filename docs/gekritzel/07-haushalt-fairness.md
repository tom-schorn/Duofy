# Haushalt & Fairness

> **Hinweis:** Dieses Dokument ist der **Konzeptstand** und weicht vom
> gebauten Code ab. Beim Umsetzen wurden Namen und Umfang geändert:
> `Period` → `Plan`, `Source` → `Commitment`, `Membership` → `HouseholdMember`,
> und Konten, Aufteilungsschlüssel und Allocations sind **nicht Teil des MVP**.
>
> Der aktuelle Stand steht in [../datenmodell/](../datenmodell/).

Wie eigene Accounts und ein gemeinsamer Haushalt zusammengehen — und wie die
Aufteilung nachweisbar fair wird.

Das ist das Alleinstellungsmerkmal von Duofy. Fast jede App kann 50/30/20.
Fast keine kann *„gemeinsam planen, getrennt besitzen"*.

---

## 1 · Struktur

```
User  ──────────►  Membership  ◄──────────  Household
 │                                              │
 └── Account                                    └── Period → Position
     gehört IMMER genau einer Person                        │
                                            gemeinsam ──────┴────── privat
```

**Grundregel: Der Haushalt besitzt nichts.**

Er ist eine reine Planungsebene. Konten gehören immer genau einer Person. Ein
„gemeinsames Konto" ist in Duofy keine Kontoart, sondern eine Aufteilungsregel
auf Positionen.

Das ist kein Notbehelf für die Insolvenz-Situation, sondern das korrekte
Modell — auch Paare mit echtem Gemeinschaftskonto fahren damit sauberer.

### Sichtbarkeit

| `sichtbarkeit` | Wer sieht es | Wer zahlt es |
|---|---|---|
| `gemeinsam` | alle Mitglieder | aufgeteilt nach Schlüssel |
| `privat` | nur der Eigentümer | der Eigentümer allein |

Private Positionen zählen **nur im persönlichen Budget**, nie im gemeinsamen
50/30/20.

---

## 2 · Was zählt als Einkommen

Der Punkt, an dem die meisten Rechnungen kippen.

**Transferleistungen sind kein persönliches Einkommen.** Wohngeld, Pflegegeld
und Kindergeld gehören dem Haushalt bzw. den Kindern — nicht der Person, auf
deren Konto sie eingehen. Rechnet man sie einer Person zu, erscheint sie
reicher als sie ist und trägt einen zu großen Anteil.

Deshalb bekommt jede Einnahme-Position **drei** Angaben:

```
gehoert_zu             person | haushalt   ← wem gehört es wirtschaftlich
eingang_konto          Account             ← wo kommt es real an
zaehlt_zum_schluessel  bool
```

> **`gehoert_zu` und `eingang_konto` sind nicht dasselbe.** Haushaltseinkommen
> landet trotzdem immer auf dem Konto *einer* Person. Wer daraus Rechnungen
> zahlt, reicht Haushaltsgeld durch — das ist **kein eigener Beitrag**.
>
> Genau hier kippt jede naive Rechnung.

Beispiel Tom & Jasmin:

| Einnahme | gehört wirtschaftlich | landet auf | zählt zum Schlüssel |
|---|---|---|---|
| Lohn Tom | Tom | Tom | ✓ |
| Gehalt Jasmin | Jasmin | Jasmin | ✓ |
| Pflegegeld | Haushalt | **Tom** | ✗ |
| Kindergeld | Haushalt | **Jasmin** | ✗ |
| Kinderzuschlag (KiZ) | Haushalt | **Jasmin** | ✗ |
| Wohngeld | Haushalt | — | ✗ |
| Nebenkostenrückzahlung | Haushalt | — | ✗ |

**Rechenweg:**

```
gemeinsame Kosten
  − Haushaltseinkommen        (deckt vorweg ab)
  = aufzuteilender Rest
  × Schlüssel je Person
  = Soll-Anteil
```

---

## 3 · Der Aufteilungsschlüssel

### Modell A — Gleichteilung

Jeder zahlt die Hälfte. Einfach, aber bei ungleichen Einkommen unfair.

### Modell B — Nach Einkommen

Anteil = eigenes Einkommen ÷ Summe aller Einkommen (nur die mit
`zaehlt_zum_schluessel = true`).

### Modell C — Nach Einkommen mit Selbstbehalt  ← **empfohlen**

Vor der Verhältnisrechnung behält jede Person einen festen Sockelbetrag für
sich. Nur was darüber liegt, geht in den Schlüssel.

```
anteil_person = (einkommen − selbstbehalt) ÷ Σ (einkommen − selbstbehalt)
```

**Warum fairer:** Bei reiner Proportionalität bleibt der Person mit weniger
Einkommen absolut weniger zum Leben. Der Selbstbehalt schützt genau das —
dasselbe Prinzip, das Familiengerichte beim Unterhalt anwenden.

Der Selbstbehalt wird pro Haushalt festgelegt und gilt für alle gleich.

### Modell D — Feste Beträge

Jeder zahlt einen vereinbarten Betrag. Für Sonderfälle, nicht als Standard.

### Rechenbeispiel

Gemeinsame Kosten **2.080 €**, Haushaltseinkommen **1.132 €**
→ aufzuteilender Rest **948 €**
Tom 1.735 € · Jasmin 900 € · Selbstbehalt 400 €

| Modell | Schlüssel | Tom | Jasmin |
|---|---|---|---|
| A — 50/50 | 50 : 50 | 474 € | 474 € |
| B — nach Einkommen | 65,8 : 34,2 | 624 € | 324 € |
| C — mit Selbstbehalt | 72,8 : 27,2 | 690 € | 258 € |

---

## 4 · Zwei Wege, den Anteil zu tragen

Es gibt kein gemeinsames Konto. Also muss der Anteil anders zur Geltung kommen
— auf einem von zwei Wegen. **Duofy muss beide können.**

### Weg A — Posten verteilen  ← *das machen Tom & Jasmin heute*

Kein Geld fließt zwischen den Personen. Stattdessen wird jeder gemeinsame
Posten einer Person als **Zahler** zugewiesen, so dass die Summen ungefähr den
fairen Anteilen entsprechen.

**Zahllast einer Person:**

```
Zahllast = fairer Anteil an den gemeinsamen Kosten
         + Haushaltseinkommen, das auf ihrem Konto eingeht
```

Bei Tom: sein Anteil **plus** Pflegegeld.
Bei Jasmin: ihr Anteil **plus** Kindergeld und KiZ.

Geht die Rechnung auf, muss **kein Euro** zwischen den Konten fließen. Bei
laufender Insolvenz ist das der ruhigere Weg.

**Verteilungs-Assistent:** Duofy kennt alle gemeinsamen Posten und beide
Zahllasten und schlägt eine Zuweisung vor, die möglichst genau aufgeht.
Perfekt wird es nie — Miete lässt sich nicht teilen. Der Rest wird über Weg B
ausgeglichen, aber es sind dann 40 € statt 260 €.

**Schwäche:** Ändert sich ein Einkommen, muss neu verteilt werden. Das ist der
Grund, warum die heutige Aufteilung „möglichst gleich, damit jeder über die
Runden kommt" mit der Zeit schief läuft.

### Weg B — Ausgleichen

Eine Person zahlt, die andere überweist ihren Anteil.

```
                Soll        Ist (tatsächlich gezahlt)
Tom             690 €       1.180 €
Jasmin          258 €           0 €
──────────────────────────────────────────
Ausgleich:      Jasmin → Tom   258 €
```

Eine Überweisung am Monatsende. Genauer als Weg A, aber es fließt Geld
zwischen den Konten.

### In der Praxis

**Weg A als Standard, Weg B für den Rest.** Duofy verteilt die Posten so gut
es geht und weist am Monatsende die verbleibende Differenz als
Ausgleichsbetrag aus.

**Nebeneffekt beider Wege:** Es entsteht ein lückenloser Nachweis, wer welchen
Anteil getragen hat — und wieviel davon nur durchgereichtes Haushaltsgeld war.

---

## 5 · Kapitalzuordnung über die Zeit

Aus den Allocations fällt ohne Zusatzaufwand heraus:

- Wer hat über die Monate wieviel in den Haushalt eingebracht
- Wieviel Kapital gehört wem (Konten + Sparttöpfe, jeweils personenbezogen)
- Wurde der Ausgleich gezahlt oder läuft ein Saldo auf

Das ist das, was mit zwei Excel-Blättern nie funktioniert hat.

---

## 6 · Umsetzung

**Datenmodell**

```
Membership
  haushalt        Household
  user            User
  rolle           besitzer | mitglied
  selbstbehalt    DECIMAL, nullable — überschreibt Haushalts-Default

Household
  schluessel_typ  gleich | einkommen | einkommen_selbstbehalt | fest
  selbstbehalt    DECIMAL — Default für alle Mitglieder
  ausgleich_modus verteilen | ausgleichen | gemischt

Position (Zusatz für gemeinsame Positionen)
  zahler          User — wer zahlt es real, aus welchem Konto

Allocation
  position        Position
  user            User
  anteil          DECIMAL — der berechnete Soll-Betrag
```

**Wichtige Regeln**

1. Der Schlüssel wird **beim Bestätigen der Periode eingefroren** und in die
   Allocations geschrieben. Ändert sich später ein Einkommen, bleiben
   abgeschlossene Monate unverändert.
2. Private Positionen erzeugen **keine** Allocations.
3. `Allocation.anteil` ist immer ein Betrag, nie ein Prozentsatz — sonst
   entstehen Rundungsdifferenzen.
4. Rundungsrest (z. B. 0,01 € bei Drittelung) geht an eine feste Person, damit
   die Summe der Allocations exakt dem Positionsbetrag entspricht.

---

## Offen

Siehe [05-offene-fragen.md](05-offene-fragen.md) — insbesondere Höhe des
Selbstbehalts und Sichtbarkeit privater Positionen.
