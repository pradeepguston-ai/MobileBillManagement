import { apiFetch, apiResponse } from './http'
import type { EntityRow } from './types'

// Company mobile devices (Masters › Mobile Devices).
export type DeviceStatus = 'InStock' | 'Issued' | 'ReturnPending' | 'UnderRepair' | 'Damaged' | 'Lost' | 'Retired'
export type DeviceReturnReason = 'Resigned' | 'Replaced' | 'Upgrade' | 'Lost' | 'Other'
export type DeviceToCollect = {
  deviceId: string; assetTag: string; brand: string; model: string; imei1: string
  employeeId: string; epf: string; employeeName: string; factory: string; department: string; resignedOn: string | null
  returnPendingSince: string; daysWaiting: number; isOverdue: boolean; purchaseCost: number; recoverableAmount: number
}

export const deviceStatusLabels: Record<DeviceStatus, string> = {
  InStock: 'In Stock', Issued: 'Issued', ReturnPending: 'Return Pending', UnderRepair: 'Under Repair', Damaged: 'Damaged', Lost: 'Lost', Retired: 'Retired',
}
export const deviceReasonLabels: Record<DeviceReturnReason, string> = { Resigned: 'Resigned', Replaced: 'Replaced', Upgrade: 'Upgrade', Lost: 'Lost', Other: 'Other' }

const post = (path: string, body: unknown) => apiFetch<EntityRow>(path, { method: 'POST', body: JSON.stringify(body) })
const orNull = (text: string | undefined) => text?.trim() ? text.trim() : null

export function issueDevice(id: string, employeeId: string, issuedOn: string, notes?: string) { return post(`/api/mobile-devices/${id}/issue`, { employeeId, issuedOn, notes: orNull(notes) }) }
export function returnDevice(id: string, returnedOn: string, condition: string, reason: string, notes: string | undefined, chargeEmployee: boolean) { return post(`/api/mobile-devices/${id}/return`, { returnedOn, condition, reason, notes: orNull(notes), chargeEmployee }) }
export function replaceDevice(id: string, newDeviceId: string, replacedOn: string, oldCondition: string, notes: string | undefined, chargeEmployee: boolean) { return post(`/api/mobile-devices/${id}/replace`, { newDeviceId, replacedOn, oldCondition, notes: orNull(notes), chargeEmployee }) }
export function markDeviceLost(id: string, lostOn: string, notes: string | undefined, chargeEmployee: boolean) { return post(`/api/mobile-devices/${id}/lost`, { lostOn, notes: orNull(notes), chargeEmployee }) }
export function markDeviceRepaired(id: string, on: string, notes?: string) { return post(`/api/mobile-devices/${id}/repaired`, { on, notes: orNull(notes) }) }
export function retireDevice(id: string, on: string, notes?: string) { return post(`/api/mobile-devices/${id}/retire`, { on, notes: orNull(notes) }) }
export function getDevicesToCollect() { return apiFetch<DeviceToCollect[]>('/api/mobile-devices/to-collect') }

export async function downloadDeviceRegister(format: 'excel' | 'pdf', filters: { factoryCode?: string; departmentCode?: string }) {
  const params = new URLSearchParams()
  if (filters.factoryCode) params.set('factoryCode', filters.factoryCode)
  if (filters.departmentCode) params.set('departmentCode', filters.departmentCode)
  const query = params.toString()
  const response = await apiResponse(`/api/mobile-devices/register/${format}${query ? `?${query}` : ''}`)
  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const fileName = disposition.match(/filename="?([^";]+)"?/i)?.[1] ?? `Mobile_Device_Register.${format === 'pdf' ? 'pdf' : 'xlsx'}`
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}
