import type { ReviewRow } from '../../../api/billReviewApi'
import type { WorkflowAction } from '../../../api/billWorkflowApi'
import type { EntityRow } from '../../../api/types'

export type MasterOption = EntityRow & { name?: string }
export type SelectOption = readonly [string, string]
export type ReviewColumn = { key: keyof ReviewRow; label: string; sortKey?: string }
export type WorkflowDialogState = {
  kind: 'submit' | 'decision'
  action?: WorkflowAction
  title: string
  commentRequired: boolean
}

export const reviewColumns: ReviewColumn[] = [
  { key: 'mobileNumber', label: 'Mobile', sortKey: 'mobileNumber' },
  { key: 'employeeEpf', label: 'EPF', sortKey: 'epf' },
  { key: 'employeeName', label: 'Name', sortKey: 'employeeName' },
  { key: 'category', label: 'Category', sortKey: 'category' },
  { key: 'designation', label: 'Designation', sortKey: 'designation' },
  { key: 'factory', label: 'Factory', sortKey: 'factory' },
  { key: 'department', label: 'Department', sortKey: 'department' },
  { key: 'callingName', label: 'Calling Name', sortKey: 'callingName' },
  { key: 'creditLimit', label: 'Credit Limit', sortKey: 'creditLimit' },
  { key: 'monthlyRental', label: 'Monthly Rental', sortKey: 'monthlyRental' },
  { key: 'actualBill', label: 'Actual Bill', sortKey: 'actualBill' },
  { key: 'variance', label: 'Variance', sortKey: 'variance' },
  { key: 'calculatedExcess', label: 'Calculated Excess', sortKey: 'calculatedExcess' },
  { key: 'responsibility', label: 'Responsibility', sortKey: 'responsibility' },
  { key: 'finalDeduction', label: 'Final Deduction', sortKey: 'finalDeduction' },
]
