import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

vi.mock('../../auth/AuthContext', () => ({ useCanEditMasterData: () => false }))

import { DepartmentsPage } from './MasterPages'

describe('MasterDataPage for read-only roles', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('lists records without create, edit or deactivate actions', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ items: [{ id: 'department-1', code: 'IT', name: 'Information Technology', isActive: true }], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))

    render(<DepartmentsPage />)

    expect(await screen.findByText('Information Technology')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Create' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Deactivate' })).toBeNull()
    expect(screen.queryByText('Actions')).toBeNull()
  })
})
