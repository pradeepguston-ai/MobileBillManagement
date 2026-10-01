import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { DeactivateConfirmationDialog } from './DeactivateConfirmationDialog'

describe('DeactivateConfirmationDialog', () => {
  it('requires an explicit confirmation before invoking deactivation', () => {
    const confirm = vi.fn()
    render(<DeactivateConfirmationDialog open title="Employee" onCancel={() => undefined} onConfirm={confirm} />)

    expect(confirm).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Deactivate' }))
    expect(confirm).toHaveBeenCalledOnce()
  })
})
