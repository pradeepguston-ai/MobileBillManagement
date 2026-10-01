import { List, ListItem, ListItemText, Typography } from '@mui/material'

import { formatDateTime } from '../../utils/formatters'

const isoTimestamp = /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})/g

export function AuditTimeline({ entries }: { entries: string[] }) {
  if (entries.length === 0) return <Typography color="text.secondary">No audit history yet</Typography>
  return <List dense disablePadding>{entries.map((entry, index) => <ListItem key={`${index}-${entry}`} divider><ListItemText primary={entry.replace(isoTimestamp, match => formatDateTime(match))} sx={{ whiteSpace: 'pre-wrap' }} /></ListItem>)}</List>
}
