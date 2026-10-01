import { afterEach, describe, expect, it, vi } from 'vitest'

import { createBatch, downloadExcelReport, getBatches, getBillLines, parseBill, uploadBill, validateBill } from './billingApi'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('billingApi', () => {
  afterEach(() => vi.restoreAllMocks())

  it('sends only the approved create-batch business fields', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json({ id: 'batch-1' }))

    await createBatch({ providerId: 'provider-1', corporateCode: 'CORP', billingMonth: 8, billingYear: 2026 })

    const [, request] = fetchMock.mock.calls[0]
    expect(request?.method).toBe('POST')
    expect(JSON.parse(String(request?.body))).toEqual({ providerId: 'provider-1', corporateCode: 'CORP', billingMonth: 8, billingYear: 2026 })
  })

  it('uses multipart upload and the separate parse and validate actions', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json({ id: 'batch-1' }))
    const file = new File(['%PDF-1.7'], 'bill.pdf', { type: 'application/pdf' })

    await uploadBill('batch-1', file)
    await parseBill('batch-1')
    await validateBill('batch-1')

    const uploadRequest = fetchMock.mock.calls[0][1]
    expect(uploadRequest).toBeDefined()
    expect(uploadRequest?.body).toBeInstanceOf(FormData)
    expect((uploadRequest!.headers as Headers).has('Content-Type')).toBe(false)
    expect(fetchMock.mock.calls.map(([url]) => String(url))).toEqual(expect.arrayContaining([
      expect.stringContaining('/api/bill-batches/batch-1/upload'),
      expect.stringContaining('/api/bill-batches/batch-1/parse'),
      expect.stringContaining('/api/bill-batches/batch-1/validate'),
    ]))
  })

  it('sends server paging, sorting, and trimmed mobile search', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))

    await getBatches({ page: 2, pageSize: 20, sortBy: 'status', sortDirection: 'asc' })
    await getBillLines('batch-1', 3, 20, '  768791861  ')

    expect(String(fetchMock.mock.calls[0][0])).toContain('page=2&pageSize=20&sortBy=status&sortDirection=asc')
    expect(String(fetchMock.mock.calls[1][0])).toContain('pageNumber=3&pageSize=20&search=768791861')
  })

  it('downloads the Excel response using the server-provided filename', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(new TextEncoder().encode('workbook'), { status: 200, headers: { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'Content-Disposition': 'attachment; filename="Mobile_Bill_August_2026.xlsx"' } }))
    const createUrl = vi.fn(() => 'blob:report')
    const revokeUrl = vi.fn()
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: createUrl })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revokeUrl })
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)

    await downloadExcelReport('batch-1')

    expect(click).toHaveBeenCalledOnce()
    expect((click.mock.instances[0] as HTMLAnchorElement).download).toBe('Mobile_Bill_August_2026.xlsx')
    expect(revokeUrl).toHaveBeenCalledWith('blob:report')
  })

  it('surfaces report errors before creating a browser file', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ title: 'Report unavailable', detail: 'The batch must be completed before export.' }), { status: 409, headers: { 'Content-Type': 'application/problem+json' } }))
    const createUrl = vi.fn()
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: createUrl })

    await expect(downloadExcelReport('batch-1')).rejects.toThrow('The batch must be completed before export.')
    expect(createUrl).not.toHaveBeenCalled()
  })
})
