import { describe, expect, it } from 'vitest'

import { formatAmount, parseAmount } from './amount'

describe('parseAmount', () => {
  it.each([
    ['1.234,56', '1234.56'],
    ['1234,56', '1234.56'],
    ['1234.56', '1234.56'],
    ['12', '12.00'],
    ['12,5', '12.50'],
    ['0,01', '0.01'],
    ['1.234.567,89', '1234567.89'],
    ['1.234', '1234.00'],
    [' 45,00 € ', '45.00'],
    [',50', '0.50'],
    ['12,', '12.00'],
    ['1.234.567', '1234567.00'],
    ['1 234,56', '1234.56'],
    ['1 234,56', '1234.56'],
    ['1 234,56', '1234.56'],
  ])('reads %s as %s', (text, value) => {
    expect(parseAmount(text)).toEqual({ ok: true, value })
  })

  it.each([
    ['12e3', 'invalid'],
    ['abc', 'invalid'],
    ['-5', 'invalid'],
    ['1,2,3', 'invalid'],
    ['1.23.4', 'invalid'],
    ['', 'empty'],
    ['   ', 'empty'],
    ['0', 'tooSmall'],
    ['0,00', 'tooSmall'],
    ['0,005', 'tooManyDecimals'],
    ['0.005', 'tooManyDecimals'],
    ['12,345', 'tooManyDecimals'],
    ['12345678901', 'tooLarge'],
  ])('rejects %s (%s)', (text, reason) => {
    expect(parseAmount(text)).toEqual({ ok: false, reason })
  })

  it('never goes through a number: a long amount stays exact', () => {
    expect(parseAmount('9.999.999.999,99')).toEqual({ ok: true, value: '9999999999.99' })
  })
})

describe('parseAmount with allowZero', () => {
  it('reads zero only when asked to', () => {
    expect(parseAmount('0,00', { allowZero: true })).toEqual({ ok: true, value: '0.00' })
    expect(parseAmount('0,00')).toEqual({ ok: false, reason: 'tooSmall' })
  })
})

describe('formatAmount', () => {
  it('shows a valid API zero and survives a round trip', () => {
    expect(formatAmount('0.00')).toBe('0,00')
    for (const text of ['1.234,56', '0,50', '12,00', '1.234.567,00']) {
      const parsed = parseAmount(text)
      expect(parsed.ok && formatAmount(parsed.value)).toBe(text)
    }
  })

  it('writes an API amount the German way, with two decimals', () => {
    expect(formatAmount('1234.5')).toBe('1.234,50')
    expect(formatAmount('12')).toBe('12,00')
    expect(formatAmount('0.01')).toBe('0,01')
  })

  it('leaves an empty or unreadable value empty', () => {
    expect(formatAmount('')).toBe('')
    expect(formatAmount('abc')).toBe('')
  })
})

describe('parseAmount with allowNegative', () => {
  it.each([
    ['-150,00', '-150.00'],
    ['−150,00', '-150.00'],
    ['-1.234,5', '-1234.50'],
    ['0', '0.00'],
    ['-0,00', '0.00'],
    ['150', '150.00'],
  ])('reads %s as %s', (text, value) => {
    expect(parseAmount(text, { allowNegative: true })).toEqual({ ok: true, value })
  })

  it('refuses a second sign and a sign in the middle', () => {
    expect(parseAmount('--5', { allowNegative: true })).toEqual({ ok: false, reason: 'invalid' })
    expect(parseAmount('5-', { allowNegative: true })).toEqual({ ok: false, reason: 'invalid' })
  })

  it('refuses a sign everywhere else', () => {
    expect(parseAmount('-150,00')).toEqual({ ok: false, reason: 'invalid' })
    expect(parseAmount('-150,00', { allowZero: true })).toEqual({ ok: false, reason: 'invalid' })
  })

  it('shows a negative API value with its sign', () => {
    expect(formatAmount('-150.00')).toBe('-150,00')
  })
})
