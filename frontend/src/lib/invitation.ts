/**
 * The token out of whatever the person pasted: the whole link
 * (`https://…/register?invitation=abc`) or just the token itself.
 */
export function invitationToken(input: string): string {
  const text = input.trim()
  const match = /[?&]invitation=([^&#\s]+)/.exec(text)
  return match ? decodeURIComponent(match[1]) : text
}

/** The link an admin hands on. */
export function invitationLink(origin: string, token: string): string {
  return `${origin}/register?invitation=${encodeURIComponent(token)}`
}
