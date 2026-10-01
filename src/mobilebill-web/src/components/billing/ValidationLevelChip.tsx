import { StatusBadge } from '../common/StatusBadge'

export function ValidationLevelChip({ level }: { level: string }) {
  const label = level === 'StructuralOnly' ? 'Structural only' : level === 'IndependentTotal' ? 'Independent total' : level
  const tone = level === 'IndependentTotal' ? 'success' : level === 'StructuralOnly' ? 'warning' : 'neutral'
  return <StatusBadge label={label} tone={tone} outlined={level === 'None'} />
}
