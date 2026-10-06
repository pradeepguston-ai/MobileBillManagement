import type { UserRole } from '../api/authApi'

// Full control of every master, including telecom providers and mobile allocation amounts.
export const masterDataEditorRoles: readonly UserRole[] = ['Administrator', 'ITEngineer']
// Employee and organisation masters; HR and Finance users maintain these too.
export const organisationMasterEditorRoles: readonly UserRole[] = [...masterDataEditorRoles, 'HrUser', 'FinanceUser']
// Moving a mobile number to another employee, for roles without full allocation rights.
export const allocationReassignRoles: readonly UserRole[] = ['HrUser', 'FinanceUser']
// Preparing billing (create, upload, parse, validate, match), assessing bills and resolving exceptions.
// Every other role, including HR and Finance users, views billing and downloads reports only.
export const billingPreparerRoles: readonly UserRole[] = ['Administrator', 'ITEngineer']
