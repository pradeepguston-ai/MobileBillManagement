import { Alert, Button } from '@mui/material'
import { useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { getSimPool } from '../../api/masterDataApi'

// Dashboard reminder: numbers idle in the SIM Pool for over 60 days still cost the company every month.
export function SimPoolAlert() {
  const [longIdle, setLongIdle] = useState(0)
  const [total, setTotal] = useState(0)
  useEffect(() => {
    let active = true
    getSimPool()
      .then(items => { if (active) { setTotal(items.length); setLongIdle(items.filter(item => item.isLongIdle).length) } })
      .catch(() => { /* the reminder is optional; the dashboard works without it */ })
    return () => { active = false }
  }, [])
  if (longIdle === 0) return null
  return <Alert severity="warning" action={<Button component={RouterLink} to="/mobile-allocations?tab=pool" color="inherit" size="small">Open SIM Pool</Button>}>
    {longIdle} of {total} pooled {total === 1 ? 'number has' : 'numbers have'} been idle for more than 60 days and {longIdle === 1 ? 'is' : 'are'} still billed. Assign {longIdle === 1 ? 'it' : 'them'} to a new holder or disconnect {longIdle === 1 ? 'it' : 'them'}.
  </Alert>
}
