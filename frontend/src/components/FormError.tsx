import { errorText } from '@/lib/api'

/** The error of a form action, above the buttons; the input stays where it was. */
export function FormError({ error }: { error: unknown }) {
  if (!error) return null
  return (
    <p role="alert" className="text-destructive text-sm">
      {errorText(error)}
    </p>
  )
}
