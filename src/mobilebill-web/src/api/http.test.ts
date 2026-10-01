import { afterEach, describe, expect, it, vi } from 'vitest'

import { ApiError, apiResponse } from './http'

describe('apiResponse', () => {
  afterEach(() => vi.restoreAllMocks())

  it('preserves safe ProblemDetails status and detail', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ status: 409, title: 'Conflict', detail: 'The batch status changed.' }), { status: 409, headers: { 'Content-Type': 'application/problem+json' } }))

    const error = await apiResponse('/conflict').catch(reason => reason)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(409)
    expect(error.message).toBe('The batch status changed.')
  })

  it('does not expose raw HTML error pages', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('<html><body>server internals</body></html>', { status: 500, headers: { 'Content-Type': 'text/html' } }))

    const error = await apiResponse('/failure').catch(reason => reason)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.message).toBe('The server could not complete the request.')
    expect(error.message).not.toContain('server internals')
  })
})
