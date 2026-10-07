import { createElement } from 'react'

import { deviceReasonLabels, deviceStatusLabels, issueDevice, markDeviceLost, markDeviceRepaired, replaceDevice, retireDevice, returnDevice, type DeviceReturnReason, type DeviceStatus } from '../../api/devicesApi'
import type { EntityRow } from '../../api/types'
import { masterDataEditorRoles } from '../../auth/roles'
import { StatusBadge } from '../../components/common/StatusBadge'
import type { FormField } from '../../components/master-data/MasterDataDialog'
import { formatCurrency } from '../../utils/formatters'
import type { MasterDataPageConfig } from './MasterDataPage'

type Row = EntityRow
const value = (row: Row | undefined, key: string) => String(row?.[key] ?? '')
const today = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}` }
const status = (row: Row) => (row.status as DeviceStatus | undefined) ?? 'InStock'
const held = (row: Row) => status(row) === 'Issued' || status(row) === 'ReturnPending'
const yesNo = [{ value: 'No', label: 'No' }, { value: 'Yes', label: 'Yes — record the depreciated value to recover' }]
const notes: FormField = { key: 'notes', label: 'Notes' }
const charge = (label = 'Charge the employee?'): FormField => ({ key: 'chargeEmployee', label, type: 'select', required: true, options: yesNo })
const tones: Record<DeviceStatus, 'success' | 'info' | 'warning' | 'error' | 'neutral'> = {
  InStock: 'success', Issued: 'info', ReturnPending: 'warning', UnderRepair: 'warning', Damaged: 'error', Lost: 'error', Retired: 'neutral',
}
const statusBadge = (row: Row) => createElement(StatusBadge, {
  label: ['UnderRepair', 'ReturnPending'].includes(status(row)) ? `${deviceStatusLabels[status(row)]} · ${value(row, 'daysInStatus')} days` : deviceStatusLabels[status(row)],
  tone: tones[status(row)],
})
const employeeOption = (row: Row) => ({ value: String(row.id), label: `${value(row, 'epf')} — ${value(row, 'fullName')} (${value(row, 'factoryName') || value(row, 'factoryCode')})` })
const deviceOption = (row: Row) => ({ value: String(row.id), label: `${value(row, 'assetTag')} — ${value(row, 'brand')} ${value(row, 'model')}` })

// The register. Administrator and IT Engineer register, issue, return and replace devices; other roles view them.
// A device leaves use by Mark Lost or Retire, not by Deactivate.
export const mobileDevicesConfig: MasterDataPageConfig<Row> = {
  title: 'Mobile Devices',
  path: '/api/mobile-devices',
  editRoles: masterDataEditorRoles,
  noDeactivate: true,
  columns: [
    { key: 'assetTag', header: 'Asset ID' },
    { key: 'model', header: 'Device', format: (_, row) => `${value(row, 'brand')} ${value(row, 'model')}` },
    { key: 'imei1', header: 'IMEI' },
    { key: 'status', header: 'Status', render: statusBadge },
    { key: 'holderName', header: 'Holder', format: (_, row) => row.holderName ? `${value(row, 'holderEpf')} — ${value(row, 'holderName')}` : '—' },
    { key: 'holderFactory', header: 'Factory', format: (_, row) => value(row, 'holderFactory') || '—' },
    { key: 'purchaseDate', header: 'Purchased' },
    { key: 'purchaseCost', header: 'Cost', format: amount => formatCurrency(Number(amount)) },
    { key: 'currentValue', header: 'Current Value', format: amount => formatCurrency(Number(amount)) },
  ],
  fields: [
    { key: 'assetTag', label: 'Asset ID', required: true },
    { key: 'imei1', label: 'IMEI 1', required: true, helperText: '15 digits (dial *#06# on the phone).' },
    { key: 'imei2', label: 'IMEI 2', helperText: 'For dual-SIM phones.' },
    { key: 'brand', label: 'Brand', required: true },
    { key: 'model', label: 'Model', required: true },
    { key: 'serialNumber', label: 'Serial No.' },
    { key: 'purchaseDate', label: 'Purchase Date', type: 'date', required: true },
    { key: 'purchaseCost', label: 'Purchase Cost', type: 'number', required: true },
    { key: 'warrantyUntil', label: 'Warranty Until', type: 'date' },
    { key: 'supplier', label: 'Supplier' },
    notes,
  ],
  lookups: [
    { fieldKey: 'employeeId', path: '/api/employees', toOption: employeeOption },
    { fieldKey: 'newDeviceId', path: '/api/mobile-devices/in-stock', toOption: deviceOption },
  ],
  toForm: row => Object.fromEntries(['assetTag', 'imei1', 'imei2', 'brand', 'model', 'serialNumber', 'purchaseDate', 'purchaseCost', 'warrantyUntil', 'supplier', 'notes'].map(key => [key, value(row, key)])),
  toRequest: values => ({
    assetTag: values.assetTag, imei1: values.imei1?.trim(), imei2: values.imei2?.trim() || null, brand: values.brand, model: values.model, serialNumber: values.serialNumber || null,
    purchaseDate: values.purchaseDate, purchaseCost: Number(values.purchaseCost), warrantyUntil: values.warrantyUntil || null, supplier: values.supplier || null, notes: values.notes || null,
  }),
  rowActionRoles: masterDataEditorRoles,
  rowActions: [
    {
      label: 'Issue',
      show: row => status(row) === 'InStock',
      title: row => `Issue ${value(row, 'assetTag')} (${value(row, 'brand')} ${value(row, 'model')})`,
      fields: () => [{ key: 'employeeId', label: 'Employee', type: 'select', required: true }, { key: 'issuedOn', label: 'Issue date', type: 'date', required: true }, notes],
      initialValues: () => ({ employeeId: '', issuedOn: today(), notes: '' }),
      submit: (row, values) => issueDevice(row.id, values.employeeId, values.issuedOn, values.notes),
    },
    {
      label: 'Return',
      show: held,
      title: row => `Return ${value(row, 'assetTag')} from ${value(row, 'holderName')}`,
      fields: () => [
        { key: 'returnedOn', label: 'Return date', type: 'date', required: true },
        { key: 'condition', label: 'Condition', type: 'select', required: true, options: [{ value: 'InStock', label: 'Good — back In Stock' }, { value: 'UnderRepair', label: 'Needs repair' }, { value: 'Damaged', label: 'Damaged' }] },
        { key: 'reason', label: 'Reason', type: 'select', required: true, options: (['Resigned', 'Upgrade', 'Other'] as DeviceReturnReason[]).map(reason => ({ value: reason, label: deviceReasonLabels[reason] })) },
        notes,
        charge('Charge the employee? (for example damage through negligence)'),
      ],
      initialValues: row => ({ returnedOn: today(), condition: 'InStock', reason: status(row) === 'ReturnPending' ? 'Resigned' : 'Other', notes: '', chargeEmployee: 'No' }),
      submit: (row, values) => returnDevice(row.id, values.returnedOn, values.condition, values.reason, values.notes, values.chargeEmployee === 'Yes'),
    },
    {
      label: 'Replace',
      show: row => status(row) === 'Issued',
      title: row => `Replace ${value(row, 'assetTag')} for ${value(row, 'holderName')}`,
      fields: () => [
        { key: 'newDeviceId', label: 'New device (In Stock)', type: 'select', required: true },
        { key: 'replacedOn', label: 'Replacement date', type: 'date', required: true },
        { key: 'oldCondition', label: 'Old device', type: 'select', required: true, options: [{ value: 'Damaged', label: 'Damaged' }, { value: 'UnderRepair', label: 'Send for repair' }] },
        { key: 'notes', label: 'What happened', helperText: 'For example: screen broken.' },
        charge('Charge the employee for the old device?'),
      ],
      initialValues: () => ({ newDeviceId: '', replacedOn: today(), oldCondition: 'Damaged', notes: '', chargeEmployee: 'No' }),
      submit: (row, values) => replaceDevice(row.id, values.newDeviceId, values.replacedOn, values.oldCondition, values.notes, values.chargeEmployee === 'Yes'),
    },
    {
      label: 'Back In Stock',
      show: row => status(row) === 'UnderRepair' || status(row) === 'Damaged',
      title: row => `${value(row, 'assetTag')} is repaired and back In Stock`,
      fields: () => [{ key: 'on', label: 'Date', type: 'date', required: true }, notes],
      initialValues: () => ({ on: today(), notes: '' }),
      submit: (row, values) => markDeviceRepaired(row.id, values.on, values.notes),
    },
    {
      label: 'Mark Lost',
      color: 'error',
      show: row => !['Lost', 'Retired'].includes(status(row)),
      title: row => `Mark ${value(row, 'assetTag')} as lost`,
      fields: () => [{ key: 'lostOn', label: 'Lost on', type: 'date', required: true }, notes, charge()],
      initialValues: row => ({ lostOn: today(), notes: '', chargeEmployee: held(row) ? 'Yes' : 'No' }),
      submit: (row, values) => markDeviceLost(row.id, values.lostOn, values.notes, values.chargeEmployee === 'Yes'),
    },
    {
      label: 'Retire',
      color: 'warning',
      show: row => ['InStock', 'UnderRepair', 'Damaged'].includes(status(row)),
      title: row => `Retire ${value(row, 'assetTag')} (take it out of use)`,
      fields: () => [{ key: 'on', label: 'Date', type: 'date', required: true }, notes],
      initialValues: () => ({ on: today(), notes: '' }),
      submit: (row, values) => retireDevice(row.id, values.on, values.notes),
    },
  ],
}

// Every handover, newest first. Read-only; the status filter's Active means still with the employee.
export const deviceIssuesConfig: MasterDataPageConfig<Row> = {
  title: 'Device History',
  path: '/api/mobile-devices/issues',
  editRoles: [],
  noDeactivate: true,
  columns: [
    { key: 'assetTag', header: 'Asset ID' },
    { key: 'model', header: 'Device', format: (_, row) => `${value(row, 'brand')} ${value(row, 'model')}` },
    { key: 'employeeName', header: 'Employee', format: (_, row) => `${value(row, 'epf')} — ${value(row, 'employeeName')}` },
    { key: 'factory', header: 'Factory' },
    { key: 'issuedOn', header: 'Issued' },
    { key: 'returnedOn', header: 'Returned', format: (_, row) => value(row, 'returnedOn') || 'Still with employee' },
    { key: 'returnReason', header: 'Reason', format: (_, row) => row.returnReason ? deviceReasonLabels[row.returnReason as DeviceReturnReason] : '—' },
    { key: 'returnCondition', header: 'Condition', format: (_, row) => row.returnCondition ? deviceStatusLabels[row.returnCondition as DeviceStatus] : '—' },
    { key: 'replacementAssetTag', header: 'Replaced By', format: (_, row) => value(row, 'replacementAssetTag') || '—' },
    { key: 'returnNotes', header: 'Notes', format: (_, row) => [value(row, 'issueNotes'), value(row, 'returnNotes')].filter(Boolean).join(' · ') || '—' },
    { key: 'recoverableAmount', header: 'To Recover', format: amount => amount === null || amount === undefined ? '—' : formatCurrency(Number(amount)) },
  ],
  fields: [],
  toForm: () => ({}),
  toRequest: () => ({}),
}
