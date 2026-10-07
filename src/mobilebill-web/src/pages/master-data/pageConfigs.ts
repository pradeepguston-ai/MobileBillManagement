import { applyPackageRental, assignFromPool, disconnectSim, releaseToPool, resignEmployee, type SimStatus } from '../../api/masterDataApi'
import { createElement } from 'react'

import type { EntityRow } from '../../api/types'
import { StatusBadge } from '../../components/common/StatusBadge'
import { masterDataEditorRoles, organisationMasterEditorRoles } from '../../auth/roles'
import type { MasterDataPageConfig } from './MasterDataPage'

type Row = EntityRow
const value = (row: Row | undefined, key: string) => String(row?.[key] ?? '')
// Today's local date as yyyy-MM-dd, for date fields.
const today = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}` }
const simStatus = (row: Row) => (row.status as SimStatus | undefined) ?? 'Assigned'
const simStatusLabel = (row: Row) => simStatus(row) === 'Pooled' ? `SIM Pool since ${value(row, 'pooledOn')}` : simStatus(row) === 'Disconnected' ? `Disconnected ${value(row, 'disconnectedOn')}`.trim() : 'Assigned'
const reference =(title: string, path: string, editRoles = organisationMasterEditorRoles): MasterDataPageConfig<Row> => ({ title, path, editRoles, columns: [{ key: 'code', header: 'Code' }, { key: 'name', header: 'Name' }, { key: 'isActive', header: 'Status' }], fields: [{ key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true }], toForm: row => ({ code: value(row, 'code'), name: value(row, 'name') }), toRequest: values => ({ code: values.code, name: values.name }) })

export const factoriesConfig = reference('Factories', '/api/factories')
export const designationsConfig = reference('Designations', '/api/designations')
export const categoriesConfig = reference('Categories', '/api/categories')
// Telecom providers stay with Administrator and IT Engineer; HR and Finance users see them read-only.
export const providersConfig = reference('Providers', '/api/providers', masterDataEditorRoles)
export const departmentsConfig = reference('Departments', '/api/departments')
// The same EPF can exist in different factories, so employees are picked by ID and shown with their factory.
const employeeOption = (row: Row) => ({ value: String(row.id), label: `${value(row, 'epf')} — ${value(row, 'fullName')} (${value(row, 'factoryName') || value(row, 'factoryCode')})` })
// The SIM type of an allocation, as stored and as shown.
export const simTypeLabels: Record<string, string> = { Voice: 'Voice', VoiceData: 'Voice + Data', Data: 'Data', ESim: 'eSIM' }
const simTypeOptions = Object.entries(simTypeLabels).map(([key, label]) => ({ value: key, label }))
const packageOption = (row: Row) => ({ value: String(row.id), label: `${value(row, 'code')} — ${value(row, 'description')} (${value(row, 'providerName')})`, data: row })
const codeOption = (row: Row, parentKey?: string) => ({ value: String(row.code), label: `${String(row.code)} — ${String(row.name)}`, parentValue: parentKey ? String(row[parentKey] ?? '') : undefined })
// Department → Section → Sub Section.
export const sectionsConfig: MasterDataPageConfig<Row> = {
  title: 'Sections',
  path: '/api/sections',
  editRoles: organisationMasterEditorRoles,
  columns: [{ key: 'code', header: 'Code' }, { key: 'name', header: 'Name' }, { key: 'departmentName', header: 'Department' }, { key: 'isActive', header: 'Status' }],
  fields: [{ key: 'departmentCode', label: 'Department', type: 'select', required: true }, { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true }],
  lookups: [{ fieldKey: 'departmentCode', path: '/api/departments', toOption: row => codeOption(row) }],
  toForm: row => ({ departmentCode: value(row, 'departmentCode'), code: value(row, 'code'), name: value(row, 'name') }),
  toRequest: values => ({ code: values.code, name: values.name, departmentCode: values.departmentCode }),
}
export const subSectionsConfig: MasterDataPageConfig<Row> = {
  title: 'Sub Sections',
  path: '/api/sub-sections',
  editRoles: organisationMasterEditorRoles,
  columns: [{ key: 'code', header: 'Code' }, { key: 'name', header: 'Name' }, { key: 'sectionName', header: 'Section' }, { key: 'departmentName', header: 'Department' }, { key: 'isActive', header: 'Status' }],
  fields: [{ key: 'departmentCode', label: 'Department', type: 'select', required: true }, { key: 'sectionCode', label: 'Section', type: 'select', required: true, dependsOn: 'departmentCode' }, { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true }],
  lookups: [
    { fieldKey: 'departmentCode', path: '/api/departments', toOption: row => codeOption(row) },
    { fieldKey: 'sectionCode', path: '/api/sections', toOption: row => codeOption(row, 'departmentCode') },
  ],
  toForm: row => ({ departmentCode: value(row, 'departmentCode'), sectionCode: value(row, 'sectionCode'), code: value(row, 'code'), name: value(row, 'name') }),
  toRequest: values => ({ code: values.code, name: values.name, sectionCode: values.sectionCode }),
}
// An active employee with a resignation date is serving notice until that day.
const employeeStatus = (row: Row) => createElement(StatusBadge, row.isActive
  ? row.resignedOn ? { label: `Leaving ${value(row, 'resignedOn')}`, tone: 'warning' } : { label: 'Active', tone: 'success' }
  : { label: row.resignedOn ? 'Resigned' : 'Inactive', tone: 'neutral' })
export const employeesConfig: MasterDataPageConfig<Row> = {
  title: 'Employees',
  path: '/api/employees',
  editRoles: organisationMasterEditorRoles,
  importer: { roles: organisationMasterEditorRoles, title: 'Import employees from Excel' },
  columns: [{ key: 'epf', header: 'EPF' }, { key: 'fullName', header: 'Full Name' }, { key: 'callingName', header: 'Calling Name' }, { key: 'categoryName', header: 'Category' }, { key: 'designationName', header: 'Designation' }, { key: 'factoryName', header: 'Factory' }, { key: 'departmentName', header: 'Department' }, { key: 'sectionName', header: 'Section' }, { key: 'subSectionName', header: 'Sub Section' }, { key: 'isActive', header: 'Status', render: employeeStatus }],
  fields: [{ key: 'epf', label: 'EPF', required: true }, { key: 'fullName', label: 'Full Name', required: true }, { key: 'callingName', label: 'Calling Name' }, { key: 'categoryCode', label: 'Category', type: 'select', required: true }, { key: 'designationCode', label: 'Designation', type: 'select', required: true }, { key: 'factoryCode', label: 'Factory', type: 'select', required: true }, { key: 'departmentCode', label: 'Department', type: 'select', required: true }, { key: 'sectionCode', label: 'Section', type: 'select', dependsOn: 'departmentCode' }, { key: 'subSectionCode', label: 'Sub Section', type: 'select', dependsOn: 'sectionCode' }],
  lookups: [
    { fieldKey: 'categoryCode', path: '/api/categories', toOption: row => codeOption(row) },
    { fieldKey: 'designationCode', path: '/api/designations', toOption: row => codeOption(row) },
    { fieldKey: 'factoryCode', path: '/api/factories', toOption: row => codeOption(row) },
    { fieldKey: 'departmentCode', path: '/api/departments', toOption: row => codeOption(row) },
    { fieldKey: 'sectionCode', path: '/api/sections', toOption: row => codeOption(row, 'departmentCode') },
    { fieldKey: 'subSectionCode', path: '/api/sub-sections', toOption: row => codeOption(row, 'sectionCode') },
  ],
  toForm: row => Object.fromEntries(['epf', 'fullName', 'callingName', 'categoryCode', 'designationCode', 'factoryCode', 'departmentCode', 'sectionCode', 'subSectionCode'].map(key => [key, value(row, key)])),
  toRequest: values => ({ ...values, sectionCode: values.sectionCode || null, subSectionCode: values.subSectionCode || null }),
  // Resign releases every number the employee holds to the SIM Pool and deactivates them in one step. A future
  // date makes it pending instead (Pending Resignation tab); it completes automatically on that date.
  rowActionRoles: organisationMasterEditorRoles,
  rowActions: [{
    label: 'Resign',
    color: 'warning',
    show: row => Boolean(row.isActive),
    title: row => `Resign ${value(row, 'fullName')} (${value(row, 'epf')})`,
    fields: () => [{ key: 'resignedOn', label: 'Resignation date', type: 'date', required: true, helperText: 'A future date keeps the employee and their numbers until that day.' }, { key: 'reason', label: 'Reason' }],
    initialValues: row => ({ resignedOn: value(row, 'resignedOn') || today(), reason: 'Resigned' }),
    submit: (row, values) => resignEmployee(row.id, values.resignedOn, values.reason),
  }],
}
export const mobileAllocationsConfig: MasterDataPageConfig<Row> = {
  title: 'Mobile Allocations',
  path: '/api/mobile-accounts',
  editRoles: masterDataEditorRoles,
  // Importing creates allocations and sets amounts, so it is for Administrator and IT Engineer only.
  importer: { roles: masterDataEditorRoles, title: 'Import mobile allocations from Excel' },
  // SIM Pool: release a resigned holder's number, give a pooled number to a new holder, or disconnect it.
  // Like every change to allocations, these are for Administrator and IT Engineer; other roles view the list only.
  rowActionRoles: masterDataEditorRoles,
  rowActions: [
    {
      label: 'Release to Pool',
      color: 'warning',
      show: row => Boolean(row.isActive) && simStatus(row) === 'Assigned',
      title: row => `Release ${value(row, 'mobileNumber')} to the SIM Pool`,
      fields: () => [{ key: 'resignedOn', label: 'Resignation date', type: 'date', required: true }, { key: 'reason', label: 'Reason' }],
      initialValues: () => ({ resignedOn: today(), reason: 'Resigned' }),
      submit: (row, values) => releaseToPool(row.id, values.resignedOn, values.reason),
    },
    {
      label: 'Assign from Pool',
      show: row => Boolean(row.isActive) && simStatus(row) === 'Pooled',
      title: row => `Assign ${value(row, 'mobileNumber')} to a new holder`,
      // Package and amounts start as the pooled SIM's and can be changed for the new holder.
      fields: () => [
        { key: 'employeeId', label: 'New holder', type: 'select', required: true },
        { key: 'packageId', label: 'Package', type: 'select' },
        { key: 'monthlyCreditLimit', label: 'Monthly Credit Limit', type: 'number', required: true },
        { key: 'monthlyRental', label: 'Monthly Rental', type: 'number', required: true },
      ],
      initialValues: row => ({ employeeId: '', packageId: value(row, 'packageId'), monthlyCreditLimit: value(row, 'monthlyCreditLimit'), monthlyRental: value(row, 'monthlyRental') }),
      submit: (row, values) => assignFromPool(row.id, values.employeeId,
        { monthlyCreditLimit: Number(values.monthlyCreditLimit), monthlyRental: Number(values.monthlyRental), packageId: values.packageId && values.packageId !== value(row, 'packageId') ? values.packageId : undefined }),
    },
    {
      label: 'Disconnect',
      color: 'error',
      show: row => Boolean(row.isActive),
      title: row => `Disconnect ${value(row, 'mobileNumber')}`,
      fields: () => [{ key: 'disconnectedOn', label: 'Disconnection date', type: 'date', required: true }, { key: 'reason', label: 'Reason' }],
      initialValues: () => ({ disconnectedOn: today(), reason: '' }),
      submit: (row, values) => disconnectSim(row.id, values.disconnectedOn, values.reason),
    },
  ],
  columns: [
    { key: 'mobileNumber', header: 'Mobile Number' },
    { key: 'epf', header: 'EPF' },
    { key: 'employeeName', header: 'Employee Name' },
    { key: 'simType', header: 'SIM Type', format: (_, row) => simTypeLabels[value(row, 'simType')] ?? '—' },
    { key: 'packageCode', header: 'Package', format: (_, row) => row.packageCode ? `${value(row, 'packageCode')}${row.differsFromPackage ? ' (rental differs)' : ''}` : '—' },
    { key: 'monthlyCreditLimit', header: 'Monthly Credit Limit' },
    { key: 'monthlyRental', header: 'Monthly Rental' },
    { key: 'factory', header: 'Factory' },
    { key: 'department', header: 'Department' },
    { key: 'status', header: 'SIM Status', format: (_, row) => simStatusLabel(row) },
    { key: 'isActive', header: 'Status' },
  ],
  fields: [
    { key: 'mobileNumber', label: 'Mobile Number', required: true },
    { key: 'employeeId', label: 'Employee', type: 'select', required: true },
    { key: 'simType', label: 'SIM Type', type: 'select', options: simTypeOptions },
    // Required for new allocations (checked by the server); older allocations may not have one yet.
    { key: 'packageId', label: 'Package', type: 'select', helperText: 'Fills in the rental, and the credit limit for a new allocation.' },
    { key: 'monthlyCreditLimit', label: 'Monthly Credit Limit', type: 'number', required: true },
    { key: 'monthlyRental', label: 'Monthly Rental', type: 'number', required: true },
  ],
  lookups: [
    { fieldKey: 'employeeId', path: '/api/employees', toOption: employeeOption },
    { fieldKey: 'packageId', path: '/api/mobile-packages', toOption: packageOption },
  ],
  // Choosing a package fills in its rental; the credit limit is per person, so only a new allocation takes the package's default.
  autofill: (key, option, creating) => key === 'packageId' && option?.data
    ? { monthlyRental: String(option.data.monthlyRental), ...(creating ? { monthlyCreditLimit: String(option.data.defaultCreditLimit) } : {}) }
    : undefined,
  toForm: row => ({ mobileNumber: value(row, 'mobileNumber'), employeeId: value(row, 'employeeId'), simType: value(row, 'simType'), packageId: value(row, 'packageId'), monthlyCreditLimit: value(row, 'monthlyCreditLimit'), monthlyRental: value(row, 'monthlyRental') }),
  toRequest: values => ({
    mobileNumber: values.mobileNumber,
    employeeId: values.employeeId,
    monthlyCreditLimit: Number(values.monthlyCreditLimit),
    monthlyRental: Number(values.monthlyRental),
    packageId: values.packageId || null,
    simType: values.simType || null,
  }),
}
// Mobile packages are kept by Administrator and IT Engineer, like telecom providers.
export const mobilePackagesConfig: MasterDataPageConfig<Row> = {
  title: 'Mobile Packages',
  path: '/api/mobile-packages',
  editRoles: masterDataEditorRoles,
  columns: [
    { key: 'code', header: 'Package Code' },
    { key: 'providerName', header: 'Provider' },
    { key: 'description', header: 'Description' },
    { key: 'monthlyRental', header: 'Rental' },
    { key: 'totalWithTax', header: 'Total with Tax' },
    { key: 'defaultCreditLimit', header: 'Default Credit Limit' },
    { key: 'allocationCount', header: 'Allocations', format: (_, row) => Number(row.allocationsWithOtherRental) > 0 ? `${value(row, 'allocationCount')} (${value(row, 'allocationsWithOtherRental')} on another rental)` : value(row, 'allocationCount') },
    { key: 'isActive', header: 'Status' },
  ],
  fields: [
    { key: 'providerId', label: 'Telecom Provider', type: 'select', required: true },
    { key: 'code', label: 'Package Code', required: true },
    { key: 'description', label: 'Package Description', required: true },
    { key: 'monthlyRental', label: 'Rental (before tax)', type: 'number', required: true },
    { key: 'totalWithTax', label: 'Total with Tax', type: 'number', required: true },
    { key: 'defaultCreditLimit', label: 'Default Credit Limit', type: 'number', required: true, helperText: 'Suggested for new allocations; each person can have their own.' },
  ],
  lookups: [{ fieldKey: 'providerId', path: '/api/providers', toOption: row => ({ value: String(row.id), label: `${String(row.code)} — ${String(row.name)}`, data: row }) }],
  // Most packages are Dialog's, so a new package starts with Dialog selected (it can be changed).
  createDefaults: (options): Record<string, string> => {
    const dialog = options.providerId?.find(option => String(option.data?.name ?? '').trim().toLowerCase() === 'dialog')
    return dialog ? { providerId: dialog.value } : {}
  },
  toForm: row => Object.fromEntries(['providerId', 'code', 'description', 'monthlyRental', 'totalWithTax', 'defaultCreditLimit'].map(key => [key, value(row, key)])),
  toRequest: values => ({ providerId: values.providerId, code: values.code, description: values.description, monthlyRental: Number(values.monthlyRental), totalWithTax: Number(values.totalWithTax), defaultCreditLimit: Number(values.defaultCreditLimit) }),
  // After a package's rental changes, its allocations keep their old rental until this is run.
  rowActionRoles: masterDataEditorRoles,
  rowActions: [{
    label: 'Apply Rental',
    show: row => Boolean(row.isActive) && Number(row.allocationsWithOtherRental) > 0,
    title: row => `Set the rental of ${value(row, 'allocationsWithOtherRental')} allocation(s) on ${value(row, 'code')} to ${value(row, 'monthlyRental')}? Credit limits stay as they are.`,
    fields: () => [],
    initialValues: () => ({}),
    submit: row => applyPackageRental(row.id),
  }],
}
