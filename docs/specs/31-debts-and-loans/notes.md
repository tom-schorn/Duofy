# Spec-Vermerk #31: Schulden und Kredite

- Ein Eintrag ist eine Forderung (offene Rechnung, Mahnung, Inkasso) oder ein Kredit. Felder: Art, Name, Gläubiger (wem), Ursprungsbetrag, Fälligkeit optional, Notiz optional.
- Offen = Ursprungsbetrag minus gezahlt. Gezahlt wird aus zugeordneten Buchungszeilen berechnet, nie gespeichert.
- Zahlungen sind Ausgaben (Zuordnung am Feld `debt_id` der Buchungszeile). Im Plan zählt ein Posten, der aus einem Vertrag der Art Schuld stammt, wie bisher; die Zuordnung am Eintrag macht ihn sichtbar.
- Ton: ruhig, keine Warnfarben, kein Alarm; ein fester Hinweis auf echte Hilfe (Schuldnerberatung) ohne Rechtsaussage.
- Rechte: Bereich `savings`.

## Stand der Umsetzung

- Tabelle `debts` (Art `claim`/`loan`, Name, Gläubiger, Ursprungsbetrag, Fälligkeit, Notiz), additive Migration `AddDebts`.
- Zahlung = Ausgabe, deren Buchungszeile `debt_id` trägt (eingebaute Kategorie "Schulden"); eine Zeile gehört nie zugleich einem Sparziel. Offen = Ursprungsbetrag minus Summe, nie unter 0; bezahlt, wenn gezahlt >= Ursprungsbetrag.
- Plan: Ein Vertrag (Art Ausgabe, ohne Zielkonto, ohne Sparziel) kann an einer Schuld hängen (`commitments.debt_id`); beim Abhaken trägt die Ausgabe die Schuld, ist alles bezahlt, wird die Rate im Plan nicht mehr verlangt. Die Auswahl im Vertragsdialog gibt es noch nicht (nur über die API).
- Rechte wie bei Sparzielen im Bereich `savings`; Abhaken und Lösen des Hakens einer zugeordneten Rate braucht dort Anlegen bzw. Löschen.
