import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

import { LegalLinks, LEGAL_DOCUMENTS } from '@/components/LegalLinks'
import { errorText, ApiError } from '@/lib/api'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { useLegalText } from '@/lib/queries'

/**
 * One of the operator's legal texts, outside the app shell so it opens without
 * signing in. The text is shown as it is written — plain, no markup is executed.
 * Without a configured text the page does not exist (404), matching the missing
 * footer link.
 */
export function LegalPage({ name }: { name: (typeof LEGAL_DOCUMENTS)[number]['name'] }) {
  const { t } = useTranslation()
  const document = useLegalText(name)

  // Only a real 404 means "not configured"; a failing server is something else.
  if (document.error instanceof ApiError && document.error.status === 404) {
    return <NotFoundPage />
  }

  return (
    <main className="mx-auto flex min-h-svh max-w-2xl flex-col gap-6 px-6 py-10">
      <Link to="/" className="text-muted-foreground text-sm underline-offset-4 hover:underline">
        {t('legal.back')}
      </Link>
      {document.isError && (
        <p role="alert" className="text-destructive text-sm">
          {errorText(document.error)}
        </p>
      )}
      {document.data && (
        <>
          <h1 className="font-heading text-3xl font-semibold">{t(`legal.${name}`)}</h1>
          <p className="text-sm whitespace-pre-wrap">{document.data.text}</p>
        </>
      )}
      <LegalLinks className="mt-auto" />
    </main>
  )
}
