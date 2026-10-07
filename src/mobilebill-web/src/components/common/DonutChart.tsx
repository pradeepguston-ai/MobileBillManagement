import { Box, Typography, useTheme } from '@mui/material'
import { useState } from 'react'

export type DonutSegment = { label: string; value: number; color: string }

const SIZE = 200
const CX = SIZE / 2
const CY = SIZE / 2
const R = 76
const STROKE = 26
const STROKE_HOVER = 30
// Gap between segments, in degrees.
const GAP = 2

function polarToXY(angle: number): [number, number] {
  const rad = ((angle - 90) * Math.PI) / 180
  return [CX + R * Math.cos(rad), CY + R * Math.sin(rad)]
}

function arcPath(start: number, end: number) {
  const [x1, y1] = polarToXY(start)
  const [x2, y2] = polarToXY(end)
  return `M ${x1} ${y1} A ${R} ${R} 0 ${end - start > 180 ? 1 : 0} 1 ${x2} ${y2}`
}

const compact = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 })

// Donut chart with the total in the centre. Hovering (or focusing) a segment or its legend row shows that
// segment's share in the centre and highlights it; the total comes back when the pointer leaves.
// Only positive values are drawn.
export function DonutChart({ data, centerLabel, ariaLabel, formatValue = value => value.toLocaleString('en-US'), formatTotal = value => compact.format(value) }: {
  data: DonutSegment[]
  centerLabel: string
  ariaLabel: string
  formatValue?: (value: number) => string
  formatTotal?: (value: number) => string
}) {
  const theme = useTheme()
  const [hoverLabel, setHoverLabel] = useState<string | null>(null)
  const positive = data.filter(item => item.value > 0)
  const total = positive.reduce((sum, item) => sum + item.value, 0)
  if (!total) return <Typography variant="body2" color="text.secondary" sx={{ py: 7, textAlign: 'center' }}>No data to show yet.</Typography>

  const gap = positive.length > 1 ? GAP : 0
  // Each segment starts where the ones before it end.
  const segments = positive.map((item, index) => {
    const cursor = positive.slice(0, index).reduce((sum, previous) => sum + (previous.value / total) * 360, 0)
    const sweep = (item.value / total) * 360
    const start = cursor + gap / 2
    return { ...item, start, end: Math.max(start, cursor + sweep - gap / 2), pct: Math.round((item.value / total) * 100) }
  })
  const hovered = segments.find(segment => segment.label === hoverLabel)
  const enter = (label: string) => () => setHoverLabel(label)
  const leave = (label: string) => () => setHoverLabel(current => (current === label ? null : current))

  return <Box sx={{ display: 'flex', flexDirection: { xs: 'column', sm: 'row' }, alignItems: 'center', gap: 2.5, height: '100%' }}>
    <Box component="svg" viewBox={`0 0 ${SIZE} ${SIZE}`} role="img" aria-label={ariaLabel} sx={{ width: { xs: 240, sm: 280 }, height: { xs: 240, sm: 280 }, flexShrink: 0 }}>
      {segments.map(segment => {
        const width = hoverLabel === segment.label ? STROKE_HOVER : STROKE
        const opacity = hoverLabel && hoverLabel !== segment.label ? 0.4 : 1
        const common = { fill: 'none', stroke: segment.color, strokeWidth: width, opacity, style: { transition: 'all 150ms', cursor: 'pointer' }, onMouseEnter: enter(segment.label), onMouseLeave: leave(segment.label) }
        const title = <title>{`${segment.label}: ${formatValue(segment.value)} (${segment.pct}%)`}</title>
        // A single segment is a full ring, which an arc cannot draw (its start and end points are the same).
        return segment.end - segment.start >= 359.99
          ? <circle key={segment.label} cx={CX} cy={CY} r={R} {...common}>{title}</circle>
          : <path key={segment.label} d={arcPath(segment.start, segment.end)} strokeLinecap="round" {...common}>{title}</path>
      })}
      <text x={CX} y={CY - 4} textAnchor="middle" fontSize={hovered ? 20 : 22} fontWeight={700} fill={hovered ? hovered.color : theme.palette.text.primary}>
        {hovered ? `${hovered.pct}%` : formatTotal(total)}
      </text>
      <text x={CX} y={CY + 15} textAnchor="middle" fontSize={hovered ? 9.5 : 10} fill={theme.palette.text.secondary}>
        {hovered ? (hovered.label.length > 22 ? `${hovered.label.slice(0, 21)}…` : hovered.label) : centerLabel}
      </text>
    </Box>
    <Box component="ul" aria-label={`${ariaLabel} legend`} sx={{ listStyle: 'none', m: 0, p: 0, display: 'flex', flexDirection: 'column', gap: 0.75, minWidth: 0 }}>
      {segments.map(segment => <Box component="li" key={segment.label} tabIndex={0}
        onMouseEnter={enter(segment.label)} onMouseLeave={leave(segment.label)} onFocus={enter(segment.label)} onBlur={leave(segment.label)}
        sx={{ display: 'flex', alignItems: 'center', gap: 1, borderRadius: 1.5, px: 0.75, py: 0.25, mx: -0.75, cursor: 'pointer', fontSize: 14, transition: 'background-color 150ms', bgcolor: hoverLabel === segment.label ? 'action.hover' : 'transparent', outline: 'none', '&:focus-visible': { boxShadow: `0 0 0 2px ${theme.palette.primary.main}` } }}>
        <Box component="span" sx={{ width: 10, height: 10, borderRadius: 0.5, flexShrink: 0, bgcolor: segment.color }} />
        <Box component="span" sx={{ color: 'text.primary', fontWeight: hoverLabel === segment.label ? 700 : 400 }}>{segment.label}</Box>
        <Box component="span" sx={{ color: 'text.secondary', whiteSpace: 'nowrap' }}>{formatValue(segment.value)} ({segment.pct}%)</Box>
      </Box>)}
    </Box>
  </Box>
}
