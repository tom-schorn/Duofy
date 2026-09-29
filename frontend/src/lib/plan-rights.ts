import type { PlanPosition } from '@/lib/domain'

/**
 * What the person looking may do in this plan (#251).
 *
 * The only thing that differs between the own plan, another person's plan and the
 * household plan besides where the data comes from. Each caller derives it from the
 * grants; the endpoints check everything again, this only decides which controls
 * are offered — a missing right removes the control, it does not disable it.
 */
export type PlanRights = {
  /** Add a position to this plan. */
  addPosition: boolean
  /** Add a booking: the book grant reaches `create`. */
  addBooking: boolean
  /** Delete the whole month. */
  deleteMonth: boolean
  /** Change a position in its dialog. */
  editPosition: (position: PlanPosition) => boolean
  /** Tick a position off or take the tick back: a booking in the owner's book. */
  tickPosition: (position: PlanPosition) => boolean
  /** Delete a position. */
  deletePosition: (position: PlanPosition) => boolean
}

/** Every right, for the person's own plan. */
export const OWN_RIGHTS: PlanRights = {
  addPosition: true,
  addBooking: true,
  deleteMonth: true,
  editPosition: () => true,
  tickPosition: () => true,
  deletePosition: () => true,
}

/** The tabs of the view; the value lives in the address, the labels are German. */
export const PLAN_TABS = new Set(['plan', 'book', 'flow'])
