import { Checkbox, FormControl, InputLabel, ListItemText, MenuItem, Select } from '@mui/material'
import { useEffect, useId, useState } from 'react'

import { listMasterData } from '../../api/masterDataApi'
import type { EntityRow } from '../../api/types'

type MasterOption = EntityRow & { code?: string; name?: string }
type MultiSelectProps = { value: string[]; onChange: (codes: string[]) => void; disabled?: boolean }

// Narrows a report download to any number of master-data values, ticked in the list; none ticked means "all".
function ReportMultiSelect({ path, label, allLabel, value, onChange, disabled }: MultiSelectProps & { path: string; label: string; allLabel: string }) {
  const labelId = useId()
  const [options, setOptions] = useState<MasterOption[]>([])
  useEffect(() => {
    let active = true
    listMasterData<MasterOption>(path, { pageNumber: 1, pageSize: 100, search: '', isActive: true })
      .then(result => { if (active) setOptions((result.items ?? []).filter(option => option.code)) })
      .catch(() => { if (active) setOptions([]) })
    return () => { active = false }
  }, [path])
  const nameFor = (code: string) => options.find(option => option.code === code)?.name ?? code
  return <FormControl size="small" sx={{ minWidth: 240, maxWidth: 360 }} disabled={disabled}>
    <InputLabel id={labelId} shrink>{label}</InputLabel>
    <Select
      multiple
      displayEmpty
      notched
      labelId={labelId}
      label={label}
      value={value}
      onChange={event => {
        const next = event.target.value
        const codes = typeof next === 'string' ? next.split(',') : next
        // Picking the "All ..." entry clears every tick.
        onChange(codes.includes('') ? [] : codes)
      }}
      renderValue={selected => selected.length === 0 ? allLabel : selected.map(nameFor).join(', ')}
    >
      <MenuItem value="">
        <Checkbox size="small" checked={value.length === 0} />
        <ListItemText primary={allLabel} />
      </MenuItem>
      {options.map(option => {
        const code = String(option.code)
        return <MenuItem key={option.id} value={code}>
          <Checkbox size="small" checked={value.includes(code)} />
          <ListItemText primary={option.name ?? code} />
        </MenuItem>
      })}
    </Select>
  </FormControl>
}

export function ReportFactorySelect(props: MultiSelectProps) {
  return <ReportMultiSelect {...props} path="/api/factories" label="Report factory" allLabel="All factories" />
}

export function ReportCategorySelect(props: MultiSelectProps) {
  return <ReportMultiSelect {...props} path="/api/categories" label="Report category" allLabel="All categories" />
}

export function ReportSectionSelect(props: MultiSelectProps) {
  return <ReportMultiSelect {...props} path="/api/sections" label="Report section" allLabel="All sections" />
}
