import { Button, MenuItem, Stack, TextField } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import UploadFileIcon from '@mui/icons-material/UploadFile'

import { FilterBar } from '../common/FilterBar'

// An extra drop-down filter, such as Factory or Department; "" means all.
export type ToolbarFilter = { key: string; label: string; value: string; options: { value: string; label: string }[]; onChange: (value: string) => void }

export function MasterDataToolbar({ search, onSearchChange, isActive, onIsActiveChange, onCreate, onImport, filters = [] }: { search: string; onSearchChange: (value: string) => void; isActive: string; onIsActiveChange: (value: string) => void; onCreate?: () => void; onImport?: () => void; filters?: ToolbarFilter[] }) {
  const activeCount = (search.trim() !== '' ? 1 : 0) + (isActive !== '' ? 1 : 0) + filters.filter(filter => filter.value !== '').length
  const clear = () => { onSearchChange(''); onIsActiveChange(''); filters.forEach(filter => filter.onChange('')) }
  return <FilterBar activeCount={activeCount} onClear={clear}>
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ flexGrow: 1, flexWrap: 'wrap', rowGap: 2 }}>
      <TextField size="small" label="Search" value={search} onChange={event => onSearchChange(event.target.value)} />
      {filters.map(filter => <TextField key={filter.key} select size="small" label={filter.label} value={filter.value} onChange={event => filter.onChange(event.target.value)} sx={{ minWidth: 200, maxWidth: 320 }}>
        <MenuItem value="">All</MenuItem>
        {filter.options.map(option => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}
      </TextField>)}
      <TextField select size="small" label="Status" value={isActive} onChange={event => onIsActiveChange(event.target.value)} sx={{ minWidth: 150 }}><MenuItem value="">All</MenuItem><MenuItem value="true">Active</MenuItem><MenuItem value="false">Inactive</MenuItem></TextField>
    </Stack>
    {onImport && <Button variant="outlined" onClick={onImport} startIcon={<UploadFileIcon />}>Import from Excel</Button>}
    {onCreate && <Button variant="contained" onClick={onCreate} startIcon={<AddIcon />}>Create</Button>}
  </FilterBar>
}
