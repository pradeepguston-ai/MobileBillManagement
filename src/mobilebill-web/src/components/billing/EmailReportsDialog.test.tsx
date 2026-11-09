import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { EmailReportsDialog } from './EmailReportsDialog'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const preview = [
  { ruleId: 'r1', ruleName: 'CAPL - Staff', factoryLabel: 'Concord Apparel', categoryLabel: 'Staff', recipients: ['hr@concord.lk', 'manager@concord.lk'], numberCount: 42, canSend: true, reason: null },
  { ruleId: 'r2', ruleName: 'CAPL - Workers', factoryLabel: 'Concord Apparel', categoryLabel: 'Workers', recipients: ['w@concord.lk'], numberCount: 7, canSend: true, reason: null },
  { ruleId: 'r3', ruleName: 'CKL - Executives', factoryLabel: 'Concord Knit', categoryLabel: 'Executive', recipients: ['ckl@concord.lk'], numberCount: 0, canSend: false, reason: 'No numbers match this rule in the batch.' },
]
const sent = { id: 'd1', ruleId: 'r1', ruleName: 'CAPL - Staff', recipients: 'hr@concord.lk; manager@concord.lk', status: 'Sent', attempts: 1, requestedAt: '2026-11-09T04:00:00Z', requestedBy: 'Pradeep', sentAt: '2026-11-09T04:00:05Z', lastError: null }
const failed = { ...sent, id: 'd2', ruleName: 'CAPL - Workers', status: 'Failed', sentAt: null, lastError: 'The attachments are 3.1 MB; an email can carry at most 2.8 MB.' }

function renderDialog() { return render(<MemoryRouter><EmailReportsDialog batchId="b1" period="October 2026" onClose={() => {}} /></MemoryRouter>) }

describe('Email Reports', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('previews each rule, sends the ticked ones and shows the history with resend for failures', async () => {
    const posts: Array<{ url: string; body: unknown }> = []
    let history: unknown[] = [failed]
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST') { posts.push({ url, body: init.body ? JSON.parse(String(init.body)) : undefined }); history = [sent, failed]; return json(url.includes('/resend') ? failed : [sent]) }
      if (url.includes('/preview')) return json(preview)
      return json(history)
    })
    renderDialog()

    const table = await screen.findByRole('table', { name: 'Report emails to send' })
    expect(within(table).getByText('No numbers match this rule in the batch.')).toBeTruthy()
    expect((within(table).getByRole('checkbox', { name: 'Send CKL - Executives' }) as HTMLInputElement).disabled).toBe(true)
    expect(within(table).getByText('hr@concord.lk, manager@concord.lk')).toBeTruthy()
    // Every rule that can be sent starts ticked; untick one.
    fireEvent.click(within(table).getByRole('checkbox', { name: 'Send CAPL - Workers' }))
    fireEvent.click(screen.getByRole('button', { name: 'Send 1 email' }))

    await waitFor(() => expect(posts).toEqual([{ url: expect.stringContaining('/api/report-distribution/batches/b1/send'), body: { ruleIds: ['r1'] } }]))
    expect(await screen.findByText(/1 email is queued/)).toBeTruthy()
    const historyTable = await screen.findByRole('table', { name: 'Report email history' })
    await waitFor(() => expect(within(historyTable).getByText('Sent')).toBeTruthy())
    expect(within(historyTable).getByText(/The attachments are 3.1 MB/)).toBeTruthy()

    fireEvent.click(within(historyTable).getAllByRole('button', { name: 'Resend' })[1])
    await waitFor(() => expect(posts[1]).toEqual({ url: expect.stringContaining('/api/report-distribution/deliveries/d2/resend'), body: undefined }))
  })

  it('explains why the reports cannot be emailed yet', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/preview')
      ? json({ title: 'Monthly bill for mobile 0771000001 has not been assessed.' }, 409)
      : json([]))
    renderDialog()

    expect(await screen.findByText('Monthly bill for mobile 0771000001 has not been assessed.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Send 0 emails' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('points to Report Distribution when there are no active rules', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json([]))
    renderDialog()

    expect((await screen.findByRole('link', { name: 'Report Distribution' })).getAttribute('href')).toBe('/admin/report-distribution')
    expect(screen.getByText('No report emails have been sent for this batch yet.')).toBeTruthy()
  })
})
