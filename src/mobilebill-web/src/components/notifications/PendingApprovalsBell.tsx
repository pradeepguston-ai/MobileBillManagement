import { Alert, Badge, Box, Button, Divider, IconButton, ListItemButton, ListItemText, Popover, Snackbar, Tooltip, Typography } from '@mui/material'
import NotificationsIcon from '@mui/icons-material/Notifications'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'

import { approverRoles, billingPeriod, getPendingApprovals, type PendingApproval } from '../../api/billWorkflowApi'
import { useAuth } from '../../auth/AuthContext'
import { formatDateTime } from '../../utils/formatters'

export const pendingApprovalsRefreshMs = 60_000

// The bell in the top bar for the Head of IT, Group HR Manager, CFO and Administrator: how many billing batches wait for
// their approval, with a list that opens each batch. It refreshes every minute, on returning to the window and on every
// page change (so it clears straight after an approval). A batch that arrives while the user is signed in pops up once.
export function PendingApprovalsBell() {
  const { user } = useAuth()
  const isApprover = Boolean(user && (approverRoles as readonly string[]).includes(user.role))
  return isApprover && user ? <Bell userId={user.id} /> : null
}

function Bell({ userId }: { userId: string }) {
  const [items, setItems] = useState<PendingApproval[]>([])
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const [arrived, setArrived] = useState<PendingApproval[]>([])
  const seen = useRef<Set<string>>(readSeen(userId))
  const location = useLocation()
  const navigate = useNavigate()

  const refresh = useCallback(async () => {
    try {
      const result = await getPendingApprovals()
      const next = Array.isArray(result) ? result : []
      setItems(next)
      const fresh = next.filter(item => !seen.current.has(item.batchId))
      if (fresh.length > 0) {
        fresh.forEach(item => seen.current.add(item.batchId))
        writeSeen(userId, seen.current)
        setArrived(fresh)
      }
    } catch { /* the bell is optional; the last list stays until the next refresh */ }
  }, [userId])

  useEffect(() => { void refresh() }, [refresh, location.pathname])
  useEffect(() => {
    const timer = window.setInterval(() => void refresh(), pendingApprovalsRefreshMs)
    const onFocus = () => void refresh()
    window.addEventListener('focus', onFocus)
    return () => { window.clearInterval(timer); window.removeEventListener('focus', onFocus) }
  }, [refresh])

  const open = (item: PendingApproval) => { setAnchor(null); setArrived([]); navigate(`/billing/${item.batchId}/review`) }
  const count = items.length
  const label = count === 0 ? 'No approvals waiting' : `${count} ${count === 1 ? 'batch is' : 'batches are'} waiting for your approval`

  return <>
    <Tooltip title={label}>
      <IconButton color="inherit" aria-label={`Pending approvals: ${count}`} onClick={event => setAnchor(event.currentTarget)} sx={{ mr: 1 }}>
        <Badge badgeContent={count} color="error" max={99}><NotificationsIcon fontSize="small" /></Badge>
      </IconButton>
    </Tooltip>
    <Popover open={Boolean(anchor)} anchorEl={anchor} onClose={() => setAnchor(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      slotProps={{ paper: { sx: { width: 380, maxWidth: 'calc(100vw - 32px)' } } }}>
      <Box sx={{ px: 2, py: 1.5 }}><Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Pending approvals</Typography></Box>
      <Divider />
      {count === 0
        ? <Typography variant="body2" color="text.secondary" sx={{ px: 2, py: 2 }}>No billing batches are waiting for your approval.</Typography>
        : <Box role="list" aria-label="Pending approvals" sx={{ maxHeight: 400, overflowY: 'auto' }}>
          {items.map(item => <ListItemButton key={item.batchId} role="listitem" onClick={() => open(item)} divider>
            <ListItemText
              primary={`${billingPeriod(item)} · ${item.provider}`}
              secondary={<>
                <span>Waiting for {item.step} · {item.numberCount} numbers</span><br />
                {item.lastActionBy && <span>{item.lastActionBy}{item.waitingSince ? `, ${formatDateTime(item.waitingSince)}` : ''}{item.lastComment ? `: “${item.lastComment}”` : ''}</span>}
              </>}
              slotProps={{ primary: { sx: { fontWeight: 600 } } }} />
          </ListItemButton>)}
        </Box>}
    </Popover>
    <Snackbar open={arrived.length > 0} anchorOrigin={{ vertical: 'top', horizontal: 'right' }} onClose={(_, reason) => { if (reason !== 'clickaway') setArrived([]) }} autoHideDuration={15_000} sx={{ mt: 7 }}>
      <Alert severity="info" variant="filled" onClose={() => setArrived([])}
        action={<Button color="inherit" size="small" onClick={event => { if (arrived.length === 1) open(arrived[0]); else { setArrived([]); setAnchor(event.currentTarget) } }}>{arrived.length === 1 ? 'Open' : 'View'}</Button>}>
        {arrived.length === 1
          ? `The ${billingPeriod(arrived[0])} bill is waiting for your ${arrived[0].step}.`
          : `${arrived.length} billing batches are waiting for your approval.`}
      </Alert>
    </Snackbar>
  </>
}

// Batches already announced to this user in this browser session, so a refresh does not pop them up again.
const seenKey = (userId: string) => `pending-approvals-seen:${userId}`
function readSeen(userId: string): Set<string> {
  try { return new Set(JSON.parse(sessionStorage.getItem(seenKey(userId)) ?? '[]') as string[]) } catch { return new Set() }
}
function writeSeen(userId: string, ids: Set<string>) {
  try { sessionStorage.setItem(seenKey(userId), JSON.stringify([...ids])) } catch { /* storage unavailable */ }
}
