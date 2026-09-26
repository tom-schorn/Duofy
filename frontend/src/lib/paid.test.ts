import { describe, expect, it } from 'vitest'

import { positionHasBookings } from './paid'

describe('positionHasBookings', () => {
  it('is true when a booking hangs off the position', () => {
    expect(positionHasBookings('a', [{ positionId: 'b' }, { positionId: 'a' }])).toBe(true)
  })

  it('is false when only other positions or free bookings exist', () => {
    expect(positionHasBookings('a', [{ positionId: 'b' }, { positionId: null }])).toBe(false)
  })

  it('is false while the bookings are not loaded yet', () => {
    expect(positionHasBookings('a', undefined)).toBe(false)
  })
})
