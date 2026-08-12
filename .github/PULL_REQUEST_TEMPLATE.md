## What changes

<!-- Two or three sentences: what does this pull request do? -->

## Why this way

<!-- Only if there is a decision behind it that the code does not show. Otherwise
     leave it out. -->

Closes #

---

### About the title

On merge, the **title of this pull request** becomes the commit message, and the
version number and changelog are derived from it. So it has to follow the pattern:

| Title | Next version |
|---|---|
| `fix: correct the category label` | patch — 1.2.0 → 1.2.1 |
| `feat: add income as its own type` | minor — 1.2.0 → 1.3.0 |
| `feat!: remove plan states` | major — 1.2.0 → 2.0.0 |
| `docs:` · `test:` · `refactor:` · `chore:` | no new version |

For `feat!`, a line `BREAKING CHANGE: …` belongs in the body — it ends up in the
changelog and tells self-hosters what they have to do when updating.

The commits **inside** your branch may be called anything.

### Before submitting

- [ ] My commits are signed with `git commit -s`
- [ ] Backend: `ruff check app/ tests/` and `pytest` pass
- [ ] Frontend: `tsc --noEmit -p tsconfig.app.json`, `npm run lint` and
      `npm run build` pass
- [ ] New logic comes with a test
- [ ] **No real data in the diff** — no names, amounts, account numbers,
      credentials, no screenshots with real figures
