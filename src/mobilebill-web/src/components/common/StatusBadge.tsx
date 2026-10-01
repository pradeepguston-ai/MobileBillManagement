import { Chip } from '@mui/material'
import { alpha, useTheme } from '@mui/material/styles'

export type StatusTone = 'success' | 'warning' | 'error' | 'info' | 'neutral'

export function StatusBadge({ label, tone, outlined = false }: { label: string; tone: StatusTone; outlined?: boolean }) {
  const theme = useTheme()
  const mainColor = tone === 'success' ? theme.palette.success.main
    : tone === 'warning' ? theme.palette.warning.main
    : tone === 'error' ? theme.palette.error.main
    : tone === 'info' ? theme.palette.primary.main
    : theme.palette.text.secondary
  return <Chip
    label={label}
    size="small"
    variant={outlined ? 'outlined' : 'filled'}
    sx={{
      bgcolor: outlined ? 'transparent' : alpha(mainColor, 0.12),
      color: mainColor,
      borderColor: outlined ? alpha(mainColor, 0.4) : undefined,
    }}
  />
}
