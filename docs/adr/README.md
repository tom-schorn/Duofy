# Architekturentscheidungen (ADR)

Hier stehen Entscheidungen, die länger gelten als ein einzelnes Issue. Jede
Entscheidung ist eine kurze Datei, versioniert mit dem Code, damit Menschen und
KI-Sitzungen sie direkt im Repo finden.

## Wann braucht es eine ADR?

Eine ADR lohnt sich, wenn die Entscheidung mindestens eines davon erfüllt:

- **Schwer umkehrbar:** Sie später zu ändern kostet Migrationen oder Umbau.
- **Ohne Kontext überraschend:** Wer den Code liest, würde es anders erwarten.
- **Echte Abwägung:** Es gab ernsthafte Alternativen mit eigenen Vorteilen.

Kleine, leicht änderbare Entscheidungen gehören ins Issue, nicht hierher.

## Format

Dateiname `NNNN-kurztitel.md`, fortlaufend nummeriert. Aufbau:

```markdown
# NNNN Titel

- Datum: TT.MM.JJJJ
- Status: vorgeschlagen | angenommen | ersetzt durch NNNN

## Kontext
## Entscheidung
## Verworfene Alternativen
## Folgen
```

Eine angenommene ADR wird nicht umgeschrieben. Ändert sich die Entscheidung,
entsteht eine neue ADR, und die alte bekommt den Status „ersetzt durch NNNN“.

## Verzeichnis

| Nr. | Titel | Status |
|---|---|---|
| [0001](0001-kontostand-nicht-gespeichert.md) | Kontostand wird nicht gespeichert | angenommen |
| [0002](0002-snapshot-vertrag-zu-posten.md) | Snapshot-Muster Vertrag → Posten | angenommen |
| [0003](0003-fehler-als-code.md) | Fehler als Code, nie als Satz | angenommen |
| [0004](0004-betraege-als-decimal.md) | Beträge als Decimal | angenommen |
| [0005](0005-abstand-in-monaten.md) | Abstand in Monaten statt Rhythmus | angenommen |
