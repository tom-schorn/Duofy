import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider, useLocation } from 'react-router'
import { describe, expect, test } from 'vitest'

import { BookRedirect } from '@/pages/BookRedirect'

function Where() {
  const location = useLocation()
  return <p data-testid="where">{location.pathname + location.search}</p>
}

function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/book', element: <BookRedirect /> },
      { path: '/plan/:year/:month', element: <Where /> },
    ],
    { initialEntries: [path] }
  )
  render(<RouterProvider router={router} />)
  return () => screen.getByTestId('where').textContent
}

describe('the old book address (#241)', () => {
  test('an old /book?month= link lands on the book tab of that plan month', () => {
    expect(renderAt('/book?month=2026-09')()).toBe('/plan/2026/09?tab=book')
  })

  test('the person being viewed stays in the address', () => {
    expect(renderAt('/book?month=2026-09&member=u2')()).toBe(
      '/plan/2026/09?member=u2&tab=book'
    )
  })

  test('the household being viewed stays in the address', () => {
    expect(renderAt('/book?month=2026-09&household=h1')()).toBe(
      '/plan/2026/09?household=h1&tab=book'
    )
  })

  test('with both in the address the household wins, as in the sidebar', () => {
    expect(renderAt('/book?month=2026-09&member=u2&household=h1')()).toBe(
      '/plan/2026/09?household=h1&tab=book'
    )
  })

  test.each(['2026-13', 'abc', '2026-9', ''])(
    'an invalid month %j opens the current month',
    (value) => {
      const now = new Date()
      const month = String(now.getMonth() + 1).padStart(2, '0')
      expect(renderAt(`/book?month=${value}`)()).toBe(
        `/plan/${now.getFullYear()}/${month}?tab=book`
      )
    }
  )

  test('without a month it opens the current month', () => {
    const now = new Date()
    const month = String(now.getMonth() + 1).padStart(2, '0')
    expect(renderAt('/book')()).toBe(`/plan/${now.getFullYear()}/${month}?tab=book`)
  })
})
