import { Box, Step, StepLabel, Stepper, Typography } from '@mui/material'

import { workflowStageLabel, workflowStatusIndex, workflowSteps } from '../../billing/workflowPresentation'

export function WorkflowStepper({ status, compact = false }: { status: string; compact?: boolean }) {
  const activeStep = workflowStatusIndex[status] ?? 0
  return <Box><Stepper activeStep={activeStep} alternativeLabel={!compact} sx={{ minWidth: compact ? 620 : undefined, overflowX: 'auto' }}>{workflowSteps.map(step => <Step key={step}><StepLabel>{step}</StepLabel></Step>)}</Stepper><Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Current stage: {workflowStageLabel(status)}</Typography></Box>
}
