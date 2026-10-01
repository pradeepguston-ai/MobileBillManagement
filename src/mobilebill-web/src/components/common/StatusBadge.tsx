import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlineOutlined'
import { Chip } from '@mui/material'
import type { Theme } from '@mui/material/styles'

export type StatusTone = 'success' | 'warning' | 'error' | 'info' | 'neutral'

function toneColor(theme: Theme, tone: StatusTone) {
  const palette = (theme.vars ?? theme).palette
  return tone === 'success' ? palette.success.main
    : tone === 'warning' ? palette.warning.main
    : tone === 'error' ? palette.error.main
    : tone === 'info' ? palette.info.main
    : palette.text.secondary
}

// Errors also carry an icon so they are never told apart from primary (red) actions by colour alone.
export function StatusBadge({ label, tone, outlined = false }: { label: string; tone: StatusTone; outlined?: boolean }) {
  return <Chip
    label={label}
    size="small"
    variant={outlined ? 'outlined' : 'filled'}
    icon={tone === 'error' ? <ErrorOutlineIcon /> : undefined}
    sx={theme => {
      const color = toneColor(theme, tone)
      return {
        bgcolor: outlined ? 'transparent' : theme.alpha(color, 0.12),
        color,
        borderColor: outlined ? theme.alpha(color, 0.4) : undefined,
        '& .MuiChip-icon': { color: 'inherit' },
      }
    }}
  />
}
