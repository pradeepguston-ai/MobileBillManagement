import { describe, expect, it } from 'vitest'

import { approvalComment, approvalRemarks, displayName, isApproval, reviewColumns } from './reviewPresentation'

describe('review table columns', () => {
  it('shows the name with initials, then the calling name, and no section columns', () => {
    const labels = reviewColumns.map(column => column.label)
    expect(labels.slice(0, 4)).toEqual(['Mobile', 'EPF', 'Name With Initials', 'Calling Name'])
    expect(labels).not.toContain('Section')
    expect(labels).not.toContain('Sub Section')
  })

  it('falls back to the full name when there are no initials', () => {
    expect(displayName({ employeeName: 'PUSSELLAGE DINESH', nameWithInitials: 'P.D. GUNAWANTHA' })).toBe('P.D. GUNAWANTHA')
    expect(displayName({ employeeName: 'SAMAN PERERA', nameWithInitials: '  ' })).toBe('SAMAN PERERA')
    expect(displayName({ employeeName: 'SIM Pool (Unassigned)', nameWithInitials: null })).toBe('SIM Pool (Unassigned)')
  })
})

describe('approval remarks', () => {
  it('offers the four approval remarks, Approved first', () => {
    expect(approvalRemarks).toEqual(['Approved', 'Reviewed and Approved', 'Verified and Approved', 'Approved with Conditions'])
  })

  it('saves the remark alone, or the remark followed by the typed comment', () => {
    expect(approvalComment('Verified and Approved', '   ')).toBe('Verified and Approved')
    expect(approvalComment('Approved with Conditions', ' Recover 761499168 next month ')).toBe('Approved with Conditions: Recover 761499168 next month')
  })

  it('applies only to approve decisions', () => {
    expect(isApproval({ kind: 'decision', action: 'Approve', title: '', commentRequired: false })).toBe(true)
    expect(isApproval({ kind: 'decision', action: 'Reject', title: '', commentRequired: true })).toBe(false)
    expect(isApproval({ kind: 'submit', title: '', commentRequired: false })).toBe(false)
  })
})
