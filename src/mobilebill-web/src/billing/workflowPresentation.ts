export const workflowSteps = ['Validation', 'IT Review', 'HR Approval', 'Finance Approval', 'Completed', 'Locked'] as const

export const workflowStatusIndex: Record<string, number> = { Validated: 0, ITReview: 1, HRApproval: 2, FinanceApproval: 3, Completed: 4, Locked: 5 }

export function workflowStageLabel(status: string) {
  return status === 'ITReview' ? 'IT Review' : status === 'HRApproval' ? 'HR Approval' : status === 'FinanceApproval' ? 'Finance Approval' : status
}
