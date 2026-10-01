import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material'

export function ConfirmActionDialog({ open, title, message, confirmLabel = 'Confirm', busy = false, onCancel, onConfirm }: { open: boolean; title: string; message: string; confirmLabel?: string; busy?: boolean; onCancel: () => void; onConfirm: () => void }) {
  return <Dialog open={open} onClose={onCancel}><DialogTitle>{title}</DialogTitle><DialogContent><DialogContentText>{message}</DialogContentText></DialogContent><DialogActions><Button onClick={onCancel}>Cancel</Button><Button variant="contained" color="warning" disabled={busy} onClick={onConfirm}>{confirmLabel}</Button></DialogActions></Dialog>
}
