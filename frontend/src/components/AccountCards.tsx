import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
} from '@/components/ui/card'
import { QueryState } from '@/components/QueryState'
import {
  ACCOUNT_TYPE_LABEL,
  euro,
  OWN_SCOPE,
  type Account,
  type BookScope,
} from '@/lib/domain'
import { useAccounts } from '@/lib/queries'

/**
 * The account balances as a row of cards — in the book, right below the plan
 * figures.
 *
 * The plan says what the month intends. This row says what is actually there.
 * Having both side by side is the point: 500 allocated and 80 on the account is a
 * statement neither figure makes on its own.
 *
 * The balance is **not tied to a month**. An account has a balance, not an August
 * balance — which is why it does not change when you switch months.
 */
type Props = {
  /** Whose book: your own, one person, or the household. */
  scope?: BookScope
}

export function AccountCards({ scope = OWN_SCOPE }: Props) {
  const accounts = useAccounts(scope)
  const usable = (accounts.data ?? []).filter((account) => account.active)

  if (!accounts.isPending && usable.length === 0) return null

  return (
    <QueryState isPending={accounts.isPending} error={accounts.error} rows={1}>
      <section
        aria-label="Kontostände"
        className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"
      >
        {usable.map((account) => (
          <AccountCard key={account.id} account={account} />
        ))}
        <TotalCard accounts={usable} />
      </section>
    </QueryState>
  )
}

function AccountCard({ account }: { account: Account }) {
  const balance = Number(account.balance)

  return (
    <Card size="sm" className="gap-2">
      <CardHeader>
        <CardDescription className="truncate text-[11px] font-semibold tracking-widest uppercase">
          {account.name}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-0.5">
      {/* Ein Minus heißt hier überzogen, nicht „Ausgabe" — deshalb rot. */}
      <span
        className={`font-mono text-lg font-medium tabular-nums ${
          balance < 0 ? 'text-destructive' : ''
        }`}
      >
        {euro.format(balance)}
      </span>
        <span className="text-muted-foreground text-xs">
          {ACCOUNT_TYPE_LABEL[account.type]}
          {account.ownerName ? ` · ${account.ownerName}` : ''}
        </span>
      </CardContent>
    </Card>
  )
}

/**
 * The total across all accounts.
 *
 * Deliberately a card of its own at the end and not emphasised: it is the least
 * useful number in the row. Bills are paid from a specific account, not from the
 * total — 2,000 in total does not help when the rent goes off the current account
 * and there are 40 on it.
 */
function TotalCard({ accounts }: { accounts: Account[] }) {
  const total = accounts.reduce(
    (sum, account) => sum + Number(account.balance),
    0
  )

  return (
    <Card size="sm" className="bg-muted/30 gap-2 ring-dashed">
      <CardHeader>
        <CardDescription className="text-[11px] font-semibold tracking-widest uppercase">
          Alle Konten
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-0.5">
        <span className="font-mono text-lg font-medium tabular-nums">
          {euro.format(total)}
        </span>
        <span className="text-muted-foreground text-xs">
          {accounts.length} Konten
        </span>
      </CardContent>
    </Card>
  )
}
