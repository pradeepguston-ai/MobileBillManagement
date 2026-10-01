import { FormControl, InputLabel, MenuItem, Select } from '@mui/material'
import { ThemeProvider } from '@mui/material/styles'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { theme } from './theme'

describe('select menus', () => {
  afterEach(cleanup)

  it('open below the field so the opening click cannot pick an option', () => {
    expect(theme.components?.MuiSelect?.defaultProps?.MenuProps).toMatchObject({
      anchorOrigin: { vertical: 'bottom', horizontal: 'left' },
      transformOrigin: { vertical: 'top', horizontal: 'left' },
    })
  })

  it('keeps the current value until an option is actually clicked', () => {
    const onChange = vi.fn()
    render(<ThemeProvider theme={theme}><FormControl><InputLabel id="d">Department</InputLabel>
      <Select labelId="d" label="Department" value="IT" onChange={onChange}>
        <MenuItem value="">All departments</MenuItem><MenuItem value="IT">IT</MenuItem><MenuItem value="HR">HR</MenuItem>
      </Select></FormControl></ThemeProvider>)

    const field = screen.getByRole('combobox', { name: 'Department' })
    fireEvent.mouseDown(field)
    fireEvent.mouseUp(field)
    expect(screen.getByRole('listbox')).toBeTruthy()
    expect(onChange).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('option', { name: 'HR' }))
    expect(onChange).toHaveBeenCalledTimes(1)
  })
})
