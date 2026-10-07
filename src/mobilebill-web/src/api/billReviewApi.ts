import { apiFetch } from './http'
import type { PagedResponse } from './types'

export type ApprovalHistoryItem = { id: string; workflowStage: string; action: string; workflowRole: string; userId: string; displayName: string; timestamp: string; comment?: string; previousStatus: string; newStatus: string; userRole?: string | null }
export type ReviewSummary = { batchId: string; billingYear: number; billingMonth: number; provider: string; corporateCode: string; batchStatus: string; validationLevel: string; totalAccounts: number; totalActualBill: number; totalCalculatedExcess: number; totalFinalDeduction: number; companyResponsibilityAmount: number; exceptionCount: number; unmatchedCount: number; assessedCount: number; unassessedCount: number; unresolvedExceptionCount?: number; approvalHistory: ApprovalHistoryItem[] }
export type ReviewRow = { id: string; mobileNumber: string; employeeEpf: string; employeeName: string; callingName?: string; category: string; designation: string; factory: string; department: string; section?: string | null; subSection?: string | null; creditLimit: number; monthlyRental: number; availableEntitlement: number; actualBill: number; variance: number; calculatedExcess: number; responsibility?: string; finalDeduction: number; remark?: string; status: string; hasException: boolean; isAssessed: boolean; packageCode?: string | null }
export type ChargeBreakdown = { previousDue: number; payments: number; totalUsage: number; idd: number; roaming: number; vas: number; discounts: number; billAdjustments: number; commitmentCharges: number; latePaymentCharges: number; addToBill: number; instalmentPlans: number; governmentTaxesLevies: number; vat: number; chargesForBillPeriod: number; totalDueAmount: number }
export type ReviewDetail = { row: ReviewRow; charges: ChargeBreakdown; exceptions: string[]; approvalHistory: string[]; auditHistory: string[]; assessedBy?: string; assessedAt?: string; deductionOverrideReason?: string }
export type AssessmentRequest = { responsibility: 'ByUser' | 'ByCompany'; finalDeduction: number; reason: string | null }
export type BulkAssessmentItemResult = { monthlyBillId: string; success: boolean; error: string | null }
export type BulkAssessmentResult = { successCount: number; failureCount: number; items: BulkAssessmentItemResult[] }
export type BulkAssessmentRequest = { monthlyBillIds: string[]; responsibility: 'ByUser' | 'ByCompany'; reason: string | null }
export async function getReviewSummary(batchId: string) { return apiFetch<ReviewSummary>(`/api/bill-batches/${batchId}/review`) }
export async function getReviewRows(batchId: string, query: Record<string, string>) { return apiFetch<PagedResponse<ReviewRow>>(`/api/bill-batches/${batchId}/review/rows?${new URLSearchParams(query)}`) }
// Every row matching the query, fetched 100 at a time (the server's page limit) and returned as a single page.
export async function getAllReviewRows(batchId: string, query: Record<string, string>): Promise<PagedResponse<ReviewRow>> {
  const first = await getReviewRows(batchId, { ...query, pageNumber: '1', pageSize: '100' })
  const items = [...first.items]
  for (let pageNumber = 2; pageNumber <= first.totalPages; pageNumber += 1) {
    const next = await getReviewRows(batchId, { ...query, pageNumber: String(pageNumber), pageSize: '100' })
    items.push(...next.items)
  }
  return { items, pageNumber: 1, pageSize: items.length, totalCount: first.totalCount, totalPages: 1 }
}
export async function getReviewDetail(batchId: string, rowId: string) { return apiFetch<ReviewDetail>(`/api/bill-batches/${batchId}/review/rows/${rowId}`) }
export type BillTrendScope = 'ThisNumber' | 'Employee' | 'AllHolders'
export type BillTrendPoint = { billingYear: number; billingMonth: number; actualBill: number; entitlement: number; calculatedExcess: number; finalDeduction: number; vas: number; responsibility: string | null; isOverLimit: boolean; isPreliminary: boolean; isCurrent: boolean; numbers: number; holderEpf: string; holderName: string; isOtherHolder: boolean }
export type BillTrend = { monthlyBillId: string; mobileNumber: string; employeeEpf: string; employeeName: string; scope: BillTrendScope; months: number; points: BillTrendPoint[]; currentActualBill: number; average: number | null; changePercent: number | null; isAboveUsual: boolean; monthsOverLimit: number; highestYear: number | null; highestMonth: number | null; highestActualBill: number | null; aboveUsualThresholdPercent: number }
export async function getBillTrend(batchId: string, rowId: string, scope: BillTrendScope, months: number) { return apiFetch<BillTrend>(`/api/bill-batches/${batchId}/review/rows/${rowId}/trend?${new URLSearchParams({ scope, months: String(months) })}`) }
export async function assessMonthlyBill(monthlyBillId: string, request: AssessmentRequest) { return apiFetch(`/api/monthly-bills/${monthlyBillId}/assessment`, { method: 'PUT', body: JSON.stringify(request) }) }
export async function bulkAssessMonthlyBills(request: BulkAssessmentRequest) { return apiFetch<BulkAssessmentResult>('/api/monthly-bills/bulk-assessment', { method: 'POST', body: JSON.stringify(request) }) }
