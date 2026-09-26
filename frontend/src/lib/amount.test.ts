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

describe('formatAmount', () => {
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
