# Offene Fragen

Was noch nicht entschieden ist. Beim Planungstermin durchgehen.

## Haushalt & Fairness

Konzept steht in [07-haushalt-fairness.md](07-haushalt-fairness.md).
Offen bleibt:

- [ ] **Wie hoch soll der Selbstbehalt sein?** (Beispiel rechnet mit 400 €)
- [ ] Selbstbehalt für alle gleich, oder pro Person unterschiedlich?
- [ ] Was sieht der Partner von privaten Positionen — Betrag, nur die Summe,
      oder gar nichts?
- [ ] Wird der Ausgleich als Position im Folgemonat angelegt, oder nur
      angezeigt?
- [ ] Verteilungs-Assistent: wie gut muss der Vorschlag sein? Reicht „greedy,
      grob passend", oder soll er wirklich optimieren?
- [ ] Ab welcher Abweichung lohnt ein Ausgleich überhaupt — Schwelle setzen?
      (Unter 20 € vielleicht einfach stehen lassen)
- [ ] Wohngeld: auf wessen Konto geht es ein?
- [ ] Was passiert, wenn der Ausgleich nicht gezahlt wird — läuft ein Saldo
      auf, wie bei einem Privatdarlehen?
- [ ] Was passiert mit dem Haushalt, wenn jemand austritt? Historie bleibt?
- [ ] Zählt eine Nebenkostenrückzahlung wirklich als Haushaltseinkommen, oder
      anteilig nach dem Schlüssel des Vorjahres?

## Planungsperiode

- [ ] Wie wird der Periodenstart festgelegt — fest „letzter Samstag" oder
      pro Haushalt einstellbar?
- [ ] Was passiert mit Positionen, die im Monat dazukommen? Plan ändern oder
      als Abweichung führen?
- [ ] Kann ein bestätigter Plan noch geändert werden, und braucht das die
      Zustimmung beider?

## Puffer-Faktor

- [ ] Prozent vom Einkommen oder fester Betrag?
- [ ] Was passiert mit dem übrigen Puffer am Monatsende — verfällt er, wandert
      er in einen Topf, oder in den Folgemonat?

## Szenarien

- [ ] Zwei feste Szenarien („mit/ohne Kindergeld") oder beliebig viele?
- [ ] Wird ein Szenario zum aktiven Plan gewählt, oder laufen beide parallel?

## Forderungen

- [ ] Woher kommen Mahnstufen — von Hand eingetragen oder aus Dokumenten?
- [ ] Braucht es Verzugszinsen, oder reichen Gebühren als Pauschale?
- [ ] Sollen Insolvenz-relevante Forderungen gesondert markiert werden?

## Privatdarlehen

- [ ] **Mit dem Insolvenzberater klären:** Forderungen gegen Dritte gehören
      zum Vermögen — was heißt das fürs laufende Verfahren?
- [ ] Braucht ein Privatdarlehen ein Fälligkeitsdatum, oder reicht „offen"?
- [ ] Soll die andere Person das auch sehen können (eigener Account), oder
      bleibt es eine private Notiz?
- [ ] Ratenzahlung an Freunde — als Vertrag abbilden oder eigener Mechanismus?

## Kategorien

- [ ] Reicht der Seed-Satz aus 14 Kategorien, oder fehlt was Wichtiges?
- [ ] Dürfen Haushalte eigene Kategorien anlegen? (Gegen: Auswertungen werden
      unvergleichbar. Für: jeder Haushalt ist anders)
- [ ] Unterkategorien nötig, oder reicht eine Ebene?
- [ ] Welche Kategorien sind wirklich `locked`? Miete als Wunsch ist Unsinn —
      aber wer entscheidet das bei Grenzfällen?
- [ ] Sprit: einigt ihr euch auf Bedarf oder Wunsch?

## Abgleich

- [ ] Welche CSV-Formate zuerst? (Volksbank, ING, comdirect — verschiedene
      Exporte)
- [ ] Wie werden Matching-Regeln angelegt — automatisch beim ersten manuellen
      Zuordnen, oder von Hand?
- [ ] Was passiert bei Teilzahlungen und Sammelbuchungen?

## Technisch

- [ ] Wie werden Beträge in der API übertragen — String, Cent-Integer oder
      Dezimalzahl?
- [ ] Mandantentrennung: reicht Filterung über den Haushalt, oder braucht es
      Row-Level-Security?
- [ ] Verschlüsselung sensibler Felder in der Datenbank — nötig oder
      Overkill für den MVP?

## Produkt

- [ ] Ist das nur für Tom & Jasmin, oder soll es andere Nutzer geben?
- [ ] Falls andere Nutzer: dann greifen DSGVO, AVV, Impressum,
      Datenschutzerklärung — und Finanzdaten sind besonders sensibel
- [ ] Was ist der kleinste Umfang, mit dem ihr die Excel-Tabelle **ablösen**
      könntet? (Das ist der MVP)
