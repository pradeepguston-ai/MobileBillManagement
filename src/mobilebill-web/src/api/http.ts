import type { ApiProblem } from './types'
import { clearToken, getToken, notifyUnauthorized } from './tokenStore'

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5283'

export class ApiError extends Error {
  readonly status: number

   constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

const statusMessages: Record<number, string> = {
  400: 'The request could not be completed. Check the supplied information.',
  403: 'You are not authorized to perform this action.',
  404: 'The requested information was not found.',
  409: 'The request conflicts with the current billing state.',
}

function isSafeProblemText(value: unknown): value is string {
  if (typeof value !== 'string' || value.trim().length === 0 || value.length > 500) return false
  return !/<(?:!doctype|html|body|script)\b/i.test(value)
    && !/(?:[a-z]:\\|\/var\/|\/home\/|stack\s*trace|inner\s*exception|system\.[a-z])/i.test(value)
}

function errorMessage(status: number, problem: ApiProblem) {
  if (isSafeProblemText(problem.detail)) return problem.detail.trim()
  if (isSafeProblemText(problem.title)) return problem.title.trim()
  return statusMessages[status] ?? (status >= 500 ? 'The server could not complete the request.' : `Request failed (${status}).`)
}

export async function apiResponse(path: string, options?: RequestInit): Promise<Response> {
  const headers = new Headers(options?.headers)
  if (options?.body && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  const token = getToken()
  if (token && !headers.has('Authorization')) headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(`${apiBaseUrl}${path}`, { ...options, headers })
  if (response.status === 401 && token) {
    // The session token we sent was rejected (expired/invalid) — distinct from an anonymous
    // login attempt failing, which also returns 401 but with no token attached.
    clearToken()
    notifyUnauthorized()
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as ApiProblem
    throw new ApiError(errorMessage(response.status, problem), response.status)
  }
  return response
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await apiResponse(path, options)
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}
