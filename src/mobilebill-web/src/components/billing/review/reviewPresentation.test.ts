import { describe, expect, it } from 'vitest'

import { approvalComment, approvalRemarks, isApproval } from './reviewPresentation'

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
