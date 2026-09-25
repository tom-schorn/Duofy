import type { ReactNode } from 'react'
import { Trans } from 'react-i18next'

import { i18n } from '@/lib/i18n'

/**
 * The texts of the explanation column, one set per page.
 *
 * The words live in their own catalog, `src/locales/<lang>.help.json`
 * (namespace `help`), apart from the short UI texts: they are long, they are
 * read as prose, and a translator works through them differently. They are
 * written together because the column shows them all at once — what belongs
 * together has to be written together, otherwise the entries drift apart in
 * tone and start repeating each other.
 *
 * Formatting is written as markers in the text (`<strong>`, `<list>`, `<code>`,
 * …) that `Trans` maps onto the elements below. No Markdown: the markers are
 * few, and the own code highlighting has to survive.
 *
 * TODO: move the texts into the wiki and load an excerpt from there. Written twice
 * — once here, once in an article — they will contradict each other within weeks.
 * Needs a public wiki page first, see the note in the project docs.
 */

export type HelpEntry = {
  /** Stable key — decides which entry stays open across a re-render. */
  id: string
  title: string
  body: ReactNode
}

/** Which page a set of entries belongs to. */
export type HelpKey =
  | 'import'
  | 'plans'
  | 'plan'
  | 'book'
  | 'commitments'
  | 'accounts'
  | 'household'

/** What each marker in the help catalog turns into. */
const MARKERS = {
  p: <p />,
  pSpaced: <p className="mt-2" />,
  guide: <p className="text-foreground font-medium" />,
  strong: <strong />,
  em: <em />,
  list: <ul className="flex list-disc flex-col gap-1 pl-4" />,
  listSpaced: <ul className="mt-2 flex list-disc flex-col gap-1 pl-4" />,
  li: <li />,
  // The own code highlighting — a plain `<code>` would look like body text.
  code: <code className="bg-muted rounded px-1 py-0.5 text-[0.95em]" />,
}

/** The three budgets, explained once and shown on the plan page. */
const PLAN_BUDGETS = ['budgets.needs', 'budgets.wants', 'budgets.savings']

/** Catalog paths of the entries, in the order the column lists them. */
const ENTRIES: Record<HelpKey, string[]> = {
  import: [
    'parking',
    'file',
    'account',
    'twice',
    'balances',
    'transfer',
    'other-side',
    'which-month',
    'nothing-changed',
  ].map((id) => `import.entries.${id}`),
  plans: ['overview', 'switch', 'ritual'].map((id) => `plans.entries.${id}`),
  plan: ['plan.entries.month', ...PLAN_BUDGETS, 'plan.entries.tick'],
  book: ['book', 'assignment', 'transfer', 'shared-book'].map(
    (id) => `book.entries.${id}`
  ),
  commitments: ['commitment', 'types', 'rhythm', 'privacy'].map(
    (id) => `commitments.entries.${id}`
  ),
  accounts: ['account', 'no-depot', 'available', 'iban'].map(
    (id) => `accounts.entries.${id}`
  ),
  household: [
    'household',
    'grants',
    'areas',
    'levels',
    'areas-hang-together',
    'invitations',
    'leaving',
  ].map((id) => `household.entries.${id}`),
}

/** The title and entries of one page, in the active language. */
export function helpFor(key: HelpKey): { title: string; entries: HelpEntry[] } {
  return {
    title: i18n.t(`${key}.title`, { ns: 'help' }),
    entries: ENTRIES[key].map((path) => ({
      id: path.slice(path.lastIndexOf('.') + 1),
      title: i18n.t(`${path}.title`, { ns: 'help' }),
      body: <Trans ns="help" i18nKey={`${path}.body`} components={MARKERS} />,
    })),
  }
}
