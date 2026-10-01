import { Alert, Box, Card, CardContent, Stack, Typography } from '@mui/material'

import type { BillBatch } from '../../api/billingApi'
import { formatCurrency } from '../../utils/formatters'
import { StatusBadge } from '../common/StatusBadge'

// Compares the "Total Due" printed on the PDF's first page with the sum of every parsed account row.
// All three figures come from the server; this card only presents them.
export function TotalTallyCard({ batch }: { batch: BillBatch }) {
  const pdfTotal = batch.statedGrandTotal
  const difference = batch.difference
  const tallies = pdfTotal != null && difference === 0
  const status = pdfTotal == null
    ? <StatusBadge label="PDF total not read" tone="warning" />
    : tallies ? <StatusBadge label="Totals tally" tone="success" /> : <StatusBadge label="Totals do not tally" tone="error" />

  return <Card variant="outlined" aria-label="PDF total check">
    <CardContent>
      <Stack spacing={2}>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', rowGap: 1 }}>
          <Typography variant="h6">PDF Total Check</Typography>
          {status}
        </Stack>
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, minmax(0, 1fr))' }, gap: 2 }}>
          <Figure label="Total Due (PDF page 1)" value={pdfTotal == null ? 'Not read' : formatCurrency(pdfTotal)} />
          <Figure label="Calculated grand total (account rows)" value={formatCurrency(batch.calculatedGrandTotal)} />
          <Figure label="Difference" value={difference == null ? '—' : formatCurrency(difference)} tone={difference == null ? undefined : difference === 0 ? 'success.main' : 'error.main'} />
        </Box>
        {pdfTotal == null && <Alert severity="info">The Total Due on the first page of this PDF could not be read, so the totals can't be compared. Check the PDF manually before submitting the batch.</Alert>}
        {pdfTotal != null && !tallies && <Alert severity="error">The account rows do not add up to the Total Due printed on the PDF. Validation will fail until the difference is resolved.</Alert>}
      </Stack>
    </CardContent>
  </Card>
}

function Figure({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 2, p: 2 }}>
    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>{label}</Typography>
    <Typography variant="h6" sx={{ color: tone }}>{value}</Typography>
  </Box>
}
