import { apiFetch } from './http'
import type { PagedResponse } from './types'

export type ApprovalHistoryItem = { id: string; workflowStage: string; action: string; workflowRole: string; userId: string; displayName: string; timestamp: string; comment?: string; previousStatus: string; newStatus: string }
export type ReviewSummary = { batchId: string; billingYear: number; billingMonth: number; provider: string; corporateCode: string; batchStatus: string; validationLevel: string; totalAccounts: number; totalActualBill: number; totalCalculatedExcess: number; totalFinalDeduction: number; companyResponsibilityAmount: number; exceptionCount: number; unmatchedCount: number; assessedCount: number; unassessedCount: number; approvalHistory: ApprovalHistoryItem[] }
export type ReviewRow = { id: string; mobileNumber: string; employeeEpf: string; employeeName: string; callingName?: string; category: string; designation: string; factory: string; department: string; creditLimit: number; monthlyRental: number; availableEntitlement: number; actualBill: number; variance: number; calculatedExcess: number; responsibility?: string; finalDeduction: number; remark?: string; status: string; hasException: boolean; isAssessed: boolean }
export type ChargeBreakdown = { previousDue: number; payments: number; totalUsage: number; idd: number; roaming: number; vas: number; discounts: number; billAdjustments: number; commitmentCharges: number; latePaymentCharges: number; addToBill: number; instalmentPlans: number; governmentTaxesLevies: number; vat: number; chargesForBillPeriod: number; totalDueAmount: number }
export type ReviewDetail = { row: ReviewRow; charges: ChargeBreakdown; exceptions: string[]; approvalHistory: string[]; auditHistory: string[]; assessedBy?: string; assessedAt?: string; deductionOverrideReason?: string }
export type AssessmentRequest = { responsibility: 'ByUser' | 'ByCompany'; finalDeduction: number; reason: string | null }
export type BulkAssessmentItemResult = { monthlyBillId: string; success: boolean; error: string | null }
export type BulkAssessmentResult = { successCount: number; failureCount: number; items: BulkAssessmentItemResult[] }
export type BulkAssessmentRequest = { monthlyBillIds: string[]; responsibility: 'ByUser' | 'ByCompany'; reason: string | null }
export async function getReviewSummary(batchId: string) { return apiFetch<ReviewSummary>(`/api/bill-batches/${batchId}/review`) }
export async function getReviewRows(batchId: string, query: Record<string, string>) { return apiFetch<PagedResponse<ReviewRow>>(`/api/bill-batches/${batchId}/review/rows?${new URLSearchParams(query)}`) }
export async function getReviewDetail(batchId: string, rowId: string) { return apiFetch<ReviewDetail>(`/api/bill-batches/${batchId}/review/rows/${rowId}`) }
export async function assessMonthlyBill(monthlyBillId: string, request: AssessmentRequest) { return apiFetch(`/api/monthly-bills/${monthlyBillId}/assessment`, { method: 'PUT', body: JSON.stringify(request) }) }
export async function bulkAssessMonthlyBills(request: BulkAssessmentRequest) { return apiFetch<BulkAssessmentResult>('/api/monthly-bills/bulk-assessment', { method: 'POST', body: JSON.stringify(request) }) }
