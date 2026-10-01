import { describe, expect, it } from 'vitest'

import { batchContinuationRoute, canDownloadReport } from './billingRoutes'

describe('billing routes', () => {
  it.each(['Draft', 'Uploaded', 'Parsed', 'ValidationFailed'])('continues %s batches in processing', status => {
    expect(batchContinuationRoute('batch-1', status)).toBe('/billing/batch-1/process')
  })

  it.each(['Validated', 'ITReview', 'HRApproval', 'FinanceApproval', 'Completed', 'Locked'])('continues %s batches in review', status => {
    expect(batchContinuationRoute('batch-1', status)).toBe('/billing/batch-1/review')
  })

  it('allows reports only for completed and locked batches', () => {
    expect(canDownloadReport('Validated')).toBe(false)
    expect(canDownloadReport('Completed')).toBe(true)
    expect(canDownloadReport('Locked')).toBe(true)
  })
})
