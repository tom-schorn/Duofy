## What changes

<!-- Two or three sentences: what does this pull request do? -->

## Why this way

<!-- Only if there is a decision behind it that the code does not show. Otherwise
     leave it out. -->

Closes #

---

### About the title

Pull requests are rebase-merged: **every commit** of your branch lands on `main`
as it is, and the version number and changelog are derived from those commit
subjects. So the title and each commit subject have to follow the pattern:

| Title | Next version |
|---|---|
| `fix: correct the category label` | patch — 0.2.0 → 0.2.1 |
| `feat: add income as its own type` | minor — 0.2.0 → 0.3.0 |
| `feat!: remove plan states` | minor — 0.2.0 → 0.3.0 |
| `docs:` · `test:` · `refactor:` · `chore:` | no new version |

Below 1.0 a breaking change bumps the minor version, not the major one — that is
what the leading zero means. The `!` still belongs on the title, and a line
`BREAKING CHANGE: …` in the body: it ends up in the changelog and tells
self-hosters what they have to do when updating.

Both are checked. Keep each commit to one thing, because they are not squashed.

### Before submitting

- [ ] My commits are signed with `git commit -s`
- [ ] Backend: `ruff check app/ tests/` and `pytest` pass
- [ ] Frontend: `tsc --noEmit -p tsconfig.app.json`, `npm run lint` and
      `npm run build` pass
- [ ] New logic comes with a test
- [ ] **No real data in the diff** — no names, amounts, account numbers,
      credentials, no screenshots with real figures
