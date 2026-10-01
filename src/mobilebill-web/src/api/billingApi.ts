import { apiFetch, apiResponse } from './http'
import type { PagedResponse } from './types'

export type BillBatchStatus = 'Draft' | 'Uploaded' | 'Parsed' | 'ValidationFailed' | 'Validated' | 'ITReview' | 'HRApproval' | 'FinanceApproval' | 'Completed' | 'Locked' | string
export type ValidationLevel = 'None' | 'StructuralOnly' | 'IndependentTotal' | string

export type BillBatchListItem = {
  id: string
  providerId: string
  provider: string
  corporateCode: string
  billingYear: number
  billingMonth: number
  calculatedGrandTotal: number | null
  validationLevel: ValidationLevel
  status: BillBatchStatus
  originalFileName: string | null
  uploadedBy: string | null
  uploadedAt: string | null
}

export type BillBatch = {
  id: string
  providerId: string
  providerName: string | null
  corporateCode: string
  billingYear: number
  billingMonth: number
  status: BillBatchStatus
  originalFileName: string | null
  fileHash: string | null
  uploadedBy: string | null
  uploadedAt: string | null
  statedGrandTotal: number | null
  calculatedGrandTotal: number | null
  difference: number | null
  grandTotalSource: string
  validationLevel: ValidationLevel
  validationWarning: string | null
  totalCandidates: number
  successfulCount: number
  failedCount: number
  warnings: string[] | null
}

export type BillLine = {
  id: string
  mobileNumber: string
  pageNumber: number
  extractionStatus: string
  extractionError: string | null
  previousDueAmount: number
  payments: number
  totalUsageCharges: number
  idd: number
  roaming: number
  valueAddedServices: number
  discounts: number
  billAdjustmentsBalanceTransfers: number
  commitmentCharges: number
  latePaymentCharges: number
  addToBill: number
  instalmentPlans: number
  governmentTaxesAndLevies: number
  vat: number
  chargesForBillPeriod: number
  totalDueAmount: number
  rawText: string
}

export type BatchListOptions = { page: number; pageSize: number; sortBy?: string; sortDirection?: 'asc' | 'desc' }
export type CreateBillBatchInput = { providerId: string; corporateCode: string; billingYear: number; billingMonth: number }

export function getBatches(options: BatchListOptions) {
  const params = new URLSearchParams({ page: String(options.page), pageSize: String(options.pageSize) })
  if (options.sortBy) params.set('sortBy', options.sortBy)
  if (options.sortDirection) params.set('sortDirection', options.sortDirection)
  return apiFetch<PagedResponse<BillBatchListItem>>(`/api/bill-batches?${params}`)
}

export function createBatch(input: CreateBillBatchInput) {
  return apiFetch<BillBatch>('/api/bill-batches', { method: 'POST', body: JSON.stringify(input) })
}

export function getBatch(batchId: string) {
  return apiFetch<BillBatch>(`/api/bill-batches/${batchId}`)
}

export function uploadBill(batchId: string, file: File) {
  const body = new FormData()
  body.append('file', file)
  return apiFetch<BillBatch>(`/api/bill-batches/${batchId}/upload`, { method: 'POST', body })
}

export function parseBill(batchId: string) {
  return apiFetch<BillBatch>(`/api/bill-batches/${batchId}/parse`, { method: 'POST' })
}

export function validateBill(batchId: string) {
  return apiFetch<BillBatch>(`/api/bill-batches/${batchId}/validate`, { method: 'POST' })
}

export type BillMatchingResult = { billBatchId: string; monthlyBillsCreated: number; exceptionsCreated: number }

export function matchBillBatch(batchId: string) {
  return apiFetch<BillMatchingResult>(`/api/bill-batches/${batchId}/match`, { method: 'POST' })
}

export function getBillLines(batchId: string, page: number, pageSize: number, search: string) {
  const params = new URLSearchParams({ pageNumber: String(page), pageSize: String(pageSize) })
  if (search.trim()) params.set('search', search.trim())
  return apiFetch<PagedResponse<BillLine>>(`/api/bill-batches/${batchId}/lines?${params}`)
}

// Every extracted line matching the search, fetched 100 at a time (the server's page limit).
export async function getAllBillLines(batchId: string, search: string) {
  const first = await getBillLines(batchId, 1, 100, search)
  const items = [...first.items]
  for (let page = 2; page <= first.totalPages; page += 1) items.push(...(await getBillLines(batchId, page, 100, search)).items)
  return items
}

export type ReportFormat = 'excel' | 'pdf'

export type ReportFilters = { factoryCodes?: string[]; categoryCodes?: string[] }

async function downloadReport(batchId: string, format: ReportFormat, filters: ReportFilters = {}) {
  const params = new URLSearchParams()
  for (const code of filters.factoryCodes ?? []) params.append('factoryCode', code)
  for (const code of filters.categoryCodes ?? []) params.append('categoryCode', code)
  const query = params.toString() ? `?${params}` : ''
  const response = await apiResponse(`/api/reports/billing/${batchId}/${format}${query}`)
  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  const plain = disposition.match(/filename="?([^";]+)"?/i)?.[1]
  const fileName = encoded ? decodeURIComponent(encoded) : plain ?? `mobile-bill-${batchId}.${format === 'pdf' ? 'pdf' : 'xlsx'}`
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export const downloadExcelReport = (batchId: string) => downloadReport(batchId, 'excel')
// Leave both filters empty for the full batch report.
export const downloadReportAs = (batchId: string, format: ReportFormat, filters?: ReportFilters) => downloadReport(batchId, format, filters)

// All extracted lines of a batch as an Excel file; search keeps only matching mobile numbers, like the screen.
export async function downloadBillLinesExcel(batchId: string, search = '') {
  const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : ''
  const response = await apiResponse(`/api/bill-batches/${batchId}/lines/excel${query}`)
  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  const plain = disposition.match(/filename="?([^";]+)"?/i)?.[1]
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = encoded ? decodeURIComponent(encoded) : plain ?? `extracted-bill-lines-${batchId}.xlsx`
  anchor.click()
  URL.revokeObjectURL(url)
}
