import type { UserRole } from './authApi'
import { apiFetch } from './http'
import type { PagedResponse } from './types'

// Emailing the monthly bill report: rules say who receives which factories and categories (empty lists mean all).
export type ReportRecipient = { userId: string | null; email: string; displayName: string | null; inactive: boolean }
export type ReportDistributionRule = { id: string; name: string; factoryCodes: string[]; factoryLabel: string; categoryCodes: string[]; categoryLabel: string; recipients: ReportRecipient[]; isActive: boolean }
export type SaveReportDistributionRule = { name: string; factoryCodes: string[]; categoryCodes: string[]; userIds: string[]; emails: string[]; isActive: boolean }
export type ReportRecipientOption = { userId: string; displayName: string; email: string; role: UserRole }
export type ReportEmailPreview = { ruleId: string; ruleName: string; factoryLabel: string; categoryLabel: string; recipients: string[]; numberCount: number; canSend: boolean; reason: string | null }
export type ReportEmailStatus = 'Pending' | 'Sent' | 'Failed'
export type ReportEmailDelivery = { id: string; ruleId: string | null; ruleName: string; recipients: string; status: ReportEmailStatus; attempts: number; requestedAt: string; requestedBy: string; sentAt: string | null; lastError: string | null; billBatchId?: string; billingYear?: number; billingMonth?: number }
export type ReportEmailHistoryQuery = { status?: ReportEmailStatus | ''; search?: string; pageNumber: number; pageSize: number }

const base = '/api/report-distribution'

export function getReportDistributionRules() { return apiFetch<ReportDistributionRule[]>(`${base}/rules`) }
export function getReportRecipientOptions() { return apiFetch<ReportRecipientOption[]>(`${base}/recipient-options`) }
export function createReportDistributionRule(rule: SaveReportDistributionRule) { return apiFetch<ReportDistributionRule>(`${base}/rules`, { method: 'POST', body: JSON.stringify(rule) }) }
export function updateReportDistributionRule(id: string, rule: SaveReportDistributionRule) { return apiFetch<ReportDistributionRule>(`${base}/rules/${id}`, { method: 'PUT', body: JSON.stringify(rule) }) }
export function deleteReportDistributionRule(id: string) { return apiFetch<void>(`${base}/rules/${id}`, { method: 'DELETE' }) }

export function previewReportEmails(batchId: string) { return apiFetch<ReportEmailPreview[]>(`${base}/batches/${batchId}/preview`) }
export function sendReportEmails(batchId: string, ruleIds: string[]) { return apiFetch<ReportEmailDelivery[]>(`${base}/batches/${batchId}/send`, { method: 'POST', body: JSON.stringify({ ruleIds }) }) }
export function getReportEmailDeliveries(batchId: string) { return apiFetch<ReportEmailDelivery[]>(`${base}/batches/${batchId}/deliveries`) }
export function resendReportEmail(id: string) { return apiFetch<ReportEmailDelivery>(`${base}/deliveries/${id}/resend`, { method: 'POST' }) }

// Every report email across batches, newest first (Email History).
export function searchReportEmails(query: ReportEmailHistoryQuery) {
  const params = new URLSearchParams({ pageNumber: String(query.pageNumber), pageSize: String(query.pageSize) })
  if (query.status) params.set('status', query.status)
  if (query.search?.trim()) params.set('search', query.search.trim())
  return apiFetch<PagedResponse<ReportEmailDelivery>>(`${base}/deliveries?${params}`)
}
