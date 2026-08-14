import { SelectGroup, SelectItem, SelectLabel } from '@/components/ui/select'
import { CATEGORY_GROUPS, CATEGORY_LABEL } from '@/lib/domain'

/**
 * The contents of every category dropdown, cut into their groups.
 *
 * Its own component because three forms ask the same question — the book, the
 * position and the commitment. A list of nearly forty entries is only bearable
 * with headings, and the headings have to be identical everywhere.
 *
 * Goes inside a `SelectContent`, not around it: the surrounding `Select` keeps
 * its own value and handler.
 */
export function CategoryOptions() {
  return (
    <>
      {CATEGORY_GROUPS.map((group) => (
        <SelectGroup key={group.label ?? 'ungrouped'}>
          {group.label && <SelectLabel>{group.label}</SelectLabel>}
          {group.categories.map((category) => (
            <SelectItem key={category} value={category}>
              {CATEGORY_LABEL[category]}
            </SelectItem>
          ))}
        </SelectGroup>
      ))}
    </>
  )
}
