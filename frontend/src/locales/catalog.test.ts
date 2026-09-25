/**
 * The catalogs and the code agree.
 *
 * * every key the code asks for exists in `de.json`, and `de.json` holds no key
 *   nothing asks for
 * * the same for the help catalog, `de.help.json`
 * * no component carries German text of its own any more (umlaut search)
 * * a language with a single translated key shows that key translated and
 *   everything else in German
 * * every further language is compared with German; what it lacks is reported,
 *   not failed — it falls back to German at runtime
 *
 * The code is read as source, not executed: `t('plan.title')` is found where it
 * is written. A key built at runtime (`enums.budget.${budget}`) marks its prefix
 * as used, and a string literal that names an existing key counts as a use, since
 * keys also travel through tables (`label: 'nav.plan'`).
 */

import i18next from 'i18next'
import ts from 'typescript'
import { describe, expect, test } from 'vitest'

import { CATEGORIES } from '@/lib/domain'
import { helpFor, type HelpKey } from '@/lib/help'
import { OPTIONS, RESOURCES } from '@/lib/i18n'
import de from '@/locales/de.json'
import deHelp from '@/locales/de.help.json'
import testLanguage from '@/locales/fixtures/xx.json'

type Catalog = { [key: string]: string | Catalog }

/** `{ a: { b: 'x' } }` → `{ 'a.b': 'x' }` */
function flatten(catalog: Catalog, prefix = ''): Record<string, string> {
  const out: Record<string, string> = {}
  for (const [key, value] of Object.entries(catalog)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (typeof value === 'string') out[path] = value
    else Object.assign(out, flatten(value, path))
  }
  return out
}

const UI_KEYS = new Set(Object.keys(flatten(de)))
const HELP_KEYS = new Set(Object.keys(flatten(deHelp)))

/** Every source file except the tests, as text. */
const SOURCES = Object.entries(
  import.meta.glob<string>(['/src/**/*.{ts,tsx}', '!/src/**/*.test.ts'], {
    query: '?raw',
    import: 'default',
    eager: true,
  })
)

function parse(path: string, text: string): ts.SourceFile {
  return ts.createSourceFile(
    path,
    text,
    ts.ScriptTarget.Latest,
    true,
    path.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS
  )
}

/** `t(…)`, `i18n.t(…)` */
function isTranslateCall(node: ts.Node): node is ts.CallExpression {
  if (!ts.isCallExpression(node)) return false
  const callee = node.expression
  if (ts.isIdentifier(callee)) return callee.text === 't'
  return ts.isPropertyAccessExpression(callee) && callee.name.text === 't'
}

type Usage = {
  /** Keys written out in `t('…')` or `i18nKey="…"`. */
  direct: Map<string, string>
  /** Prefixes of keys built at runtime: `enums.budget.` */
  prefixes: Set<string>
  /** String literals anywhere — a key can travel through a table. */
  literals: Set<string>
}

function collectUsage(): Usage {
  const usage: Usage = { direct: new Map(), prefixes: new Set(), literals: new Set() }

  for (const [path, text] of SOURCES) {
    // The help column has its own namespace and its own test below.
    if (path.endsWith('/lib/help.tsx')) continue
    const where = (node: ts.Node, sf: ts.SourceFile) =>
      `${path}:${sf.getLineAndCharacterOfPosition(node.getStart()).line + 1}`

    const sf = parse(path, text)
    const visit = (node: ts.Node) => {
      if (isTranslateCall(node) && node.arguments.length > 0) {
        const first = node.arguments[0]
        if (ts.isStringLiteralLike(first)) usage.direct.set(first.text, where(first, sf))
        else if (ts.isTemplateExpression(first)) usage.prefixes.add(first.head.text)
      }
      if (
        ts.isJsxAttribute(node) &&
        node.name.getText() === 'i18nKey' &&
        node.initializer &&
        ts.isStringLiteral(node.initializer)
      ) {
        usage.direct.set(node.initializer.text, where(node, sf))
      }
      if (ts.isStringLiteralLike(node)) usage.literals.add(node.text)
      ts.forEachChild(node, visit)
    }
    visit(sf)
  }
  return usage
}

describe('de.json', () => {
  const usage = collectUsage()
  const categories = new Set<string>(CATEGORIES)
  const namespaces = new Set([...UI_KEYS].map((key) => key.split('.')[0]))

  test('holds every key the code uses', () => {
    const missing = [...usage.direct]
      .filter(([key]) => !UI_KEYS.has(key))
      .map(([key, where]) => `${key} (${where})`)

    // Keys that travel through tables: a literal shaped like a key, under a
    // namespace the catalog has, is taken to be one. Category values share the
    // shape (`household.groceries`) and are not keys.
    const indirect = [...usage.literals].filter(
      (text) =>
        /^[a-z][A-Za-z]*(\.[A-Za-z0-9_-]+)+$/.test(text) &&
        namespaces.has(text.split('.')[0]) &&
        !categories.has(text) &&
        !UI_KEYS.has(text) &&
        !usage.direct.has(text) &&
        ![...usage.prefixes].some((prefix) => text.startsWith(prefix))
    )

    expect([...missing, ...indirect]).toEqual([])
  })

  test('holds no key nothing uses', () => {
    const orphans = [...UI_KEYS].filter(
      (key) =>
        !usage.direct.has(key) &&
        !usage.literals.has(key) &&
        ![...usage.prefixes].some((prefix) => key.startsWith(prefix))
    )
    expect(orphans).toEqual([])
  })

  test('prefixes built at runtime point at something', () => {
    const empty = [...usage.prefixes].filter(
      (prefix) => ![...UI_KEYS].some((key) => key.startsWith(prefix))
    )
    expect(empty).toEqual([])
  })
})

describe('de.help.json', () => {
  const PAGES: HelpKey[] = [
    'import',
    'plans',
    'plan',
    'book',
    'commitments',
    'accounts',
    'household',
  ]

  // Which keys the column asks for: the title of every page, and title and body
  // of every entry.
  const used = new Set<string>()
  for (const page of PAGES) {
    used.add(`${page}.title`)
    for (const entry of helpFor(page).entries) {
      const body = entry.body as { props: { i18nKey: string } }
      used.add(body.props.i18nKey)
      used.add(body.props.i18nKey.replace(/\.body$/, '.title'))
    }
  }

  test('holds every key the help column uses', () => {
    expect([...used].filter((key) => !HELP_KEYS.has(key))).toEqual([])
  })

  test('holds no entry the help column does not show', () => {
    expect([...HELP_KEYS].filter((key) => !used.has(key))).toEqual([])
  })
})

describe('source', () => {
  /**
   * German text outside the catalogs gives itself away by its umlauts. Comments
   * may keep them — only literals and JSX text are searched.
   */
  test('carries no text with umlauts outside the catalogs', () => {
    const found: string[] = []
    for (const [path, text] of SOURCES) {
      if (!path.endsWith('.tsx')) continue
      const sf = parse(path, text)
      const visit = (node: ts.Node) => {
        const literal =
          ts.isJsxText(node) || ts.isStringLiteralLike(node) || ts.isTemplateLiteralToken(node)
            ? node.text
            : null
        if (literal !== null && /[äöüÄÖÜß]/.test(literal)) {
          const line = sf.getLineAndCharacterOfPosition(node.getStart()).line + 1
          found.push(`${path}:${line} ${literal.trim()}`)
        }
        ts.forEachChild(node, visit)
      }
      visit(sf)
    }
    expect(found).toEqual([])
  })
})

describe('languages', () => {
  test('a test language shows its one key translated and everything else in German', async () => {
    const instance = i18next.createInstance()
    await instance.init({
      ...OPTIONS,
      resources: { ...RESOURCES, xx: { translation: testLanguage } },
      lng: 'xx',
    })

    expect(instance.t('nav.plan')).toBe('Planning')
    expect(instance.t('nav.book')).toBe(de.nav.book)
    expect(instance.t('errors.unknown')).toBe(de.errors.unknown)
    expect(instance.t('import.title', { ns: 'help' })).toBe(deHelp.import.title)
  })

  test('every further language is compared with German', () => {
    // Reported, not failed: a missing key falls back to German at runtime, and a
    // half-finished translation is still worth merging.
    for (const [language, catalogs] of Object.entries(RESOURCES)) {
      if (language === 'de') continue
      const own = {
        translation: new Set(Object.keys(flatten(catalogs.translation))),
        help: new Set(Object.keys(flatten(catalogs.help))),
      }
      const missing = [
        ...[...UI_KEYS].filter((key) => !own.translation.has(key)),
        ...[...HELP_KEYS].filter((key) => !own.help.has(key)).map((key) => `help:${key}`),
      ]
      if (missing.length > 0) {
        console.warn(`${language}: ${missing.length} keys fall back to German:\n  ${missing.join('\n  ')}`)
      }
    }
    expect(Object.keys(RESOURCES)).toContain('de')
  })
})
