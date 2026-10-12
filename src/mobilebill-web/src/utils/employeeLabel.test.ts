import { describe, expect, it } from 'vitest'

import { employeeLabel, employeeSearchText } from './employeeLabel'

describe('employee label', () => {
  it('shows EPF, name with initials, calling name and factory', () => {
    expect(employeeLabel({ epf: '211832', nameWithInitials: 'P.D.P. GUNAWANTHA', fullName: 'PUSSELLAGE DINESH PUSHPAKUMARA GUNAWANTHA', callingName: 'DINESH', factory: 'CONCORD APPAREL (PVT) LTD' }))
      .toBe('211832 — P.D.P. GUNAWANTHA (DINESH) — CONCORD APPAREL (PVT) LTD')
  })

  it('uses the full name when there are no initials and leaves out what is missing', () => {
    expect(employeeLabel({ epf: 'GUS_01', nameWithInitials: '', fullName: 'MIKAEL OLANDER', callingName: null, factory: 'GUSTON LIMITED' })).toBe('GUS_01 — MIKAEL OLANDER — GUSTON LIMITED')
    expect(employeeLabel({ epf: 'E1', fullName: 'Someone' })).toBe('E1 — Someone')
  })

  it('lets the full name be searched', () => {
    expect(employeeSearchText({ fullName: ' PUSSELLAGE DINESH ' })).toBe('PUSSELLAGE DINESH')
  })
})
