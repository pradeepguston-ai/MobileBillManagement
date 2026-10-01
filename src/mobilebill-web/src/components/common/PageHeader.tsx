import { Box, Stack, Typography } from '@mui/material'
import type { ReactNode } from 'react'

import { brandGradients } from '../../theme/theme'

export function PageHeader({ title, subtitle, action }: { title: string; subtitle?: ReactNode; action?: ReactNode }) {
  return <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between' }}>
    <Box>
      <Typography component="h1" variant="h4" sx={{ position: 'relative', pl: 1.5, '&::before': { content: '""', position: 'absolute', left: 0, top: 4, bottom: 4, width: 4, borderRadius: 1, backgroundImage: brandGradients.accentVertical } }}>{title}</Typography>
      {subtitle && <Typography color="text.secondary" sx={{ pl: 1.5 }}>{subtitle}</Typography>}
    </Box>
    {action}
  </Stack>
}
