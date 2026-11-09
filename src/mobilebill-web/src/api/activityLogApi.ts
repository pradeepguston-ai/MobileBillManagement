import { apiFetch } from './http'
import type { PagedResponse } from './types'

// What every user did in the system, newest first. Every signed-in user can read it.
export type ActivityEntry = { id: string; occurredAt: string; userId: string; userName: string; role: string; area: string; description: string }
export type ActivityFilters = { users: Array<{ userId: string; userName: string }>; areas: string[] }
export type ActivityQuery = { userId?: string; area?: string; from?: string; to?: string; search?: string; pageNumber: number; pageSize: number }

export function getActivityLog(query: ActivityQuery) {
  const params = new URLSearchParams({ pageNumber: String(query.pageNumber), pageSize: String(query.pageSize) })
  for (const key of ['userId', 'area', 'from', 'to', 'search'] as const) { const value = query[key]?.trim(); if (value) params.set(key, value) }
  return apiFetch<PagedResponse<ActivityEntry>>(`/api/activity-log?${params}`)
}

export function getActivityFilters() { return apiFetch<ActivityFilters>('/api/activity-log/filters') }
