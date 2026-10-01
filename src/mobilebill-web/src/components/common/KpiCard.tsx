import { Card, CardContent, Typography } from '@mui/material'
import type { ReactNode } from 'react'

import { kpiColors } from '../../theme/theme'

// Sibling KPI cards cycle through the brand colours (light, dark) for their numbers.
const valueColors = Object.fromEntries(kpiColors.map(([light], index) => [`&:nth-of-type(4n+${index + 1}) .kpi-value`, { color: light }]))
const darkValueColors = Object.fromEntries(kpiColors.map(([, dark], index) => [`&:nth-of-type(4n+${index + 1}) .kpi-value`, { color: dark }]))

export function KpiCard({ label, value, accentColor }: { label: string; value: ReactNode; accentColor?: string }) {
  return <Card sx={theme => ({
    transition: 'transform .2s ease, box-shadow .2s ease',
    '&:hover': { transform: 'translateY(-2px)', boxShadow: '0 8px 24px rgba(15,23,42,0.08)' },
    ...valueColors,
    ...theme.applyStyles('dark', darkValueColors),
    ...(accentColor ? { borderLeft: 4, borderLeftColor: accentColor } : {}),
  })}>
    <CardContent>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>{label}</Typography>
      <Typography variant="h6" className="kpi-value">{value}</Typography>
    </CardContent>
  </Card>
}
