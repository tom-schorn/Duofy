/**
 * Whether a position already carries bookings. Ticking it off then creates no new
 * booking — the backend counts bookings, not the actual amount, which can also be
 * typed in by hand — so the tick dialog asks the same question of the same data.
 */
export function positionHasBookings(
  positionId: string,
  transactions: readonly { positionId: string | null }[] | undefined
): boolean {
  return transactions?.some((entry) => entry.positionId === positionId) ?? false
}
