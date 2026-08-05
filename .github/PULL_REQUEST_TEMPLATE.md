## Was ändert sich

<!-- Zwei, drei Sätze: was tut dieser Pull Request? -->

## Warum so

<!-- Nur wenn eine Entscheidung dahintersteckt, die man dem Code nicht ansieht.
     Sonst weglassen. -->

Closes #

---

### Zum Titel

Beim Merge wird der **Titel dieses Pull Requests** zur Commit-Nachricht, und daraus
entstehen Versionsnummer und Changelog. Deshalb muss er dem Muster folgen:

| Titel | Nächste Version |
|---|---|
| `fix: correct the category label` | Patch — 1.2.0 → 1.2.1 |
| `feat: add income as its own type` | Minor — 1.2.0 → 1.3.0 |
| `feat!: remove plan states` | Major — 1.2.0 → 2.0.0 |
| `docs:` · `test:` · `refactor:` · `chore:` | keine neue Version |

Bei `feat!` gehört eine Zeile `BREAKING CHANGE: …` in den Rumpf — sie landet im
Changelog und sagt Selbst-Hostern, was sie beim Update tun müssen.

Die Commits **innerhalb** deines Branches dürfen heißen wie sie wollen.

### Vor dem Absenden

- [ ] Ich stimme der [Beitragsvereinbarung](../blob/main/CLA.md) zu
- [ ] Meine Commits sind mit `git commit -s` signiert
- [ ] Backend: `ruff check app/` und `pytest` laufen durch
- [ ] Frontend: `tsc --noEmit -p tsconfig.app.json`, `npm run lint` und
      `npm run build` laufen durch
- [ ] Neue Logik hat einen Test
- [ ] **Keine echten Daten im Diff** — keine Namen, Beträge, Kontonummern,
      Zugangsdaten, keine Screenshots mit echten Zahlen
