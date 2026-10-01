import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ForgotPasswordPage } from './ForgotPasswordPage'

function fill(email: string, password: string, confirm: string) {
  fireEvent.change(screen.getByLabelText(/Email/), { target: { value: email } })
  fireEvent.change(screen.getByLabelText(/^New password/), { target: { value: password } })
  fireEvent.change(screen.getByLabelText(/Confirm new password/), { target: { value: confirm } })
  fireEvent.click(screen.getByRole('button', { name: 'Request reset' }))
}

describe('ForgotPasswordPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('sends the reset request and tells the user an administrator must approve it', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 202 }))
    render(<MemoryRouter><ForgotPasswordPage /></MemoryRouter>)

    fill('engineer@example.com', 'new-password-1', 'new-password-1')

    expect(await screen.findByText('Request sent')).toBeTruthy()
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toContain('/api/auth/forgot-password')
    expect(JSON.parse(String((init as RequestInit).body))).toEqual({ email: 'engineer@example.com', newPassword: 'new-password-1' })
  })

  it('rejects mismatched or short passwords without calling the server', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
    render(<MemoryRouter><ForgotPasswordPage /></MemoryRouter>)

    fill('engineer@example.com', 'new-password-1', 'different-password')
    expect(await screen.findByText('Passwords do not match.')).toBeTruthy()

    fill('engineer@example.com', 'short', 'short')
    expect(await screen.findByText('Password must be at least 8 characters.')).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
