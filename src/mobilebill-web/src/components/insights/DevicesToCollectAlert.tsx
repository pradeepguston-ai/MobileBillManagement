import { Alert, Button } from '@mui/material'
import { useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { getDevicesToCollect } from '../../api/devicesApi'

// Dashboard reminder: company devices still with employees who have left.
export function DevicesToCollectAlert() {
  const [total, setTotal] = useState(0)
  const [overdue, setOverdue] = useState(0)
  useEffect(() => {
    let active = true
    getDevicesToCollect()
      .then(items => { if (active && Array.isArray(items)) { setTotal(items.length); setOverdue(items.filter(item => item.isOverdue).length) } })
      .catch(() => { /* the reminder is optional; the dashboard works without it */ })
    return () => { active = false }
  }, [])
  if (total === 0) return null
  return <Alert severity={overdue > 0 ? 'warning' : 'info'} action={<Button component={RouterLink} to="/devices?tab=collect" color="inherit" size="small">Open To Collect</Button>}>
    {total} company {total === 1 ? 'device is' : 'devices are'} still with employees who have left{overdue > 0 ? `; ${overdue} overdue for collection` : ''}.
  </Alert>
}
