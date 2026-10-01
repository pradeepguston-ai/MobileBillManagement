import type { BillBatchStatus } from '../api/billingApi'

const reviewStatuses = new Set<BillBatchStatus>(['Validated', 'ITReview', 'HRApproval', 'FinanceApproval', 'Completed', 'Locked'])

export function batchContinuationRoute(batchId: string, status: BillBatchStatus) {
  return reviewStatuses.has(status) ? `/billing/${batchId}/review` : `/billing/${batchId}/process`
}

export function canDownloadReport(status: BillBatchStatus) {
  return status === 'Completed' || status === 'Locked'
}

export function exceptionDownloadWarning(unresolvedCount: number) {
  return `This batch still has ${unresolvedCount} unresolved exception${unresolvedCount === 1 ? '' : 's'}. The Excel report will include those bill lines with the employee, allocation and deduction columns left blank. Download anyway?`
}

export function billingPeriod(year: number, month: number) {
  return new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1))
}
