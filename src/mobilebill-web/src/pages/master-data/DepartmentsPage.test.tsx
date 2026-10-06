import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

vi.mock('../../auth/AuthContext', () => ({ useCanEditMasterData: () => false }))

import { DepartmentsPage } from './MasterPages'

const json = (items: unknown[]) => new Response(JSON.stringify({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('DepartmentsPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows departments, sections and sub sections on separate tabs', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const path = new URL(String(input), 'http://localhost').pathname
      if (path === '/api/sections') return json([{ id: 's1', code: 'IT-INF', name: 'Infrastructure', departmentCode: 'IT', departmentName: 'Information Technology', isActive: true }])
      if (path === '/api/sub-sections') return json([{ id: 'ss1', code: 'IT-NET', name: 'Network', sectionCode: 'IT-INF', sectionName: 'Infrastructure', departmentCode: 'IT', departmentName: 'Information Technology', isActive: true }])
      return json([{ id: 'd1', code: 'IT', name: 'Information Technology', isActive: true }])
    })

    render(<DepartmentsPage />)

    expect(screen.getByRole('heading', { name: 'Departments' })).toBeTruthy()
    expect(await screen.findByText('Information Technology')).toBeTruthy()

    fireEvent.click(screen.getByRole('tab', { name: 'Sections' }))
    expect(await screen.findByText('Infrastructure')).toBeTruthy()

    fireEvent.click(screen.getByRole('tab', { name: 'Sub Sections' }))
    expect(await screen.findByText('Network')).toBeTruthy()
    expect(screen.getByRole('tab', { name: 'Sub Sections' }).getAttribute('aria-selected')).toBe('true')
  })
})
