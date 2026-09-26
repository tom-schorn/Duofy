import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-4 px-6 text-center">
      <p className="text-muted-foreground text-xs font-semibold tracking-widest uppercase">
        Duofy
      </p>
      <h1 className="font-heading text-4xl font-semibold">
        {t('notFound.title')}
      </h1>
      <p className="text-muted-foreground max-w-sm">
        {t('notFound.text')}
      </p>
      <Button asChild className="mt-2">
        <Link to="/plan">{t('notFound.back')}</Link>
      </Button>
    </main>
  )
}
