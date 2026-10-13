import type { ReviewRow } from '../../../api/billReviewApi'
import type { WorkflowAction } from '../../../api/billWorkflowApi'
import type { EntityRow } from '../../../api/types'

export type MasterOption = EntityRow & { name?: string; departmentCode?: string }
export type SelectOption = readonly [string, string]
export type ReviewColumn = { key: keyof ReviewRow; label: string; sortKey?: string }
export type WorkflowDialogState = {
  kind: 'submit' | 'decision'
  action?: WorkflowAction
  title: string
  commentRequired: boolean
}

// Remarks offered when approving a batch at any stage (IT, HR, Finance). The conditions must be written in the comment.
export const approvalRemarks = ['Approved', 'Reviewed and Approved', 'Verified and Approved', 'Approved with Conditions'] as const
export const approvalRemarkNeedingComment = 'Approved with Conditions'

export const isApproval = (state?: WorkflowDialogState) => state?.kind === 'decision' && state.action === 'Approve'

// The comment saved in the approval history: the chosen remark, then anything typed in the comment box.
export function approvalComment(remark: string, comment: string) {
  const typed = comment.trim()
  return typed ? `${remark}: ${typed}` : remark
}

export const displayName = (row: Pick<ReviewRow, 'employeeName' | 'nameWithInitials'>) => row.nameWithInitials?.trim() || row.employeeName

export const reviewColumns: ReviewColumn[] = [
  { key: 'mobileNumber', label: 'Mobile', sortKey: 'mobileNumber' },
  { key: 'employeeEpf', label: 'EPF', sortKey: 'epf' },
  // Shows the name with initials; the full name stands in when an employee has none (see displayName).
  { key: 'employeeName', label: 'Name With Initials', sortKey: 'employeeName' },
  { key: 'callingName', label: 'Calling Name', sortKey: 'callingName' },
  { key: 'category', label: 'Category', sortKey: 'category' },
  { key: 'designation', label: 'Designation', sortKey: 'designation' },
  { key: 'factory', label: 'Factory', sortKey: 'factory' },
  { key: 'department', label: 'Department', sortKey: 'department' },
  { key: 'packageCode', label: 'Package' },
  { key: 'creditLimit', label: 'Credit Limit', sortKey: 'creditLimit' },
  { key: 'monthlyRental', label: 'Monthly Rental', sortKey: 'monthlyRental' },
  { key: 'actualBill', label: 'Actual Bill', sortKey: 'actualBill' },
  { key: 'variance', label: 'Variance', sortKey: 'variance' },
  { key: 'calculatedExcess', label: 'Calculated Excess', sortKey: 'calculatedExcess' },
  { key: 'responsibility', label: 'Responsibility', sortKey: 'responsibility' },
  { key: 'finalDeduction', label: 'Final Deduction', sortKey: 'finalDeduction' },
]
