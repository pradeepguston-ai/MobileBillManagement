import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { UserRole } from '../api/authApi'

// The signed-in role, applied through the real role list behind useCanPrepareBilling.
const auth = vi.hoisted(() => ({ role: 'ITEngineer' as string }))
vi.mock('../auth/AuthContext', async () => {
  const { billingPreparerRoles } = await import('../auth/roles')
  return { useCanPrepareBilling: () => (billingPreparerRoles as readonly string[]).includes(auth.role) }
})

import { MonthlyBillReportPage } from './MonthlyBillReportPage'

const batch = { id: 'locked', providerId: 'p1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 10, calculatedGrandTotal: 90, validationLevel: 'StructuralOnly', status: 'Locked', originalFileName: 'oct.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-11-01T00:00:00Z' }
const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('Monthly Bill Report email button', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it.each<[UserRole, boolean]>([
    ['ITEngineer', true],
    ['Administrator', true],
    ['HeadOfIt', false],
    ['GroupHrManager', false],
    ['Cfo', false],
    ['HrUser', false],
    ['FinanceUser', false],
    ['Viewer', false],
  ])('%s sees Email Reports: %s', async (role, visible) => {
    auth.role = role
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/api/bill-batches?')
      ? json({ items: [batch], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
      : json({ batchId: 'locked', totalActualBill: 90, totalFinalDeduction: 9, unresolvedExceptionCount: 0 }))
    render(<MemoryRouter><MonthlyBillReportPage /></MemoryRouter>)

    // Every role can still download the reports.
    expect(await screen.findByRole('button', { name: 'Download Excel' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Email Reports' }) !== null).toBe(visible)
  })
})
