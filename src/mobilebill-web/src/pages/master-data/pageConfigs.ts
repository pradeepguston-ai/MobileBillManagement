import { assignFromPool, disconnectSim, reassignMobileAccount, releaseToPool, resignEmployee, type SimStatus } from '../../api/masterDataApi'
import type { EntityRow } from '../../api/types'
import { allocationReassignRoles, masterDataEditorRoles, organisationMasterEditorRoles } from '../../auth/roles'
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
export const employeesConfig: MasterDataPageConfig<Row> = {
  title: 'Employees',
  path: '/api/employees',
  editRoles: organisationMasterEditorRoles,
  importer: { roles: organisationMasterEditorRoles, title: 'Import employees from Excel' },
  columns: [{ key: 'epf', header: 'EPF' }, { key: 'fullName', header: 'Full Name' }, { key: 'callingName', header: 'Calling Name' }, { key: 'categoryName', header: 'Category' }, { key: 'designationName', header: 'Designation' }, { key: 'factoryName', header: 'Factory' }, { key: 'departmentName', header: 'Department' }, { key: 'sectionName', header: 'Section' }, { key: 'subSectionName', header: 'Sub Section' }, { key: 'isActive', header: 'Status' }],
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
  // Resign releases every number the employee holds to the SIM Pool and deactivates them in one step.
  rowActionRoles: organisationMasterEditorRoles,
  rowActions: [{
    label: 'Resign',
    color: 'warning',
    show: row => Boolean(row.isActive),
    title: row => `Resign ${value(row, 'fullName')} (${value(row, 'epf')})`,
    fields: () => [{ key: 'resignedOn', label: 'Resignation date', type: 'date', required: true }, { key: 'reason', label: 'Reason' }],
    initialValues: () => ({ resignedOn: today(), reason: 'Resigned' }),
    submit: (row, values) => resignEmployee(row.id, values.resignedOn, values.reason),
  }],
}
export const mobileAllocationsConfig: MasterDataPageConfig<Row> = {
  title: 'Mobile Allocations',
  path: '/api/mobile-accounts',
  editRoles: masterDataEditorRoles,
  // Importing creates allocations and sets amounts, so it is for Administrator and IT Engineer only.
  importer: { roles: masterDataEditorRoles, title: 'Import mobile allocations from Excel' },
  // HR and Finance users cannot create allocations or change amounts; they can only move a number to another employee.
  limitedEdit: {
    roles: allocationReassignRoles,
    label: 'Reassign',
    title: 'Reassign Mobile Number',
    editableFields: ['employeeId'],
    canEditRow: row => Boolean(row.isActive) && simStatus(row) === 'Assigned',
    save: (id, values) => reassignMobileAccount(id, values.employeeId),
  },
  // SIM Pool: release a resigned holder's number, give a pooled number to a new holder, or disconnect it.
  rowActionRoles: organisationMasterEditorRoles,
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
      // Only Administrator and IT Engineer may change the amounts; others keep the pooled SIM's limit and rental.
      fields: fullEditor => [
        { key: 'employeeId', label: 'New holder', type: 'select', required: true },
        { key: 'monthlyCreditLimit', label: 'Monthly Credit Limit', type: 'number', required: true, disabled: !fullEditor },
        { key: 'monthlyRental', label: 'Monthly Rental', type: 'number', required: true, disabled: !fullEditor },
      ],
      initialValues: row => ({ employeeId: '', monthlyCreditLimit: value(row, 'monthlyCreditLimit'), monthlyRental: value(row, 'monthlyRental') }),
      submit: (row, values, fullEditor) => assignFromPool(row.id, values.employeeId, fullEditor ? { monthlyCreditLimit: Number(values.monthlyCreditLimit), monthlyRental: Number(values.monthlyRental) } : undefined),
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
    { key: 'monthlyCreditLimit', label: 'Monthly Credit Limit', type: 'number', required: true },
    { key: 'monthlyRental', label: 'Monthly Rental', type: 'number', required: true },
  ],
  lookups: [{ fieldKey: 'employeeId', path: '/api/employees', toOption: employeeOption }],
  toForm: row => ({ mobileNumber: value(row, 'mobileNumber'), employeeId: value(row, 'employeeId'), monthlyCreditLimit: value(row, 'monthlyCreditLimit'), monthlyRental: value(row, 'monthlyRental') }),
  toRequest: values => ({
    mobileNumber: values.mobileNumber,
    employeeId: values.employeeId,
    monthlyCreditLimit: Number(values.monthlyCreditLimit),
    monthlyRental: Number(values.monthlyRental),
  }),
}
