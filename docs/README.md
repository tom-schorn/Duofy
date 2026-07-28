# Duofy — Dokumentation

```
docs/
├── datenmodell/   was gebaut ist  — spiegelt backend/app/models/
├── gekritzel/     wie es entstand — Konzept, Ideen, offene Fragen
└── referenz/      private Unterlagen (von Git ausgeschlossen)
```

## Wo fange ich an?

**Du willst wissen, wie Duofy funktioniert**
→ [gekritzel/01-vision.md](gekritzel/01-vision.md)

**Du willst am Code arbeiten**
→ [datenmodell/README.md](datenmodell/README.md) — ER-Diagramm und die drei
Regeln, aus denen alles folgt

**Du suchst ein bestimmtes Modell**
→ [datenmodell/](datenmodell/) — eine Seite pro Aggregat

**Du willst wissen, was noch offen ist**
→ [gekritzel/05-offene-fragen.md](gekritzel/05-offene-fragen.md) und die
„Offen"-Abschnitte am Ende jeder Modellseite

## Warum zwei Ordner

`gekritzel/` ist die **Entstehungsgeschichte** — inklusive Ideen, die es nicht
in den MVP geschafft haben, und Entscheidungen, die später revidiert wurden.
Das bleibt so stehen: Es erklärt das *Warum*, das man aus dem Code nicht
herauslesen kann.

`datenmodell/` beschreibt den **Ist-Stand** und wird mit dem Code aktuell
gehalten.

Beides zu vermischen hieße, das Konzept so umzuschreiben, als wäre es immer
richtig gewesen.

## Konventionen

- Dokumentation auf **Deutsch** (Jasmin liest mit)
- Code, Commits und Issues auf **Englisch**
- `referenz/` enthält echte Finanzdaten und ist per `.gitignore` ausgeschlossen
