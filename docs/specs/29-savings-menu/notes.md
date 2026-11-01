# Spec-Vermerk #29: Menüpunkt Sparen mit Gerüst und Rechten

- Neues fünftes Ziel "Sparen" (`/sparen`) in unterer Leiste und Seitenleiste, zwischen Buchungen und Mehr.
- Die Seite zeigt drei Abschnitte (Sparziele, Schulden, Kredite), je mit einem Leerzustand in ruhigem Ton.
- Rechte: neuer Freigabebereich `savings` (Stufen wie bei den anderen Bereichen). Vorlagen: Partner = Löschen, Mitlesen = Ansehen, Kind = Nichts, Nichts = Nichts. Ohne Ansehen sagt die Seite "teilt diesen Bereich nicht".
- Daten: nur die Bereichsprüfung der Tabelle `grants` wird erweitert (additive Migration `AddSavingsGrantArea`, keine Datenänderung).
- Nicht im Umfang: Inhalte der Abschnitte (kommen mit #30 und #31).
