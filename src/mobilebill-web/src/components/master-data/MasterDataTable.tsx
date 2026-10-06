import { Button, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material'
import type { ReactNode } from 'react'
import type { EntityRow } from '../../api/types'
import { StatusBadge } from '../common/StatusBadge'

export type Column<T extends EntityRow> = { key: keyof T & string; header: string; format?: (value: unknown, row: T) => string; render?: (row: T) => ReactNode }

// extraActions adds page-specific buttons (for example Release to Pool) after Edit and Deactivate.
export function MasterDataTable<T extends EntityRow>({ columns, rows, onEdit, onDeactivate, editLabel = 'Edit', canEditRow, extraActions }: { columns: Column<T>[]; rows: T[]; onEdit?: (row: T) => void; onDeactivate?: (row: T) => void; editLabel?: string; canEditRow?: (row: T) => boolean; extraActions?: (row: T) => ReactNode }) {
  const showActions = Boolean(onEdit || onDeactivate || extraActions)
  return <Table size="small" stickyHeader><TableHead><TableRow>{columns.map(column => <TableCell key={column.key}>{column.header}</TableCell>)}{showActions && <TableCell>Actions</TableCell>}</TableRow></TableHead><TableBody>{rows.map(row => <TableRow key={row.id} hover>{columns.map(column => <TableCell key={column.key}>{column.render ? column.render(row) : column.key === 'isActive' ? <StatusBadge label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'success' : 'neutral'} /> : column.format ? column.format(row[column.key], row) : String(row[column.key] ?? '')}</TableCell>)}{showActions && <TableCell sx={{ whiteSpace: 'nowrap' }}>{onEdit && <Button size="small" disabled={canEditRow ? !canEditRow(row) : false} onClick={() => onEdit(row)}>{editLabel}</Button>}{onDeactivate && <Button size="small" color="warning" disabled={!row.isActive} onClick={() => onDeactivate(row)}>Deactivate</Button>}{extraActions?.(row)}</TableCell>}</TableRow>)}</TableBody></Table>
}
