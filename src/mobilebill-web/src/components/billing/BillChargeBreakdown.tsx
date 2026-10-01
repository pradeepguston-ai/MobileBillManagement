import { Box, Typography } from '@mui/material'

import type { ChargeBreakdown } from '../../api/billReviewApi'
import { CurrencyDisplay } from './CurrencyDisplay'

const fields: Array<[keyof ChargeBreakdown, string]> = [
  ['previousDue', 'Previous Due'], ['payments', 'Payments'], ['totalUsage', 'Total Usage'], ['idd', 'IDD'], ['roaming', 'Roaming'], ['vas', 'VAS'], ['discounts', 'Discounts'], ['billAdjustments', 'Bill Adjustments'], ['commitmentCharges', 'Commitment Charges'], ['latePaymentCharges', 'Late Payment Charges'], ['addToBill', 'Add To Bill'], ['instalmentPlans', 'Instalment Plans'], ['governmentTaxesLevies', 'Government Taxes / Levies'], ['vat', 'VAT'], ['chargesForBillPeriod', 'Charges For Bill Period'], ['totalDueAmount', 'Total Due'],
]

export function BillChargeBreakdown({ charges }: { charges: ChargeBreakdown }) {
  return <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 1 }}>{fields.map(([key, label]) => <Box key={key} sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, py: 0.5, borderBottom: 1, borderColor: 'divider' }}><Typography variant="body2" color="text.secondary">{label}</Typography><Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}><CurrencyDisplay value={charges[key]} /></Typography></Box>)}</Box>
}
