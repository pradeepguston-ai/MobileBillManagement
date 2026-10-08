import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AssistantChat } from './AssistantChat'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('Assistant chat', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('stays hidden when the assistant is not set up', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ isEnabled: false }))
    render(<AssistantChat />)

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: 'Open assistant' })).toBeNull()
  })

  it('asks a question, shows the answer and continues the same conversation', async () => {
    const asked: unknown[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      if (String(input).includes('/api/assistant/status')) return json({ isEnabled: true })
      asked.push(JSON.parse(String(init?.body)))
      return json({ conversationId: 'conv-1', reply: asked.length === 1 ? 'August excess was LKR 9,000.00.' : 'Sam Perera holds 761499198.' })
    })
    render(<AssistantChat />)

    fireEvent.click(await screen.findByRole('button', { name: 'Open assistant' }))
    fireEvent.click(screen.getByRole('button', { name: 'Summarise the latest billing batch' }))
    const log = screen.getByRole('log', { name: 'Conversation' })
    expect(await within(log).findByText('August excess was LKR 9,000.00.')).toBeTruthy()

    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), { target: { value: 'Who holds 761499198?' } })
    fireEvent.keyDown(screen.getByRole('textbox', { name: 'Question' }), { key: 'Enter' })
    expect(await within(log).findByText('Sam Perera holds 761499198.')).toBeTruthy()

    expect(asked).toEqual([{ message: 'Summarise the latest billing batch', conversationId: null }, { message: 'Who holds 761499198?', conversationId: 'conv-1' }])
    expect(within(log).getByText('Who holds 761499198?')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'New chat' }))
    expect(within(log).queryByText('Sam Perera holds 761499198.')).toBeNull()
  })

  it('shows why the assistant could not answer', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/status')
      ? json({ isEnabled: true })
      : json({ title: "The free AI model is busy or today's free limit has been used. Try again in a minute." }, 503))
    render(<AssistantChat />)

    fireEvent.click(await screen.findByRole('button', { name: 'Open assistant' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), { target: { value: 'Hi' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText(/today's free limit has been used/)).toBeTruthy()
  })
})
