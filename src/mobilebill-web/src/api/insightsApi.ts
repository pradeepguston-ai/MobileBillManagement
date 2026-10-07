import { apiFetch } from './http'

export type InsightsBatchOption = { id: string; billingYear: number; billingMonth: number; provider: string; status: string; isApproved: boolean }
export type InsightsKpis = {
  totalActualBill: number
  totalEntitlement: number
  totalCalculatedExcess: number
  deductedFromEmployees: number
  borneByCompany: number
  unassessedExcess: number
  totalRoaming: number
  accounts: number
  overLimitAccounts: number
}
export type InsightsAmount = { key: string; label: string; amount: number }
export type InsightsGroupRow = { code: string; name: string; accounts: number; actualBill: number; entitlement: number; calculatedExcess: number }
export type InsightsTopAccount = { mobileNumber: string; epf: string; employeeName: string; callingName: string | null; factory: string; department: string; actualBill: number; entitlement: number; calculatedExcess: number; responsibility: 'ByUser' | 'ByCompany' | null; remark: string | null }
export type InsightsTrendPoint = { batchId: string; billingYear: number; billingMonth: number; isApproved: boolean; totalActualBill: number; totalCalculatedExcess: number; deductedFromEmployees: number; borneByCompany: number; accounts: number; waivedForEmployees: number; unassessedExcess: number }
export type InsightsBand = { key: string; label: string; accounts: number; calculatedExcess: number }
export type InsightsRepeatAccount = { mobileNumber: string; epf: string; employeeName: string; factory: string; packageCode: string | null; monthsOverLimit: number; monthsBilled: number; totalExcess: number; averageExcess: number; selectedBatchExcess: number }
export type InsightsRepeatOverLimit = { windowMonths: number; minMonthsOver: number; totalCount: number; accounts: InsightsRepeatAccount[] }
export type InsightsCount = { key: string; label: string; count: number }
export type InsightsAssets = { simTypes: InsightsCount[]; deviceStatuses: InsightsCount[]; devices: number; devicesWithEmployeesValue: number }
export type BillingInsights = {
  batches: InsightsBatchOption[]
  selected: InsightsBatchOption | null
  current: InsightsKpis | null
  previous: InsightsKpis | null
  previousBatch: InsightsBatchOption | null
  chargeMix: InsightsAmount[]
  excessSplit: InsightsAmount[]
  groups: { factory: InsightsGroupRow[]; department: InsightsGroupRow[]; category: InsightsGroupRow[] }
  topOverLimit: InsightsTopAccount[]
  trend: InsightsTrendPoint[]
  billRanges: InsightsBand[]
  repeatOverLimit: InsightsRepeatOverLimit
  assets: InsightsAssets
}

// batchId omitted: the latest matched batch. months: how many approved batches the trend covers.
export function getBillingInsights(batchId?: string, months = 6) {
  const params = new URLSearchParams({ months: String(months) })
  if (batchId) params.set('batchId', batchId)
  return apiFetch<BillingInsights>(`/api/insights?${params}`)
}
