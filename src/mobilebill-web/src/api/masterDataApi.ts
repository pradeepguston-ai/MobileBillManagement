import { apiFetch, apiResponse } from './http'
import type { EntityRow, PagedResponse } from './types'

export type ListOptions = { pageNumber: number; pageSize: number; search: string; isActive?: boolean }

export function listMasterData<T extends EntityRow>(path: string, options: ListOptions) {
  const params = new URLSearchParams({ pageNumber: String(options.pageNumber), pageSize: String(options.pageSize), search: options.search })
  if (options.isActive !== undefined) params.set('isActive', String(options.isActive))
  return apiFetch<PagedResponse<T>>(`${path}?${params}`)
}

export async function listAllMasterData<T extends EntityRow>(path: string, search: string, isActive?: boolean) {
  const firstPage = await listMasterData<T>(path, { pageNumber: 1, pageSize: 100, search, isActive })
  const items = [...firstPage.items]
  for (let pageNumber = 2; pageNumber <= firstPage.totalPages; pageNumber += 1) {
    const nextPage = await listMasterData<T>(path, { pageNumber, pageSize: 100, search, isActive })
    items.push(...nextPage.items)
  }
  return items
}

export function createMasterData<T extends EntityRow>(path: string, body: unknown) { return apiFetch<T>(path, { method: 'POST', body: JSON.stringify(body) }) }
export function updateMasterData<T extends EntityRow>(path: string, id: string, body: unknown) { return apiFetch<T>(`${path}/${id}`, { method: 'PUT', body: JSON.stringify(body) }) }
// Moves an active mobile allocation to another employee; the number, credit limit and rental are unchanged.
// SIM Pool. Dates are yyyy-MM-dd.
export type SimStatus = 'Assigned' | 'Pooled' | 'Disconnected'
export type SimPoolItem = { id: string; mobileNumber: string; previousEpf: string; previousEmployeeName: string; factory: string; department: string; pooledOn: string; reason: string | null; daysInPool: number; isLongIdle: boolean; monthlyCreditLimit: number; monthlyRental: number }
export function getSimPool() { return apiFetch<SimPoolItem[]>('/api/mobile-accounts/pool') }
export function releaseToPool(id: string, resignedOn: string, reason: string) { return apiFetch<EntityRow>(`/api/mobile-accounts/${id}/release-to-pool`, { method: 'POST', body: JSON.stringify({ resignedOn, reason: reason || null }) }) }
// Leave the amounts out to keep the pooled SIM's credit limit and rental (HR and Finance users must).
export function applyPackageRental(id: string) { return apiFetch<number>(`/api/mobile-packages/${id}/apply-rental`, { method: 'POST' }) }
// amounts (and a package) may be sent only by Administrator and IT Engineer.
export function assignFromPool(id: string, employeeId: string, amounts?: { monthlyCreditLimit: number; monthlyRental: number; packageId?: string }) { return apiFetch<EntityRow>(`/api/mobile-accounts/${id}/assign-from-pool`, { method: 'POST', body: JSON.stringify({ employeeId, ...amounts }) }) }
export function disconnectSim(id: string, disconnectedOn: string, reason: string) { return apiFetch<EntityRow>(`/api/mobile-accounts/${id}/disconnect`, { method: 'POST', body: JSON.stringify({ disconnectedOn, reason: reason || null }) }) }
export type ResignationStatus = 'Pending' | 'Resigned'
export type EmployeeResignation = { id: string; epf: string; fullName: string; callingName: string | null; factory: string; department: string; section: string | null; resignedOn: string; reason: string | null; daysLeft: number; mobileNumbers: string[]; devices?: string[] | null }
export function getResignations(status: ResignationStatus) { return apiFetch<EmployeeResignation[]>(`/api/employees/resignations?status=${status}`) }
export function cancelResignation(id: string) { return apiFetch<void>(`/api/employees/${id}/cancel-resignation`, { method: 'POST' }) }
export function resignEmployee(id: string, resignedOn: string, reason: string) { return apiFetch<EntityRow[]>(`/api/employees/${id}/resign`, { method: 'POST', body: JSON.stringify({ resignedOn, reason: reason || null }) }) }

// Excel import. Checking (commit = false) validates every row and saves nothing; importing (commit = true) saves
// the whole file only if every row is valid. Row numbers are the Excel row numbers.
export type ImportRowError = { row: number; message: string }
export type ImportResult = { totalRows: number; newCount: number; updatedCount: number; unchangedCount: number; errors: ImportRowError[]; committed: boolean }
export function importMasterData(path: string, file: File, commit: boolean) {
  const body = new FormData()
  body.append('file', file)
  return apiFetch<ImportResult>(`${path}/import?commit=${commit}`, { method: 'POST', body })
}
export async function downloadImportTemplate(path: string) {
  const response = await apiResponse(`${path}/import/template`)
  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const fileName = disposition.match(/filename="?([^";]+)"?/i)?.[1] ?? 'Import_Template.xlsx'
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export function deactivateMasterData(path: string, id: string) { return apiFetch<void>(`${path}/${id}/deactivate`, { method: 'POST' }) }
