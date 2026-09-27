import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from '@/components/ui/button'

/**
 * An error that belongs to the page rather than a form: a box announced as an alert,
 * with "Erneut versuchen" when there is something to run again.
 */
export function ErrorBox({ children, onRetry }: { children: ReactNode; onRetry?: () => void }) {
  const { t } = useTranslation()
  return (
    <div
      role="alert"
      className="border-destructive bg-destructive/10 text-destructive flex flex-wrap items-center justify-between gap-3 rounded-lg border px-4 py-3 text-sm"
    >
      <span>{children}</span>
      {onRetry && (
        <Button type="button" variant="outline" size="sm" onClick={onRetry}>
          {t('common.retry')}
        </Button>
      )}
    </div>
  )
}
