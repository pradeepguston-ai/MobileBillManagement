import { Box, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'

import { roleLabels, type UserRole } from '../../api/authApi'
import type { ApprovalHistoryItem } from '../../api/billReviewApi'
import { workflowStageLabel } from '../../billing/workflowPresentation'
import { formatDateTime } from '../../utils/formatters'

export function ApprovalTimeline({ history }: { history: ApprovalHistoryItem[] }) {
  if (history.length === 0) return <Typography color="text.secondary">No approval history yet</Typography>
  return <Box sx={{ overflowX: 'auto' }}><Table size="small" aria-label="Approval history"><TableHead><TableRow><TableCell>Stage</TableCell><TableCell>Action</TableCell><TableCell>User</TableCell><TableCell>User Role</TableCell><TableCell>Timestamp</TableCell><TableCell>Comment</TableCell></TableRow></TableHead><TableBody>{history.map(item => <TableRow key={item.id}><TableCell>{workflowStageLabel(item.workflowStage)}</TableCell><TableCell>{item.action === 'ReturnForCorrection' ? 'Return for Correction' : item.action}</TableCell><TableCell title={item.userId}>{item.displayName}</TableCell><TableCell>{item.userRole ? roleLabels[item.userRole as UserRole] ?? item.userRole : '—'}</TableCell><TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(item.timestamp)}</TableCell><TableCell>{item.comment ?? '—'}</TableCell></TableRow>)}</TableBody></Table></Box>
}
