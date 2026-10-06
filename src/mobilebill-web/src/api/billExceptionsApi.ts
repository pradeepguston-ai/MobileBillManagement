import { apiFetch } from './http'
import type { PagedResponse } from './types'

export const billExceptionTypes = [
  'MOBILE_NOT_FOUND',
  'EMPLOYEE_NOT_ACTIVE',
  'ENTITLEMENT_NOT_FOUND',
  'MULTIPLE_ACTIVE_ALLOCATIONS',
  'MULTIPLE_ENTITLEMENTS',
  'ZERO_BILL',
  'PARSER_WARNING',
  'BILLED_AFTER_DISCONNECTION',
] as const

export type BillExceptionType = typeof billExceptionTypes[number]
export type BillExceptionResolutionFilter = 'All' | 'Unresolved' | 'Resolved'

export type BillExceptionRow = {
  id: string
  billBatchId: string
  billLineId?: string
  mobileNumber?: string
  exceptionType: BillExceptionType
  severity: string
  status: string
  description: string
  resolution?: string
  resolvedBy?: string
  resolvedAt?: string
  allocationMatchMethod?: string
  entitlementMatchMethod?: string
}

export type BillExceptionSummary = {
  billBatchId: string
  totalCount: number
  unresolvedCount: number
  resolvedCount: number
}

export type MobileAccountCandidate = {
  mobileAccountId: string
  employeeId: string
  mobileNumber: string
  employeeEpf: string
  employeeName: string
  isActive: boolean
}

export function getBillExceptions(batchId: string, query: Record<string, string>) {
  return apiFetch<PagedResponse<BillExceptionRow>>(`/api/bill-batches/${batchId}/exceptions?${new URLSearchParams(query)}`)
}

export function getBillExceptionSummary(batchId: string) {
  return apiFetch<BillExceptionSummary>(`/api/bill-batches/${batchId}/exceptions/summary`)
}

export function getMobileAccountCandidates(exceptionId: string) {
  return apiFetch<MobileAccountCandidate[]>(`/api/bill-exceptions/${exceptionId}/mobile-account-candidates`)
}

export function resolveMobileAccountException(exceptionId: string, mobileAccountId: string, comment: string) {
  return apiFetch(`/api/bill-exceptions/${exceptionId}/resolve-mobile-account`, {
    method: 'POST',
    body: JSON.stringify({ mobileAccountId, comment }),
  })
}
