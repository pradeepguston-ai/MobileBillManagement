import { Box, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'

import type { ApprovalHistoryItem } from '../../api/billReviewApi'
import { workflowStageLabel } from '../../billing/workflowPresentation'
import { formatDateTime } from '../../utils/formatters'

export function ApprovalTimeline({ history }: { history: ApprovalHistoryItem[] }) {
  if (history.length === 0) return <Typography color="text.secondary">No approval history yet</Typography>
  return <Box sx={{ overflowX: 'auto' }}><Table size="small" aria-label="Approval history"><TableHead><TableRow><TableCell>Stage</TableCell><TableCell>Action</TableCell><TableCell>Workflow Role</TableCell><TableCell>User</TableCell><TableCell>Timestamp</TableCell><TableCell>Comment</TableCell></TableRow></TableHead><TableBody>{history.map(item => <TableRow key={item.id}><TableCell>{workflowStageLabel(item.workflowStage)}</TableCell><TableCell>{item.action === 'ReturnForCorrection' ? 'Return for Correction' : item.action}</TableCell><TableCell>{item.workflowRole}</TableCell><TableCell title={item.userId}>{item.displayName}</TableCell><TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(item.timestamp)}</TableCell><TableCell>{item.comment ?? '—'}</TableCell></TableRow>)}</TableBody></Table></Box>
}
