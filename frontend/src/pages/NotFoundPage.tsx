import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

import { Button } from '@/components/ui/button'

/**
 * The message alone, for inside the app shell — the sidebar and header are
 * already there. Routes outside the layout use `NotFoundPage`.
 */
export function NotFoundBody() {
  const { t } = useTranslation()
  return (
    <div className="flex flex-col items-center gap-4 px-6 py-16 text-center">
      <h1 className="font-heading text-4xl font-semibold">{t('notFound.title')}</h1>
      <p className="text-muted-foreground max-w-sm">{t('notFound.text')}</p>
      <Button asChild className="mt-2">
        <Link to="/plan">{t('notFound.back')}</Link>
      </Button>
    </div>
  )
}

export function NotFoundPage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center">
      <p className="text-muted-foreground text-xs font-semibold tracking-widest uppercase">
        Duofy
      </p>
      <NotFoundBody />
    </main>
  )
}
