import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { renderAt, stub } from '@/test/plan-stub'

/**
 * #251: one plan view for the own plan, another person's plan and the household
 * plan. Header, print, tabs and the flow behave the same everywhere; only the data
 * source and the rights decide which actions are there.
 */

const SCOPES = [
  ['own plan', '/plan/2026/11', 'none'],
  ["another person's plan", '/plan/2026/11?member=u2', 'edit'],
  ['household plan', '/plan/2026/11?household=h1', 'edit'],
] as const

afterEach(() => vi.unstubAllGlobals())

describe.each(SCOPES)('the shared plan view: %s', (_name, path, level) => {
  test('shows the month, a print button and the tabs Plan, Buch, Verlauf', async () => {
    stub(level)
    renderAt(path)
    expect(
      await screen.findByRole('heading', { name: 'November 2026' })
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: i18n.t('plan.print') })).toBeInTheDocument()
    const tabs = screen.getAllByRole('tab').map((tab) => tab.textContent)
    expect(tabs[0]).toBe(i18n.t('plan.tabPlan'))
    expect(tabs[1]).toContain(i18n.t('plan.tabBook'))
    expect(tabs[2]).toBe(i18n.t('plan.tabFlow'))
  })

  test('the flow has no limits switch, only a hint that the setting is personal', async () => {
    stub(level)
    renderAt(path)
    await userEvent
      .setup()
      .click(await screen.findByRole('tab', { name: i18n.t('plan.tabFlow') }))
    const link = await screen.findByRole('link', {
      name: i18n.t('monthFlow.limitsChange'),
    })
    expect(link).toHaveAttribute('href', '/settings')
    expect(screen.queryByLabelText(i18n.t('monthFlow.limitsBy'))).not.toBeInTheDocument()
  })
})

describe('what the rights add to the shared view', () => {
  const add = () =>
    screen.queryByRole('button', { name: i18n.t('positionDialog.addTitle') })
  const remove = () => screen.queryByRole('button', { name: i18n.t('plan.deleteMonth') })

  test('own plan: add a position and delete the month', async () => {
    stub('none')
    renderAt('/plan/2026/11')
    await screen.findByRole('heading', { name: 'November 2026' })
    expect(add()).toBeInTheDocument()
    expect(remove()).toBeInTheDocument()
  })

  test('another person with the edit grant: adding, but no deleting', async () => {
    stub('edit')
    renderAt('/plan/2026/11?member=u2')
    await screen.findByRole('heading', { name: 'November 2026' })
    expect(add()).toBeInTheDocument()
    expect(remove()).not.toBeInTheDocument()
  })

  test('another person with the delete grant: adding and deleting', async () => {
    stub('delete')
    renderAt('/plan/2026/11?member=u2')
    await screen.findByRole('heading', { name: 'November 2026' })
    expect(add()).toBeInTheDocument()
    expect(remove()).toBeInTheDocument()
  })

  test('another person with the view grant: neither', async () => {
    stub('view')
    renderAt('/plan/2026/11?member=u2')
    await screen.findByRole('heading', { name: 'November 2026' })
    expect(add()).not.toBeInTheDocument()
    expect(remove()).not.toBeInTheDocument()
  })

  test('household plan: the household owns nothing, so neither', async () => {
    stub('edit')
    renderAt('/plan/2026/11?household=h1')
    await screen.findByRole('heading', { name: 'November 2026' })
    expect(add()).not.toBeInTheDocument()
    expect(remove()).not.toBeInTheDocument()
  })
})
