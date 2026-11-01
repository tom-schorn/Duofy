# Spec-Vermerk #30: Sparziele mit Stand je Ziel

- Ein Sparziel gehört einer Person: Name, optional Zielbetrag, optional Zieldatum, optional das Sparkonto, auf dem das Geld liegt.
- Der Stand wird nie gespeichert. Er ist "Schon angespart" plus die Summe der Buchungszeilen, die dem Ziel zugeordnet sind (Einzahlung plus, Entnahme minus).
- "Schon angespart" (`start_amount`, Entscheidung von Tom am 01.11.2026): ein einmalig eingegebener Startwert, Standard 0, nicht negativ, zwei Nachkommastellen, beim Anlegen und Ändern. Er erzeugt keine Buchung und ändert weder Kontostände noch Plan, Topfsummen oder Quoten. Ein negativer Wert antwortet `400 savings_goal_start_invalid`.
- Zuordnung am Buchungszeilen-Feld `savings_goal_id`. Erlaubt sind nur Zeilen, die Sparen bewegen: eine Ausgabe im Topf Sparen oder eine Umbuchung mit Zieltopf Sparen (Einzahlung), eine Umbuchung mit Quelltopf Sparen und anderem Zieltopf (Entnahme). Alles andere antwortet `400 savings_goal_booking_invalid`.
- Mehrere Ziele können dasselbe Konto nennen; der Stand je Ziel ist unabhängig vom Kontostand.
- Erreicht heißt Stand >= Zielbetrag (nur mit Zielbetrag). Ein Vertrag, der auf ein erreichtes Ziel zeigt, wird im Plan nicht mehr verlangt: Er zählt wie nicht fällig und der Abgleich bietet an, den Posten zu entfernen.
- Rechte: Bereich `savings`; Ansehen, Anlegen, Ändern, Löschen wie bei Verträgen. Fremde Ziele ohne Ansehen: 404.
- Nicht im Umfang: Archivieren (Löschen nur ohne Zuordnungen).
- Rechte-Nachträge: Zuordnung, Änderung oder Lösen einer Zuordnung an einer Buchung braucht im Bereich Sparen Anlegen/Ändern; eine zugeordnete Buchung zu löschen braucht Löschen. Alte Sparziele und Schulden unter den Verträgen gehören zum Bereich Sparen.
