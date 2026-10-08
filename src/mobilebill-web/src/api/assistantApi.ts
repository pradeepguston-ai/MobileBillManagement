import { apiFetch } from './http'

export type AssistantStatus = { isEnabled: boolean }
export type AssistantReply = { conversationId: string; reply: string }

export function getAssistantStatus() {
  return apiFetch<AssistantStatus>('/api/assistant/status')
}

// conversationId omitted starts a new conversation.
export function askAssistant(message: string, conversationId?: string) {
  return apiFetch<AssistantReply>('/api/assistant/ask', { method: 'POST', body: JSON.stringify({ message, conversationId: conversationId ?? null }) })
}
