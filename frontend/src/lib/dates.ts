/**
 * Datum als ISO-Tag, ohne Zeitzonen-Falle.
 *
 * `new Date().toISOString().slice(0, 10)` sieht harmlos aus und ist in Europa
 * falsch: `toISOString` rechnet nach UTC. Bucht jemand um 00:30 deutscher
 * Sommerzeit, liefert es den **Vortag** — die Buchung landet einen Tag zu früh
 * und damit womöglich im falschen Monat.
 *
 * Deshalb überall diese Helfer statt `toISOString`.
 */

/** `2026-08-03` aus den **lokalen** Datumsteilen. */
export function toIsoDay(date: Date): string {
  const jahr = date.getFullYear()
  const monat = String(date.getMonth() + 1).padStart(2, '0')
  const tag = String(date.getDate()).padStart(2, '0')
  return `${jahr}-${monat}-${tag}`
}

/** Heute als ISO-Tag. */
export function today(): string {
  return toIsoDay(new Date())
}

/**
 * ISO-Tag zu `Date` — als **lokale** Mitternacht.
 *
 * `new Date('2026-08-03')` liest den String als UTC und ergibt in Deutschland
 * den 3. August um 02:00. Beim Zurückrechnen über die lokalen Teile stimmt das
 * noch, bei Zeitzonen westlich von UTC nicht mehr.
 */
export function fromIsoDay(iso: string): Date | undefined {
  const teile = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso)
  if (!teile) return undefined
  return new Date(Number(teile[1]), Number(teile[2]) - 1, Number(teile[3]))
}

const LANG = new Intl.DateTimeFormat('de-DE', {
  day: '2-digit',
  month: 'long',
  year: 'numeric',
})

/** `03. August 2026` — für Knöpfe, die ein gewähltes Datum zeigen. */
export function langesDatum(iso: string): string {
  const date = fromIsoDay(iso)
  return date ? LANG.format(date) : ''
}
