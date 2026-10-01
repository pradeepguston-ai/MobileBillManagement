import type { EntityRow } from '../../api/types'
import type { MasterDataPageConfig } from './MasterDataPage'

type Row = EntityRow
const value = (row: Row | undefined, key: string) => String(row?.[key] ?? '')
const reference =(title: string, path: string): MasterDataPageConfig<Row> => ({ title, path, columns: [{ key: 'code', header: 'Code' }, { key: 'name', header: 'Name' }, { key: 'isActive', header: 'Status' }], fields: [{ key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true }], toForm: row => ({ code: value(row, 'code'), name: value(row, 'name') }), toRequest: values => ({ code: values.code, name: values.name }) })

export const factoriesConfig = reference('Factories', '/api/factories')
export const designationsConfig = reference('Designations', '/api/designations')
export const categoriesConfig = reference('Categories', '/api/categories')
export const providersConfig = reference('Providers', '/api/providers')
export const departmentsConfig = reference('Departments', '/api/departments')
export const employeesConfig: MasterDataPageConfig<Row> = {
  title: 'Employees',
  path: '/api/employees',
  columns: [{ key: 'epf', header: 'EPF' }, { key: 'fullName', header: 'Full Name' }, { key: 'callingName', header: 'Calling Name' }, { key: 'categoryName', header: 'Category' }, { key: 'designationName', header: 'Designation' }, { key: 'factoryName', header: 'Factory' }, { key: 'departmentName', header: 'Department' }, { key: 'isActive', header: 'Status' }],
  fields: [{ key: 'epf', label: 'EPF', required: true }, { key: 'fullName', label: 'Full Name', required: true }, { key: 'callingName', label: 'Calling Name' }, { key: 'categoryCode', label: 'Category', type: 'select', required: true }, { key: 'designationCode', label: 'Designation', type: 'select', required: true }, { key: 'factoryCode', label: 'Factory', type: 'select', required: true }, { key: 'departmentCode', label: 'Department', type: 'select', required: true }],
  lookups: [
    { fieldKey: 'categoryCode', path: '/api/categories', toOption: row => ({ value: String(row.code), label: `${String(row.code)} — ${String(row.name)}` }) },
    { fieldKey: 'designationCode', path: '/api/designations', toOption: row => ({ value: String(row.code), label: `${String(row.code)} — ${String(row.name)}` }) },
    { fieldKey: 'factoryCode', path: '/api/factories', toOption: row => ({ value: String(row.code), label: `${String(row.code)} — ${String(row.name)}` }) },
    { fieldKey: 'departmentCode', path: '/api/departments', toOption: row => ({ value: String(row.code), label: `${String(row.code)} — ${String(row.name)}` }) },
  ],
  toForm: row => Object.fromEntries(['epf', 'fullName', 'callingName', 'categoryCode', 'designationCode', 'factoryCode', 'departmentCode'].map(key => [key, value(row, key)])),
  toRequest: values => values,
}
export const mobileAllocationsConfig: MasterDataPageConfig<Row> = {
  title: 'Mobile Allocations',
  path: '/api/mobile-accounts',
  columns: [
    { key: 'mobileNumber', header: 'Mobile Number' },
    { key: 'epf', header: 'EPF' },
    { key: 'employeeName', header: 'Employee Name' },
    { key: 'monthlyCreditLimit', header: 'Monthly Credit Limit' },
    { key: 'monthlyRental', header: 'Monthly Rental' },
    { key: 'factory', header: 'Factory' },
    { key: 'department', header: 'Department' },
    { key: 'isActive', header: 'Status' },
  ],
  fields: [
    { key: 'mobileNumber', label: 'Mobile Number', required: true },
    { key: 'employeeEpf', label: 'Employee EPF', type: 'select', required: true },
    { key: 'monthlyCreditLimit', label: 'Monthly Credit Limit', type: 'number', required: true },
    { key: 'monthlyRental', label: 'Monthly Rental', type: 'number', required: true },
  ],
  lookups: [{ fieldKey: 'employeeEpf', path: '/api/employees', toOption: row => ({ value: String(row.epf), label: `${String(row.epf)} — ${String(row.fullName)}` }) }],
  toForm: row => ({ mobileNumber: value(row, 'mobileNumber'), employeeEpf: value(row, 'epf'), monthlyCreditLimit: value(row, 'monthlyCreditLimit'), monthlyRental: value(row, 'monthlyRental') }),
  toRequest: values => ({
    mobileNumber: values.mobileNumber,
    employeeEpf: values.employeeEpf,
    monthlyCreditLimit: Number(values.monthlyCreditLimit),
    monthlyRental: Number(values.monthlyRental),
  }),
}
