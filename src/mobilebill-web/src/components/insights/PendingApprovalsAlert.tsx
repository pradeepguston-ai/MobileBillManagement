import { Alert, Button } from '@mui/material'
import { useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { approverRoles, billingPeriod, getPendingApprovals, type PendingApproval } from '../../api/billWorkflowApi'
import { useAuth } from '../../auth/AuthContext'

// Dashboard reminder for the Head of IT, Group HR Manager, CFO and Administrator: batches waiting for their approval.
export function PendingApprovalsAlert() {
  const { user } = useAuth()
  const isApprover = Boolean(user && (approverRoles as readonly string[]).includes(user.role))
  const [items, setItems] = useState<PendingApproval[]>([])
  useEffect(() => {
    if (!isApprover) return
    let active = true
    getPendingApprovals()
      .then(result => { if (active && Array.isArray(result)) setItems(result) })
      .catch(() => { /* the reminder is optional; the dashboard works without it */ })
    return () => { active = false }
  }, [isApprover])
  if (items.length === 0) return null
  const first = items[0]
  return <Alert severity="info" action={<Button component={RouterLink} to={`/billing/${first.batchId}/review`} color="inherit" size="small">{items.length === 1 ? 'Review' : 'Review oldest'}</Button>}>
    {items.length === 1
      ? `The ${billingPeriod(first)} bill is waiting for your ${first.step}.`
      : `${items.length} billing batches are waiting for your approval. The oldest is ${billingPeriod(first)} (${first.step}).`}
  </Alert>
}
