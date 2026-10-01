import { workflowStageLabel } from '../../billing/workflowPresentation'
import { StatusBadge } from '../common/StatusBadge'

export function BatchStatusChip({ status }: { status: string }) {
  const tone = status === 'Locked' || status === 'Completed' || status === 'Validated'
    ? 'success'
    : status === 'ValidationFailed'
      ? 'error'
      : status === 'Draft'
        ? 'neutral'
        : 'info'
  return <StatusBadge label={workflowStageLabel(status)} tone={tone} />
}
