import { describe, expect, test } from 'vitest'

import { dateLocale, FALLBACK_LANGUAGE, locale } from '@/lib/i18n'

describe('dateLocale', () => {
  test('follows the active language', () => {
    expect(locale()).toBe('de')
    expect(dateLocale().code).toBe('de')
  })

  test('starts the German week on Monday', () => {
    expect(dateLocale('de').options?.weekStartsOn).toBe(1)
  })

  test('falls back to German for a language without a pack, not to English', () => {
    expect(dateLocale('xx').code).toBe(FALLBACK_LANGUAGE)
  })
})
