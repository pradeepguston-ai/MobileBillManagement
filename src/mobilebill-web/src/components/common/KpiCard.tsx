import { Card, CardContent, Typography } from '@mui/material'
import type { ReactNode } from 'react'

export function KpiCard({ label, value, accentColor }: { label: string; value: ReactNode; accentColor?: string }) {
  return <Card sx={accentColor ? { borderLeft: 4, borderLeftColor: accentColor } : undefined}>
    <CardContent>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>{label}</Typography>
      <Typography variant="h6">{value}</Typography>
    </CardContent>
  </Card>
}
