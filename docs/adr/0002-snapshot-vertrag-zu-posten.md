# 0002 Snapshot-Muster Vertrag → Posten

- Datum: 26.09.2026
- Status: angenommen

## Kontext
Ein Vertrag (`Commitment`) beschreibt eine wiederkehrende Zahlung. Der Monatsplan
besteht aus Posten (`PlanPosition`). Ändert sich ein Vertrag, sollen bereits
geplante oder abgeschlossene Monate nicht nachträglich anders aussehen.

## Entscheidung
Beim Anlegen des Monats wird der Vertrag in einen Posten kopiert. Spätere
Änderungen am Vertrag ändern alte Monate nicht.

## Verworfene Alternativen
- **Posten verweist nur auf den Vertrag:** immer aktuell, verändert aber die
  Vergangenheit, sobald der Vertrag angepasst wird.

## Folgen
- Alte Monate bleiben stabil und nachvollziehbar.
- Eine Vertragsänderung wirkt erst für Monate, die danach angelegt werden.
- Der Posten trägt eigene Werte und ist nicht von späteren Änderungen abhängig.
