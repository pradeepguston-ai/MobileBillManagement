import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import App from './App'
import { AuthProvider } from './auth/AuthContext'

const jsonResponse = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const currentUserResponse = () => jsonResponse({ id: 'user-1', email: 'admin@example.com', displayName: 'Test Admin', role: 'Administrator', status: 'Active' })

function mockAuthenticatedFetch(handler?: (url: string, init?: RequestInit) => Response) {
  localStorage.setItem('mobilebill.token', 'test-token')
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input)
    if (url.includes('/api/auth/me')) return currentUserResponse()
    return handler ? handler(url, init) : jsonResponse({})
  })
}

function renderApp() {
  return render(<AuthProvider><App /></AuthProvider>)
}

describe('App', () => {
  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
    localStorage.clear()
    window.history.pushState({}, '', '/')
  })

  it('shows the product title and dashboard navigation', async () => {
    mockAuthenticatedFetch()
    renderApp()

    expect(await screen.findByText('Mobile Bill Management')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeTruthy()
  })

  it('shows the grouped billing, master-data, and report navigation', async () => {
    mockAuthenticatedFetch()
    renderApp()

    expect(await screen.findByText('Billing')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Billing Batches' }).getAttribute('href')).toBe('/billing')
    expect(screen.getByRole('link', { name: 'Monthly Bill Review' }).getAttribute('href')).toBe('/monthly-bill-review')
    expect(screen.getByRole('link', { name: 'Exception Review' }).getAttribute('href')).toBe('/exception-review')
    expect(screen.queryByRole('link', { name: 'Bill Review' })).toBeNull()
    expect(screen.getByText('Masters')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Telecom Providers' }).getAttribute('href')).toBe('/providers')
    expect(screen.getByText('Reports')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Monthly Bill Report' }).getAttribute('href')).toBe('/reports/monthly-bill')
  })

  it('opens mobile navigation and closes it after selecting a route', async () => {
    window.matchMedia = vi.fn().mockImplementation(query => ({ matches: query.includes('max-width'), media: query, onchange: null, addListener: vi.fn(), removeListener: vi.fn(), addEventListener: vi.fn(), removeEventListener: vi.fn(), dispatchEvent: vi.fn() }))
    mockAuthenticatedFetch()
    renderApp()

    fireEvent.click(await screen.findByRole('button', { name: 'Open navigation' }))
    const reportLink = await screen.findByRole('link', { name: 'Monthly Bill Report' })
    expect(reportLink.getAttribute('href')).toBe('/reports/monthly-bill')
    fireEvent.click(reportLink)

    expect(await screen.findByRole('heading', { name: 'Monthly Bill Report' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Close navigation' })).toBeNull()
  })

  it('routes the browser to the billing batch list', async () => {
    window.history.pushState({}, '', '/billing')
    mockAuthenticatedFetch(() => jsonResponse({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))

    renderApp()

    expect(await screen.findByRole('heading', { name: 'Billing Batches' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'New Billing Batch' }).getAttribute('href')).toBe('/billing/new')
  })
})
