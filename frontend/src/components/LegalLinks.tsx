import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

import { useLegalDocuments } from '@/lib/queries'

/** The documents an operator can configure, in footer order, with their routes. */
export const LEGAL_DOCUMENTS = [
  { name: 'imprint', path: '/impressum' },
  { name: 'privacy', path: '/datenschutz' },
  { name: 'terms', path: '/agb' },
] as const

/**
 * The footer line with the operator's legal pages.
 *
 * Shows exactly the documents the instance has configured — and nothing at all if
 * there are none: an empty link is worse than none.
 */
export function LegalLinks({ className }: { className?: string }) {
  const { t } = useTranslation()
  const documents = useLegalDocuments().data?.documents ?? []
  const shown = LEGAL_DOCUMENTS.filter((document) => documents.includes(document.name))

  if (shown.length === 0) return null

  return (
    <nav aria-label={t('legal.footer')} className={className}>
      <ul className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-xs">
        {shown.map((document) => (
          <li key={document.name}>
            <Link
              to={document.path}
              className="hover:text-foreground underline-offset-4 hover:underline"
            >
              {t(`legal.${document.name}`)}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  )
}
