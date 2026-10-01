import { List, ListItem, ListItemText, Typography } from '@mui/material'

export function AuditTimeline({ entries }: { entries: string[] }) {
  if (entries.length === 0) return <Typography color="text.secondary">No audit history yet</Typography>
  return <List dense disablePadding>{entries.map((entry, index) => <ListItem key={`${index}-${entry}`} divider><ListItemText primary={entry} sx={{ whiteSpace: 'pre-wrap' }} /></ListItem>)}</List>
}
