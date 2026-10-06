import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'
import DownloadIcon from '@mui/icons-material/Download'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import { useState } from 'react'

import { downloadImportTemplate, importMasterData, type ImportResult } from '../../api/masterDataApi'

// Excel upload in two steps: Check file lists every problem with its Excel row number and saves nothing;
// Import saves the whole file in one go, and is offered only when the check found no problems.
export function ImportDialog({ open, title, path, onClose, onImported }: { open: boolean; title: string; path: string; onClose: () => void; onImported: () => void }) {
  const [file, setFile] = useState<File>()
  const [checked, setChecked] = useState<ImportResult>()
  const [imported, setImported] = useState<ImportResult>()
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)

  const reset = () => { setFile(undefined); setChecked(undefined); setImported(undefined); setError(undefined) }
  const close = () => { reset(); onClose() }
  const run = async (commit: boolean) => {
    if (!file) return
    setBusy(true); setError(undefined)
    try {
      const result = await importMasterData(path, file, commit)
      if (commit && result.committed) { setImported(result); onImported() } else setChecked(result)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to read the file.') }
    finally { setBusy(false) }
  }
  const template = async () => { try { await downloadImportTemplate(path) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to download the template.') } }
  const canImport = Boolean(checked && checked.errors.length === 0 && checked.newCount + checked.updatedCount > 0)

  return <Dialog open={open} onClose={close} fullWidth maxWidth="md" aria-labelledby="import-dialog-title">
    <DialogTitle id="import-dialog-title">{title}</DialogTitle>
    <DialogContent>
      <Stack spacing={2} sx={{ pt: 1 }}>
        {imported ? <Alert severity="success">Imported {imported.totalRows} rows: {imported.newCount} new, {imported.updatedCount} updated, {imported.unchangedCount} unchanged.</Alert> : <>
          <Typography variant="body2" color="text.secondary">Fill in the template, then check the file. Nothing is saved until you import, and a file with any problem is not saved at all.</Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' } }}>
            <Button variant="outlined" startIcon={<DownloadIcon />} onClick={() => void template()}>Download template</Button>
            <Button variant="outlined" component="label" startIcon={<UploadFileIcon />}>
              Choose Excel file
              <input hidden type="file" accept=".xlsx" aria-label="Excel file" onChange={event => { setFile(event.target.files?.[0]); setChecked(undefined); setError(undefined); event.target.value = '' }} />
            </Button>
            <Typography variant="body2">{file?.name ?? 'No file chosen'}</Typography>
          </Stack>
          {error && <Alert severity="error">{error}</Alert>}
          {checked && checked.errors.length === 0 && <Alert severity="success">{checked.totalRows} rows checked with no problems: {checked.newCount} new, {checked.updatedCount} to update, {checked.unchangedCount} unchanged.{!canImport && ' There is nothing to import.'}</Alert>}
          {checked && checked.errors.length > 0 && <>
            <Alert severity="error">{checked.errors.length} {checked.errors.length === 1 ? 'problem' : 'problems'} found in {checked.totalRows} rows. Fix them in the file and check again; nothing has been saved.</Alert>
            <Box sx={{ maxHeight: 320, overflow: 'auto' }}>
              <Table size="small" stickyHeader aria-label="Import problems">
                <TableHead><TableRow><TableCell sx={{ width: 90 }}>Row</TableCell><TableCell>Problem</TableCell></TableRow></TableHead>
                <TableBody>{checked.errors.map((item, index) => <TableRow key={`${item.row}-${index}`}><TableCell>{item.row}</TableCell><TableCell>{item.message}</TableCell></TableRow>)}</TableBody>
              </Table>
            </Box>
          </>}
        </>}
      </Stack>
    </DialogContent>
    <DialogActions>
      <Button onClick={close}>{imported ? 'Close' : 'Cancel'}</Button>
      {!imported && <Button variant="outlined" disabled={!file || busy} onClick={() => void run(false)}>{busy && !checked ? 'Checking…' : 'Check file'}</Button>}
      {!imported && <Button variant="contained" disabled={!canImport || busy} onClick={() => void run(true)}>{busy && checked ? 'Importing…' : `Import ${checked ? checked.newCount + checked.updatedCount : ''} rows`.replace('  ', ' ')}</Button>}
    </DialogActions>
  </Dialog>
}
