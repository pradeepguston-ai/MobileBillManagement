import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ImportDialog } from './ImportDialog'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const chooseFile = () => fireEvent.change(screen.getByLabelText('Excel file'), { target: { files: [new File(['x'], 'employees.xlsx')] } })

describe('ImportDialog', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('lists problems with row numbers and does not offer to import', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ totalRows: 3, newCount: 1, updatedCount: 0, unchangedCount: 0, committed: false, errors: [{ row: 3, message: "Factory Code 'NOPE' is not an active code." }, { row: 4, message: 'Factory F1 + EPF 200 also appears on row 2.' }] }))
    render(<ImportDialog open title="Import employees from Excel" path="/api/employees" onClose={() => {}} onImported={() => {}} />)

    chooseFile()
    fireEvent.click(screen.getByRole('button', { name: 'Check file' }))

    expect(await screen.findByText(/2 problems found in 3 rows/)).toBeTruthy()
    expect(screen.getByText("Factory Code 'NOPE' is not an active code.")).toBeTruthy()
    expect(screen.getByRole('cell', { name: '4' })).toBeTruthy()
    expect((screen.getByRole('button', { name: /^Import/ }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('checks first, then imports the clean file and reports the counts', async () => {
    const calls: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      calls.push(url)
      const committed = url.includes('commit=true')
      return json({ totalRows: 3, newCount: 2, updatedCount: 1, unchangedCount: 0, committed, errors: [] })
    })
    const onImported = vi.fn()
    render(<ImportDialog open title="Import employees from Excel" path="/api/employees" onClose={() => {}} onImported={onImported} />)

    chooseFile()
    fireEvent.click(screen.getByRole('button', { name: 'Check file' }))
    expect(await screen.findByText(/3 rows checked with no problems: 2 new, 1 to update/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Import 3 rows' }))

    expect(await screen.findByText('Imported 3 rows: 2 new, 1 updated, 0 unchanged.')).toBeTruthy()
    expect(onImported).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(calls).toEqual([expect.stringContaining('/api/employees/import?commit=false'), expect.stringContaining('/api/employees/import?commit=true')]))
  })
})
