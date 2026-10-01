import { apiFetch } from './http'
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
export function deactivateMasterData(path: string, id: string) { return apiFetch<void>(`${path}/${id}/deactivate`, { method: 'POST' }) }
