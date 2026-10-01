import { ThemeProvider } from '@mui/material/styles'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { AuthProvider } from '../auth/AuthContext'
import { AppLayout } from '../layouts/AppLayout'
import { theme } from './theme'

const scheme = () => (document.documentElement.hasAttribute('data-dark') ? 'dark' : 'light')

describe('colour mode', () => {
  beforeEach(() => {
    localStorage.setItem('mobilebill.token', 'test-token')
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response(JSON.stringify({ id: 'u1', email: 'a@example.com', displayName: 'Test Admin', role: 'Administrator', status: 'Active' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
  })
  afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear(); document.documentElement.removeAttribute('data-dark') })

  it('starts light and switches to dark and back with the toolbar toggle', async () => {
    render(
      <ThemeProvider theme={theme} defaultMode="light">
        <AuthProvider><MemoryRouter><Routes><Route element={<AppLayout />}><Route index element={<div>Page</div>} /></Route></Routes></MemoryRouter></AuthProvider>
      </ThemeProvider>,
    )

    const toggle = await screen.findByRole('button', { name: 'Toggle dark mode' })
    expect(scheme()).not.toBe('dark')

    fireEvent.click(toggle)
    await waitFor(() => expect(scheme()).toBe('dark'))
    expect(localStorage.getItem('mui-mode')).toBe('dark')

    fireEvent.click(screen.getByRole('button', { name: 'Toggle dark mode' }))
    await waitFor(() => expect(scheme()).not.toBe('dark'))
    expect(localStorage.getItem('mui-mode')).toBe('light')
  })
})
