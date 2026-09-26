import { describe, expect, test } from 'vitest'

import { invitationLink, invitationToken } from '@/lib/invitation'

describe('invitation link', () => {
  test('the token comes out of a pasted link', () => {
    const link = invitationLink('https://duofy.example', 'a-b_c')
    expect(invitationToken(link)).toBe('a-b_c')
  })

  test('a bare token and surrounding spaces are taken as they are', () => {
    expect(invitationToken('  a-b_c \n')).toBe('a-b_c')
  })
})
