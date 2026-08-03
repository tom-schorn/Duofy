import { QueryState } from '@/components/QueryState'
import { ACCOUNT_TYPE_LABEL, euro, type Account } from '@/lib/domain'
import { useAccounts } from '@/lib/queries'

/**
 * Die Kontostände als Kartenreihe — im Buch, direkt unter den Plan-Zahlen.
 *
 * Der Plan sagt, was der Monat vorhat. Diese Reihe sagt, was tatsächlich da
 * ist. Beides nebeneinander ist der Sinn: 500 € verplant und 80 € auf dem
 * Konto ist eine Aussage, die keine der beiden Zahlen allein trifft.
 *
 * Der Stand ist **nicht monatsgebunden**. Ein Konto hat einen Stand, keinen
 * August-Stand — deshalb ändert er sich nicht, wenn man den Monat wechselt.
 */
type Props = {
  /** Fremder Besitzer für die Personenansicht. null = eigene Konten. */
  ownerId?: string | null
}

export function AccountCards({ ownerId = null }: Props) {
  const accounts = useAccounts(ownerId)
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
    <div className="bg-card border-border flex flex-col gap-1 rounded-lg border p-4">
      <span className="text-muted-foreground truncate text-[11px] font-semibold tracking-widest uppercase">
        {account.name}
      </span>
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
      </span>
    </div>
  )
}

/**
 * Die Summe über alle Konten.
 *
 * Bewusst als eigene Karte am Ende und nicht hervorgehoben: sie ist die
 * unwichtigste Zahl der Reihe. Bezahlt wird von einem bestimmten Konto, nicht
 * aus der Summe — 2.000 € gesamt helfen nicht, wenn die Miete vom Giro geht
 * und dort 40 € liegen.
 */
function TotalCard({ accounts }: { accounts: Account[] }) {
  const total = accounts.reduce(
    (sum, account) => sum + Number(account.balance),
    0
  )

  return (
    <div className="border-border/60 bg-muted/30 flex flex-col gap-1 rounded-lg border border-dashed p-4">
      <span className="text-muted-foreground text-[11px] font-semibold tracking-widest uppercase">
        Alle Konten
      </span>
      <span className="font-mono text-lg font-medium tabular-nums">
        {euro.format(total)}
      </span>
      <span className="text-muted-foreground text-xs">
        {accounts.length} Konten
      </span>
    </div>
  )
}
