import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import { DonutChart } from './DonutChart'

const data = [{ label: 'Deducted from employees', value: 75, color: '#7c3aed' }, { label: 'By Company', value: 25, color: '#f59e0b' }, { label: 'Credit', value: -5, color: '#000' }]

describe('DonutChart', () => {
  afterEach(cleanup)

  it('shows the total in the centre and each positive segment with its share in the legend', () => {
    render(<DonutChart ariaLabel="Split" centerLabel="LKR total" data={data} />)

    const chart = screen.getByRole('img', { name: 'Split' })
    expect(chart.textContent).toContain('100LKR total')
    expect(chart.querySelectorAll('path')).toHaveLength(2)
    expect(screen.getAllByRole('listitem').map(item => item.textContent)).toEqual(['Deducted from employees75 (75%)', 'By Company25 (25%)'])
  })

  it('shows a segment\'s share in the centre while its legend row is hovered', () => {
    render(<DonutChart ariaLabel="Split" centerLabel="LKR total" data={data} />)

    const row = screen.getAllByRole('listitem')[1]
    fireEvent.mouseEnter(row)
    expect(screen.getByRole('img', { name: 'Split' }).textContent).toContain('25%By Company')

    fireEvent.mouseLeave(row)
    expect(screen.getByRole('img', { name: 'Split' }).textContent).toContain('100LKR total')
  })

  it('draws a single segment as a full ring and says when there is nothing to show', () => {
    const { rerender } = render(<DonutChart ariaLabel="Split" centerLabel="total" data={[data[0]]} />)
    expect(screen.getByRole('img', { name: 'Split' }).querySelector('circle')).toBeTruthy()

    rerender(<DonutChart ariaLabel="Split" centerLabel="total" data={[]} />)
    expect(screen.getByText('No data to show yet.')).toBeTruthy()
  })
})
