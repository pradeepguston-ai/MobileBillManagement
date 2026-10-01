import { apiFetch } from './http'

export type WorkflowAction = 'Approve' | 'Reject' | 'ReturnForCorrection'
export type WorkflowCapabilities = { canSubmit: boolean; canApprove: boolean; canReject: boolean; canReturnForCorrection: boolean; canLock: boolean; currentStage: string; status: string }

export function getWorkflowCapabilities(batchId: string) {
  return apiFetch<WorkflowCapabilities>(`/api/bill-batches/${batchId}/workflow-capabilities`)
}

export function submitForItReview(batchId: string, comment?: string) {
  return apiFetch(`/api/bill-batches/${batchId}/workflow/submit`, { method: 'POST', body: JSON.stringify({ comment: comment ?? null }) })
}

export function decideWorkflow(batchId: string, action: WorkflowAction, comment?: string) {
  return apiFetch(`/api/bill-batches/${batchId}/workflow/decision`, { method: 'POST', body: JSON.stringify({ action, comment: comment ?? null }) })
}

export function lockBatch(batchId: string) {
  return apiFetch(`/api/bill-batches/${batchId}/lock`, { method: 'POST' })
}
