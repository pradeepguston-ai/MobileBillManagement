import { Button, MenuItem, Stack, TextField } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'

import { FilterBar } from '../common/FilterBar'

export function MasterDataToolbar({ search, onSearchChange, isActive, onIsActiveChange, onCreate }: { search: string; onSearchChange: (value: string) => void; isActive: string; onIsActiveChange: (value: string) => void; onCreate: () => void }) {
  const activeCount = (search.trim() !== '' ? 1 : 0) + (isActive !== '' ? 1 : 0)
  const clear = () => { onSearchChange(''); onIsActiveChange('') }
  return <FilterBar activeCount={activeCount} onClear={clear}>
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ flexGrow: 1, flexWrap: 'wrap' }}>
      <TextField size="small" label="Search" value={search} onChange={event => onSearchChange(event.target.value)} />
      <TextField select size="small" label="Status" value={isActive} onChange={event => onIsActiveChange(event.target.value)} sx={{ minWidth: 150 }}><MenuItem value="">All</MenuItem><MenuItem value="true">Active</MenuItem><MenuItem value="false">Inactive</MenuItem></TextField>
    </Stack>
    <Button variant="contained" onClick={onCreate} startIcon={<AddIcon />}>Create</Button>
  </FilterBar>
}
