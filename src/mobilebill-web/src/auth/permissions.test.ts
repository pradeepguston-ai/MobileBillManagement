import { describe, expect, it } from 'vitest'

import { allows, defaultAllows } from './permissions'

describe('screen permissions', () => {
  it('match today\'s access rules by default', () => {
    expect(defaultAllows('HrUser', 'employees', 'Edit')).toBe(true)
    expect(defaultAllows('HrUser', 'providers', 'Edit')).toBe(false)
    expect(defaultAllows('HrUser', 'providers', 'View')).toBe(true)
    expect(defaultAllows('Cfo', 'report-distribution', 'View')).toBe(false)
    expect(defaultAllows('ITEngineer', 'report-distribution', 'Delete')).toBe(true)
  })

  it('use the saved permissions once loaded, and the defaults for screens not in them', () => {
    const saved = [{ module: 'employees' as const, canView: true, canCreate: false, canEdit: false, canDelete: false }]
    expect(allows(saved, 'HrUser', 'employees', 'Edit')).toBe(false)
    expect(allows(saved, 'HrUser', 'factories', 'Edit')).toBe(true)
  })

  it('always allow the Administrator', () => {
    expect(allows([{ module: 'employees', canView: false, canCreate: false, canEdit: false, canDelete: false }], 'Administrator', 'employees', 'Delete')).toBe(true)
  })
})
