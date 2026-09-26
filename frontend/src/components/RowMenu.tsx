import { MoreHorizontal, type LucideIcon } from 'lucide-react'
import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

export type RowMenuItem = {
  key: string
  /** A verb in the infinitive: Beenden, Deaktivieren, Löschen. */
  label: string
  icon?: LucideIcon
  /** Goes last, red, behind a separator. */
  destructive?: boolean
  onSelect: () => void
}

/**
 * The ⋯ of a row (UI guideline rules 5–6): only what is not "edit".
 *
 * Without an action there is no menu at all — a ⋯ with nothing in it, or with
 * only "Bearbeiten", is noise. A missing right means no items, so nothing is shown
 * greyed out. `disabled` is for the short time a save is running.
 */
export function RowMenu({
  name,
  items,
  disabled = false,
}: {
  /** The object the menu belongs to; it becomes part of the button's name. */
  name: string
  items: RowMenuItem[]
  disabled?: boolean
}) {
  const { t } = useTranslation()
  if (items.length === 0) return null

  // Frequent before rare, destructive last.
  const ordered = [...items].sort(
    (a, b) => Number(a.destructive ?? false) - Number(b.destructive ?? false)
  )

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="size-8"
          disabled={disabled}
          aria-label={t('ui.rowMenu', { name })}
        >
          <MoreHorizontal className="size-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {ordered.map((item, index) => (
          <Fragment key={item.key}>
            {item.destructive && index > 0 && !ordered[index - 1].destructive && (
              <DropdownMenuSeparator />
            )}
            <DropdownMenuItem
              onSelect={item.onSelect}
              variant={item.destructive ? 'destructive' : 'default'}
              className="gap-2"
            >
              {item.icon && <item.icon className="size-4" />}
              {item.label}
            </DropdownMenuItem>
          </Fragment>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
