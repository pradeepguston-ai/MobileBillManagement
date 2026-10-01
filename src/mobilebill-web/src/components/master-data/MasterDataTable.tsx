import { Button, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material'
import type { EntityRow } from '../../api/types'
import { StatusBadge } from '../common/StatusBadge'

export type Column<T extends EntityRow> = { key: keyof T & string; header: string; format?: (value: unknown, row: T) => string }

export function MasterDataTable<T extends EntityRow>({ columns, rows, onEdit, onDeactivate }: { columns: Column<T>[]; rows: T[]; onEdit: (row: T) => void; onDeactivate: (row: T) => void }) {
  return <Table size="small" stickyHeader><TableHead><TableRow>{columns.map(column => <TableCell key={column.key}>{column.header}</TableCell>)}<TableCell>Actions</TableCell></TableRow></TableHead><TableBody>{rows.map(row => <TableRow key={row.id} hover>{columns.map(column => <TableCell key={column.key}>{column.key === 'isActive' ? <StatusBadge label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'success' : 'neutral'} /> : column.format ? column.format(row[column.key], row) : String(row[column.key] ?? '')}</TableCell>)}<TableCell><Button size="small" onClick={() => onEdit(row)}>Edit</Button><Button size="small" color="warning" disabled={!row.isActive} onClick={() => onDeactivate(row)}>Deactivate</Button></TableCell></TableRow>)}</TableBody></Table>
}
