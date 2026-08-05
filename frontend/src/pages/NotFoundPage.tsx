import { Link } from 'react-router'

import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-4 px-6 text-center">
      <p className="text-muted-foreground text-xs font-semibold tracking-widest uppercase">
        Duofy
      </p>
      <h1 className="font-heading text-4xl font-semibold">
        Diese Seite gibt es nicht
      </h1>
      <p className="text-muted-foreground max-w-sm">
        Vielleicht ein alter Link, vielleicht ein Tippfehler. Beides kein
        Weltuntergang.
      </p>
      <Button asChild className="mt-2">
        <Link to="/plan">Zur Planung</Link>
      </Button>
    </main>
  )
}
