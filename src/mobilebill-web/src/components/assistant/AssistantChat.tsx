import { Alert, Box, Chip, CircularProgress, Drawer, Fab, IconButton, Stack, TextField, Tooltip, Typography } from '@mui/material'
import AddCommentOutlinedIcon from '@mui/icons-material/AddCommentOutlined'
import CloseIcon from '@mui/icons-material/Close'
import SendIcon from '@mui/icons-material/Send'
import SmartToyOutlinedIcon from '@mui/icons-material/SmartToyOutlined'
import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'

import { askAssistant, getAssistantStatus } from '../../api/assistantApi'

type Message = { role: 'user' | 'assistant'; text: string }

const maxLength = 2000
const suggestions = [
  'Summarise the latest billing batch',
  'Who went over their limit the most this month?',
  'Which numbers keep going over their limit?',
  'Which devices still need to be collected?',
]

// The AI assistant: a button at the bottom right that opens a chat panel. It shows only when the server has an
// AI model configured. The conversation lives on the server; this keeps what is shown and the conversation id.
export function AssistantChat() {
  const [enabled, setEnabled] = useState(false)
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<Message[]>([])
  const [conversationId, setConversationId] = useState<string>()
  const [draft, setDraft] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string>()
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    let active = true
    getAssistantStatus().then(status => { if (active) setEnabled(status.isEnabled) }).catch(() => { if (active) setEnabled(false) })
    return () => { active = false }
  }, [])

  useEffect(() => { endRef.current?.scrollIntoView?.({ block: 'end' }) }, [messages, sending, error])

  if (!enabled) return null

  async function send(text: string) {
    const question = text.trim()
    if (!question || sending) return
    setMessages(current => [...current, { role: 'user', text: question }])
    setDraft(''); setError(undefined); setSending(true)
    try {
      const reply = await askAssistant(question, conversationId)
      setConversationId(reply.conversationId)
      setMessages(current => [...current, { role: 'assistant', text: reply.reply }])
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'The assistant could not answer.')
    } finally { setSending(false) }
  }

  function newChat() { setMessages([]); setConversationId(undefined); setError(undefined); setDraft('') }

  function onSubmit(event: FormEvent) { event.preventDefault(); void send(draft) }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void send(draft) }
  }

  return <>
    {!open && <Tooltip title="Ask the assistant" placement="left">
      <Fab color="primary" aria-label="Open assistant" onClick={() => setOpen(true)} sx={{ position: 'fixed', right: { xs: 16, sm: 24 }, bottom: { xs: 16, sm: 24 }, zIndex: theme => theme.zIndex.drawer + 2 }}>
        <SmartToyOutlinedIcon />
      </Fab>
    </Tooltip>}
    {/* Above the app bar (drawer + 1), which would otherwise cover the panel's header. */}
    <Drawer anchor="right" open={open} onClose={() => setOpen(false)} sx={{ zIndex: theme => theme.zIndex.modal }} slotProps={{ paper: { sx: { width: { xs: '100%', sm: 420 }, display: 'flex', flexDirection: 'column' } } }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', px: 2, py: 1.5, borderBottom: 1, borderColor: 'divider' }}>
        <SmartToyOutlinedIcon color="primary" />
        <Box sx={{ flexGrow: 1 }}>
          <Typography component="h2" variant="h6" sx={{ lineHeight: 1.2 }}>AI Assistant</Typography>
          <Typography variant="caption" color="text.secondary">AI powered help assistant for your  support</Typography>
        </Box>
        <Tooltip title="New chat"><span><IconButton aria-label="New chat" onClick={newChat} disabled={sending || messages.length === 0}><AddCommentOutlinedIcon /></IconButton></span></Tooltip>
        <IconButton aria-label="Close assistant" onClick={() => setOpen(false)}><CloseIcon /></IconButton>
      </Stack>

      <Box role="log" aria-label="Conversation" aria-live="polite" sx={{ flexGrow: 1, overflowY: 'auto', px: 2, py: 2, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        {messages.length === 0 && <Stack spacing={1.5}>
          <Typography variant="body2" color="text.secondary">Ask about bills, excess and deductions, mobile allocations, the SIM pool or company devices. I can only read data, not change it.</Typography>
          <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1 }}>
            {suggestions.map(suggestion => <Chip key={suggestion} label={suggestion} variant="outlined" onClick={() => void send(suggestion)} disabled={sending} />)}
          </Stack>
        </Stack>}
        {messages.map((message, index) => <Box key={index} data-role={message.role} sx={{
          alignSelf: message.role === 'user' ? 'flex-end' : 'flex-start', maxWidth: '88%', px: 1.5, py: 1, borderRadius: 2, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', fontSize: 14,
          bgcolor: message.role === 'user' ? 'primary.main' : 'action.hover', color: message.role === 'user' ? 'primary.contrastText' : 'text.primary',
        }}>{message.text}</Box>)}
        {sending && <Stack direction="row" spacing={1} sx={{ alignItems: 'center', color: 'text.secondary' }}><CircularProgress size={16} /><Typography variant="body2">Thinking…</Typography></Stack>}
        {error && <Alert severity="warning">{error}</Alert>}
        <div ref={endRef} />
      </Box>

      <Box component="form" onSubmit={onSubmit} sx={{ p: 2, borderTop: 1, borderColor: 'divider' }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-end' }}>
          <TextField
            fullWidth multiline maxRows={4} size="small" placeholder="Ask a question…" value={draft}
            onChange={event => setDraft(event.target.value.slice(0, maxLength))} onKeyDown={onKeyDown}
            slotProps={{ htmlInput: { 'aria-label': 'Question', maxLength } }}
          />
          <IconButton type="submit" color="primary" aria-label="Send" disabled={sending || !draft.trim()}><SendIcon /></IconButton>
        </Stack>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
          Names, EPF and mobile numbers are hidden from the AI service; it sees amounts, factories and departments.
        </Typography>
      </Box>
    </Drawer>
  </>
}
