import { Alert, Button, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Link, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import { useCallback, useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { getReportEmailDeliveries, previewReportEmails, resendReportEmail, sendReportEmails, type ReportEmailDelivery, type ReportEmailPreview } from '../../api/reportDistributionApi'
import { formatDateTime } from '../../utils/formatters'
import { LoadingState } from '../common/LoadingState'

export const deliveryRefreshMs = 5_000

// Sends a batch's monthly bill report to each Report Distribution rule's recipients (Excel and PDF, filtered to the rule's
// factories and categories) and shows what has been sent. Emails are sent in the background; the list refreshes until they finish.
export function EmailReportsDialog({ batchId, period, onClose }: { batchId: string; period: string; onClose: () => void }) {
  const [preview, setPreview] = useState<ReportEmailPreview[]>()
  const [previewError, setPreviewError] = useState<string>()
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [deliveries, setDeliveries] = useState<ReportEmailDelivery[]>([])
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string>()
  const [queued, setQueued] = useState(0)

  const loadDeliveries = useCallback(async () => {
    try { setDeliveries(await getReportEmailDeliveries(batchId)) } catch { /* the history is kept until the next refresh */ }
  }, [batchId])

  useEffect(() => {
    let active = true
    previewReportEmails(batchId)
      .then(items => { if (active) { setPreview(items); setSelected(new Set(items.filter(item => item.canSend).map(item => item.ruleId))) } })
      .catch(reason => { if (active) { setPreview([]); setPreviewError(message(reason, 'Unable to prepare the report emails.')) } })
    void loadDeliveries()
    return () => { active = false }
  }, [batchId, loadDeliveries])

  // While emails are waiting to be sent, keep their status up to date.
  const waiting = deliveries.some(delivery => delivery.status === 'Pending')
  useEffect(() => {
    if (!waiting) return
    const timer = window.setInterval(() => void loadDeliveries(), deliveryRefreshMs)
    return () => window.clearInterval(timer)
  }, [waiting, loadDeliveries])

  const toggle = (ruleId: string) => setSelected(current => { const next = new Set(current); if (next.has(ruleId)) next.delete(ruleId); else next.add(ruleId); return next })
  const send = async () => {
    setSending(true); setError(undefined)
    try { const result = await sendReportEmails(batchId, [...selected]); setQueued(result.length); await loadDeliveries() }
    catch (reason) { setError(message(reason, 'Unable to send the report emails.')) }
    finally { setSending(false) }
  }
  const resend = async (id: string) => {
    setError(undefined)
    try { await resendReportEmail(id); await loadDeliveries() } catch (reason) { setError(message(reason, 'Unable to resend the email.')) }
  }

  return <Dialog open onClose={onClose} fullWidth maxWidth="lg">
    <DialogTitle>Email Reports · {period}</DialogTitle>
    <DialogContent>
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        {queued > 0 && <Alert severity="success">{queued} {queued === 1 ? 'email is' : 'emails are'} queued and will be sent in the background. Their progress shows below.</Alert>}
        {!preview ? <LoadingState label="Preparing the report emails…" />
          : previewError ? <Alert severity="error">{previewError}</Alert>
          : preview.length === 0 ? <Alert severity="info">No active report distribution rules. Set them up in <Link component={RouterLink} to="/admin/report-distribution">Report Distribution</Link>.</Alert>
          : <TableContainer sx={{ overflowX: 'auto' }}><Table size="small" aria-label="Report emails to send">
            <TableHead><TableRow><TableCell padding="checkbox" /><TableCell>Rule</TableCell><TableCell>Factories</TableCell><TableCell>Categories</TableCell><TableCell align="right">Numbers</TableCell><TableCell>Recipients</TableCell></TableRow></TableHead>
            <TableBody>{preview.map(item => <TableRow key={item.ruleId}>
              <TableCell padding="checkbox"><Checkbox checked={selected.has(item.ruleId)} disabled={!item.canSend} onChange={() => toggle(item.ruleId)} slotProps={{ input: { 'aria-label': `Send ${item.ruleName}` } }} /></TableCell>
              <TableCell sx={{ fontWeight: 600 }}>{item.ruleName}{item.reason && <Typography variant="caption" color="warning.main" sx={{ display: 'block' }}>{item.reason}</Typography>}</TableCell>
              <TableCell>{item.factoryLabel}</TableCell>
              <TableCell>{item.categoryLabel}</TableCell>
              <TableCell align="right">{item.numberCount}</TableCell>
              <TableCell>{item.recipients.join(', ') || '—'}</TableCell>
            </TableRow>)}</TableBody>
          </Table></TableContainer>}

        <Typography variant="subtitle1" sx={{ fontWeight: 700, pt: 1 }}>Send history</Typography>
        {deliveries.length === 0 ? <Typography variant="body2" color="text.secondary">No report emails have been sent for this batch yet.</Typography>
          : <TableContainer sx={{ overflowX: 'auto' }}><Table size="small" aria-label="Report email history">
            <TableHead><TableRow><TableCell>Rule</TableCell><TableCell>Recipients</TableCell><TableCell>Status</TableCell><TableCell>Requested</TableCell><TableCell /></TableRow></TableHead>
            <TableBody>{deliveries.map(delivery => <TableRow key={delivery.id}>
              <TableCell>{delivery.ruleName}</TableCell>
              <TableCell sx={{ maxWidth: 320, wordBreak: 'break-word' }}>{delivery.recipients}</TableCell>
              <TableCell><DeliveryStatus delivery={delivery} /></TableCell>
              <TableCell>{formatDateTime(delivery.requestedAt)} · {delivery.requestedBy}</TableCell>
              <TableCell>{delivery.status !== 'Pending' && <Button size="small" onClick={() => void resend(delivery.id)}>Resend</Button>}</TableCell>
            </TableRow>)}</TableBody>
          </Table></TableContainer>}
      </Stack>
    </DialogContent>
    <DialogActions>
      <Button onClick={onClose}>Close</Button>
      <Button variant="contained" disabled={sending || selected.size === 0 || Boolean(previewError)} onClick={() => void send()}>
        {sending ? 'Sending…' : `Send ${selected.size} ${selected.size === 1 ? 'email' : 'emails'}`}
      </Button>
    </DialogActions>
  </Dialog>
}

export function DeliveryStatus({ delivery }: { delivery: ReportEmailDelivery }) {
  if (delivery.status === 'Sent') return <Stack spacing={0.25}><Chip size="small" color="success" label="Sent" sx={{ alignSelf: 'flex-start' }} /><Typography variant="caption" color="text.secondary">{formatDateTime(delivery.sentAt)}</Typography></Stack>
  if (delivery.status === 'Failed') return <Stack spacing={0.25}><Chip size="small" color="error" label="Failed" sx={{ alignSelf: 'flex-start' }} /><Typography variant="caption" color="error">{delivery.lastError}</Typography></Stack>
  return <Stack spacing={0.25}><Chip size="small" color="info" label={delivery.attempts > 0 ? 'Retrying' : 'Sending'} sx={{ alignSelf: 'flex-start' }} />{delivery.lastError && <Typography variant="caption" color="text.secondary">{delivery.lastError}</Typography>}</Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
