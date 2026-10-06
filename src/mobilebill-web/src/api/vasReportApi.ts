import { apiFetch, apiResponse } from './http'

// Value Added Services report (see Reports › VAS Report).
export type VasBatch = { batchId: string; billingYear: number; billingMonth: number; provider: string; batchStatus: string; isPreliminary: boolean }
export type VasRow = {
  mobileNumber: string; epf: string | null; employeeName: string | null; callingName: string | null
  factory: string | null; department: string | null; section: string | null; category: string | null
  vas: number; actualBill: number; vasShareOfBill: number; responsibility: 'ByUser' | 'ByCompany' | null; finalDeduction: number | null
  monthsWithVas: number; monthsConsidered: number; isRepeat: boolean; isPooled: boolean; isMatched: boolean
}
export type VasGroup = { name: string; users: number; totalVas: number }
export type VasReport = {
  batchId: string; billingYear: number; billingMonth: number; provider: string; batchStatus: string; isPreliminary: boolean
  users: number; totalVas: number; repeatUsers: number; previousUsers: number | null; previousTotalVas: number | null
  byFactory: VasGroup[]; byDepartment: VasGroup[]; rows: VasRow[]
}
export type VasFilters = { factoryCodes?: string[]; categoryCodes?: string[]; sectionCodes?: string[]; minimumVas?: number; repeatOnly?: boolean }

function query(filters: VasFilters) {
  const params = new URLSearchParams()
  for (const code of filters.factoryCodes ?? []) params.append('factoryCode', code)
  for (const code of filters.categoryCodes ?? []) params.append('categoryCode', code)
  for (const code of filters.sectionCodes ?? []) params.append('sectionCode', code)
  if (filters.minimumVas !== undefined) params.set('minimumVas', String(filters.minimumVas))
  if (filters.repeatOnly) params.set('repeatOnly', 'true')
  const text = params.toString()
  return text ? `?${text}` : ''
}

export function getVasBatches() { return apiFetch<VasBatch[]>('/api/reports/vas/batches') }
export function getVasReport(batchId: string, filters: VasFilters) { return apiFetch<VasReport>(`/api/reports/vas/${batchId}${query(filters)}`) }

export async function downloadVasReport(batchId: string, format: 'excel' | 'pdf', filters: VasFilters) {
  const response = await apiResponse(`/api/reports/vas/${batchId}/${format}${query(filters)}`)
  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const fileName = disposition.match(/filename="?([^";]+)"?/i)?.[1] ?? `VAS_Report.${format === 'pdf' ? 'pdf' : 'xlsx'}`
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}
