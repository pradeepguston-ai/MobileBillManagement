import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { MasterDataTable } from './MasterDataTable'

describe('MasterDataTable', () => {
  it('renders configured columns and row values', () => {
    render(
      <MasterDataTable
        columns={[{ key: 'name', header: 'Name' }, { key: 'isActive', header: 'Status' }]}
        rows={[{ id: '1', name: 'Head Office', isActive: true }]}
        onEdit={() => undefined}
        onDeactivate={() => undefined}
      />,
    )

    expect(screen.getByRole('columnheader', { name: 'Name' })).toBeTruthy()
    expect(screen.getByText('Head Office')).toBeTruthy()
    expect(screen.getByText('Active')).toBeTruthy()
  })
})
