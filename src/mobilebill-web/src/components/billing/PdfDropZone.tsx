import { Box, Button, Stack, Typography } from '@mui/material'
import { useRef } from 'react'

type Props = { file?: File; disabled?: boolean; onChange: (file: File) => void }

export function PdfDropZone({ file, disabled, onChange }: Props) {
  const input = useRef<HTMLInputElement>(null)
  const select = (candidate?: File) => { if (candidate) onChange(candidate) }
  return <Box
    onDragOver={event => event.preventDefault()}
    onDrop={event => { event.preventDefault(); select(event.dataTransfer.files[0]) }}
    sx={{ border: '2px dashed', borderColor: 'divider', borderRadius: 2, p: 4, textAlign: 'center', bgcolor: 'background.default' }}
  >
    <input ref={input} hidden type="file" accept="application/pdf,.pdf" aria-label="Choose PDF file" onChange={event => select(event.target.files?.[0])} />
    <Stack spacing={1} sx={{ alignItems: 'center' }}>
      <Typography sx={{ fontWeight: 600 }}>Drop the monthly telecom PDF here</Typography>
      <Typography variant="body2" color="text.secondary">or browse from this computer</Typography>
      <Button variant="outlined" disabled={disabled} onClick={() => input.current?.click()}>Browse PDF</Button>
      {file && <Typography variant="body2">{file.name} · {formatFileSize(file.size)}</Typography>}
    </Stack>
  </Box>
}

function formatFileSize(bytes: number) {
  return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
