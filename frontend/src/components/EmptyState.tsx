import type { ReactNode } from 'react'

import { Empty, EmptyDescription, EmptyHeader } from '@/components/ui/empty'

/**
 * The one shape of an empty list: a sentence, and the page's main button when the
 * viewer may do something about it.
 *
 * Every empty state goes through here, so none of them ends in a dead end — and none
 * invents a second call to action next to the one in the page header.
 */
export function EmptyState({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <Empty className="border-border rounded-xl border border-dashed">
      <EmptyHeader>
        <EmptyDescription>{children}</EmptyDescription>
      </EmptyHeader>
      {action}
    </Empty>
  )
}
