import { Box, Card, CardContent } from '@mui/material'
import type { ReactNode } from 'react'

export function AuthLayout({ children, maxWidth = 420 }: { children: ReactNode; maxWidth?: number }) {
  return <Box sx={{ display: 'flex', minHeight: '100vh', alignItems: 'center', justifyContent: 'center', p: 2 }}>
    <Card sx={{ maxWidth, width: '100%' }}>
      <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
        {children}
      </CardContent>
    </Card>
  </Box>
}
