import { Button, FormControl, InputLabel, MenuItem, Select, Stack, TextField } from '@mui/material'
import { type FormEvent, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import { createBatch } from '../../api/billingApi'
import { listMasterData } from '../../api/masterDataApi'
import type { EntityRow } from '../../api/types'
import { ErrorState } from '../../components/common/ErrorState'
import { PageHeader } from '../../components/common/PageHeader'

type Provider = EntityRow & { name?: string; code?: string }

// Dialog is the provider for almost every batch, so it is pre-selected; another provider can still be chosen.
const isDialog = (provider: Provider) => String(provider.code ?? '').toUpperCase() === 'DIALOG' || /^dialog\b/i.test(String(provider.name ?? ''))

const defaultCorporateCode = 'PR48799679'

export function CreateBillingBatchPage() {
  const navigate = useNavigate()
  const today = new Date()
  const [providers, setProviders] = useState<Provider[]>([])
  const [providerId, setProviderId] = useState('')
  // Pre-filled with the company's usual corporate account; it can still be changed for a different account.
  const [corporateCode, setCorporateCode] = useState(defaultCorporateCode)
  const [billingMonth, setBillingMonth] = useState(today.getMonth() + 1)
  const [billingYear, setBillingYear] = useState(today.getFullYear())
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => { void listMasterData<Provider>('/api/providers', { pageNumber: 1, pageSize: 100, search: '', isActive: true }).then(result => { setProviders(result.items); const dialog = result.items.find(isDialog); if (dialog) setProviderId(current => current || dialog.id) }).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load providers.')) }, [])
  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!providerId || !corporateCode.trim() || billingMonth < 1 || billingMonth > 12 || billingYear < 2000) { setError('Complete all required billing batch fields.'); return }
    setSaving(true); setError(undefined)
    try {
      const batch = await createBatch({ providerId, corporateCode: corporateCode.trim(), billingMonth, billingYear })
      navigate(`/billing/${batch.id}/process`)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create billing batch.') }
    finally { setSaving(false) }
  }

  return <Stack spacing={2} sx={{ maxWidth: 720 }} component="form" onSubmit={event => void submit(event)}>
    <PageHeader title="New Billing Batch" subtitle="Create the monthly batch before uploading its source PDF." />
    {error && <ErrorState message={error} />}
    <FormControl required fullWidth><InputLabel id="provider-label">Provider</InputLabel><Select labelId="provider-label" label="Provider" value={providerId} onChange={event => setProviderId(event.target.value)}>{providers.map(provider => <MenuItem key={provider.id} value={provider.id}>{provider.name ?? provider.id}</MenuItem>)}</Select></FormControl>
    <TextField required label="Corporate Code" value={corporateCode} onChange={event => setCorporateCode(event.target.value)} />
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
      <FormControl required fullWidth><InputLabel id="month-label">Billing Month</InputLabel><Select labelId="month-label" label="Billing Month" value={billingMonth} onChange={event => setBillingMonth(Number(event.target.value))}>{Array.from({ length: 12 }, (_, index) => <MenuItem key={index + 1} value={index + 1}>{new Intl.DateTimeFormat('en', { month: 'long' }).format(new Date(2020, index, 1))}</MenuItem>)}</Select></FormControl>
      <TextField required fullWidth label="Billing Year" type="number" value={billingYear} onChange={event => setBillingYear(Number(event.target.value))} slotProps={{ htmlInput: { min: 2000, max: 9999 } }} />
    </Stack>
    <Stack direction="row" spacing={1}><Button type="submit" variant="contained" disabled={saving}>{saving ? 'Creating…' : 'Create Batch'}</Button><Button onClick={() => navigate('/billing')} disabled={saving}>Cancel</Button></Stack>
  </Stack>
}
