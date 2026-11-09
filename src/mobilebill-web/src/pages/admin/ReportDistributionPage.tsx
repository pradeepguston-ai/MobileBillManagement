import { Alert, Autocomplete, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Stack, Switch, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { useCallback, useEffect, useState } from 'react'

import {
  createReportDistributionRule, deleteReportDistributionRule, getReportDistributionRules, getReportRecipientOptions, updateReportDistributionRule,
  type ReportDistributionRule, type ReportRecipientOption,
} from '../../api/reportDistributionApi'
import { ReportCategorySelect, ReportFactorySelect } from '../../components/billing/ReportFactorySelect'
import { ConfirmActionDialog } from '../../components/common/ConfirmActionDialog'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { PageHeader } from '../../components/common/PageHeader'

// A recipient picked from the system users, or an email address typed in.
type Recipient = ReportRecipientOption | string
type Form = { id?: string; name: string; factoryCodes: string[]; categoryCodes: string[]; recipients: Recipient[]; isActive: boolean }
const emptyForm: Form = { name: '', factoryCodes: [], categoryCodes: [], recipients: [], isActive: true }
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Who receives the monthly bill report by email: each rule sends the report for its factories and categories
// (none chosen means all) to its system users and typed addresses. Reports are sent from Monthly Bill Report.
export function ReportDistributionPage() {
  const [rules, setRules] = useState<ReportDistributionRule[]>()
  const [options, setOptions] = useState<ReportRecipientOption[]>([])
  const [error, setError] = useState<string>()
  const [form, setForm] = useState<Form>()
  const [formError, setFormError] = useState<string>()
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState<ReportDistributionRule>()

  const load = useCallback(async () => {
    try {
      const [nextRules, nextOptions] = await Promise.all([getReportDistributionRules(), getReportRecipientOptions()])
      setRules(nextRules); setOptions(nextOptions); setError(undefined)
    } catch (reason) { setError(message(reason, 'Unable to load the report distribution rules.')) }
  }, [])
  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])

  const edit = (rule: ReportDistributionRule) => {
    setFormError(undefined)
    setForm({
      id: rule.id, name: rule.name, factoryCodes: rule.factoryCodes, categoryCodes: rule.categoryCodes, isActive: rule.isActive,
      // Users already on the rule stay listed even if they are no longer active (they are skipped when sending).
      recipients: rule.recipients.map(recipient => recipient.userId
        ? options.find(option => option.userId === recipient.userId) ?? { userId: recipient.userId, displayName: `${recipient.displayName ?? recipient.email} (inactive)`, email: recipient.email, role: 'Viewer' }
        : recipient.email),
    })
  }

  const save = async () => {
    if (!form) return
    const emails = form.recipients.filter((recipient): recipient is string => typeof recipient === 'string').map(email => email.trim())
    const invalid = emails.find(email => !emailPattern.test(email))
    if (!form.name.trim()) { setFormError('Enter a rule name.'); return }
    if (invalid) { setFormError(`'${invalid}' is not a valid email address.`); return }
    if (form.recipients.length === 0) { setFormError('Add at least one recipient: a system user or an email address.'); return }
    const request = {
      name: form.name.trim(), factoryCodes: form.factoryCodes, categoryCodes: form.categoryCodes, isActive: form.isActive, emails,
      userIds: form.recipients.filter((recipient): recipient is ReportRecipientOption => typeof recipient !== 'string').map(recipient => recipient.userId),
    }
    setSaving(true); setFormError(undefined)
    try {
      if (form.id) await updateReportDistributionRule(form.id, request); else await createReportDistributionRule(request)
      setForm(undefined); await load()
    } catch (reason) { setFormError(message(reason, 'Unable to save the rule.')) }
    finally { setSaving(false) }
  }

  const remove = async () => {
    if (!deleting) return
    try { await deleteReportDistributionRule(deleting.id); setDeleting(undefined); await load() }
    catch (reason) { setDeleting(undefined); setError(message(reason, 'Unable to delete the rule.')) }
  }

  return <Stack spacing={2}>
    <PageHeader title="Report Distribution" subtitle="Who receives the monthly bill report by email, for which factories and categories. Active rules are emailed automatically when a batch is locked, and can be sent any time from Monthly Bill Report."
      action={<Button variant="contained" startIcon={<AddIcon />} onClick={() => { setFormError(undefined); setForm(emptyForm) }}>Add Rule</Button>} />
    {error && <ErrorState message={error} onRetry={() => void load()} />}
    {!rules ? (!error && <LoadingState label="Loading rules…" />) : rules.length === 0
      ? <EmptyState message="No report distribution rules yet. Add a rule for each factory and the categories its recipients should receive." />
      : <TableContainer sx={{ overflowX: 'auto' }}><Table size="small" aria-label="Report distribution rules">
        <TableHead><TableRow><TableCell>Name</TableCell><TableCell>Factories</TableCell><TableCell>Categories</TableCell><TableCell>Recipients</TableCell><TableCell>Status</TableCell><TableCell>Actions</TableCell></TableRow></TableHead>
        <TableBody>{rules.map(rule => <TableRow key={rule.id} hover>
          <TableCell sx={{ fontWeight: 600 }}>{rule.name}</TableCell>
          <TableCell>{rule.factoryLabel}</TableCell>
          <TableCell>{rule.categoryLabel}</TableCell>
          <TableCell><Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>{rule.recipients.map(recipient =>
            <Chip key={recipient.userId ?? recipient.email} size="small" color={recipient.inactive ? 'warning' : 'default'} variant={recipient.userId ? 'filled' : 'outlined'}
              label={recipient.userId ? `${recipient.displayName}${recipient.inactive ? ' (inactive)' : ''}` : recipient.email} title={recipient.email} />)}</Box></TableCell>
          <TableCell><Chip size="small" color={rule.isActive ? 'success' : 'default'} label={rule.isActive ? 'Active' : 'Off'} /></TableCell>
          <TableCell><Stack direction="row" spacing={1}><Button size="small" onClick={() => edit(rule)}>Edit</Button><Button size="small" color="error" onClick={() => setDeleting(rule)}>Delete</Button></Stack></TableCell>
        </TableRow>)}</TableBody>
      </Table></TableContainer>}

    <Dialog open={Boolean(form)} onClose={() => setForm(undefined)} fullWidth maxWidth="sm">
      <DialogTitle>{form?.id ? 'Edit Rule' : 'Add Rule'}</DialogTitle>
      {form && <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {formError && <Alert severity="error">{formError}</Alert>}
          <TextField label="Rule name" required value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} helperText="Used in the email subject, for example “CAPL - Executive and Staff”." slotProps={{ htmlInput: { maxLength: 200 } }} />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
            <ReportFactorySelect value={form.factoryCodes} onChange={factoryCodes => setForm({ ...form, factoryCodes })} />
            <ReportCategorySelect value={form.categoryCodes} onChange={categoryCodes => setForm({ ...form, categoryCodes })} />
          </Stack>
          <Autocomplete<Recipient, true, false, true>
            multiple freeSolo
            options={options}
            value={form.recipients}
            onChange={(_, recipients) => setForm({ ...form, recipients: recipients.map(recipient => typeof recipient === 'string' ? recipient.trim() : recipient).filter(recipient => recipient !== '') })}
            getOptionLabel={recipient => typeof recipient === 'string' ? recipient : `${recipient.displayName} <${recipient.email}>`}
            isOptionEqualToValue={(option, value) => typeof option !== 'string' && typeof value !== 'string' && option.userId === value.userId}
            filterSelectedOptions
            renderInput={params => <TextField {...params} label="Recipients" placeholder="Pick a user or type an email" helperText="Pick system users, or type an email address and press Enter." />}
          />
          <FormControlLabel control={<Switch checked={form.isActive} onChange={event => setForm({ ...form, isActive: event.target.checked })} />} label="Active (included when sending reports)" />
          <Typography variant="body2" color="text.secondary">Each email carries the report as Excel and PDF, filtered to the chosen factories and categories. Leave a filter on “All” to include everything.</Typography>
        </Stack>
      </DialogContent>}
      <DialogActions><Button onClick={() => setForm(undefined)}>Cancel</Button><Button variant="contained" disabled={saving} onClick={() => void save()}>{saving ? 'Saving…' : 'Save'}</Button></DialogActions>
    </Dialog>

    <ConfirmActionDialog open={Boolean(deleting)} title="Delete rule" message={`Delete “${deleting?.name ?? ''}”? Reports already sent with it stay in the send history.`} confirmLabel="Delete" onCancel={() => setDeleting(undefined)} onConfirm={() => void remove()} />
  </Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
