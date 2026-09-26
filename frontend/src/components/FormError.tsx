import { errorText } from '@/lib/api'

/** The error of a form action, above the buttons; the input stays where it was. */
export function FormError({ error }: { error: unknown }) {
  if (!error) return null
  return (
    <p role="alert" className="border-destructive bg-destructive/10 text-destructive rounded-lg border px-4 py-3 text-sm">
      {errorText(error)}
    </p>
  )
}
